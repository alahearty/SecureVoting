import { expect, test, request as playwrightRequest, type APIRequestContext, type APIResponse } from '@playwright/test'
const apiBase = process.env.SECUREVOTE_E2E_URL ?? 'http://localhost:5000'

const officerEmail = 'officer@demo.iaue.edu.ng'
const auditorEmail = 'auditor@demo.iaue.edu.ng'
const superAdminEmail = 'superadmin@demo.iaue.edu.ng'
const demoPassword = 'DemoPass!2026Secure'
const voterPassword = 'E2eVoter!2026'
const candidatePassword = 'E2eCandidate!2026'

async function csrf(request: APIRequestContext): Promise<string> {
  const response = await request.get('/api/auth/csrf')
  expect(response.ok()).toBeTruthy()
  return ((await response.json()) as { token: string }).token
}

async function login(request: APIRequestContext, identifier: string, password: string): Promise<void> {
  const token = await csrf(request)
  const response = await request.post('/api/auth/login', {
    headers: { 'X-CSRF-TOKEN': token },
    data: { identifier, password }
  })
  expect(response.ok(), await response.text()).toBeTruthy()
}

async function authed(request: APIRequestContext, method: 'post' | 'put' | 'get', path: string, data?: unknown): Promise<APIResponse> {
  if (method === 'get') return request.get(path)
  const token = await csrf(request)
  return request[method](path, {
    headers: { 'X-CSRF-TOKEN': token },
    ...(data === undefined ? {} : { data })
  })
}

test('secure departmental election from setup through publication and student result', async ({ page, request }) => {
  await login(request, officerEmail, demoPassword)
  const departmentsResponse = await authed(request, 'get', '/api/departments')
  expect(departmentsResponse.ok()).toBeTruthy()
  const departments = (await departmentsResponse.json()) as Array<{ id: string; code: string }>
  const departmentId = departments.find((department) => department.code === 'CSC')?.id
  expect(departmentId).toBeTruthy()

  const now = Date.now()
  const nominationClose = new Date(now + 8_000).toISOString()
  const opensAt = new Date(now + 12_000).toISOString()
  const closesAt = new Date(now + 45 * 60_000).toISOString()
  const unique = String(now).slice(-6)
  const voterMatric = `E2E/CS/27/${unique}1`
  const candidateMatric = `E2E/CS/27/${unique}2`
  const electionTitle = `E2E Departmental Election ${now}`
  const created = await authed(request, 'post', '/api/admin/elections', {
    title: electionTitle,
    description: 'Synthetic Playwright end-to-end election.',
    academicSession: '2026/2027',
    departmentId: departmentId!,
    nominationOpensAt: new Date(now - 60 * 60_000).toISOString(),
    nominationClosesAt: nominationClose,
    opensAt,
    closesAt,
    allowAbstention: true,
    tieHandling: 'RunoffRequired',
    nominationRequiresApproval: true
  })
  expect(created.status()).toBe(201)
  const electionId = ((await created.json()) as { id: string }).id

  const positionResponse = await authed(request, 'post', `/api/admin/elections/${electionId}/positions`, {
    title: 'President',
    description: 'E2E position',
    displayOrder: 1,
    seatCount: 1,
    maxSelections: 1
  })
  expect(positionResponse.status()).toBe(201)
  const positionId = ((await positionResponse.json()) as { id: string }).id

  const csv = 'matric,fullname,email,level,department\n' +
    `${voterMatric},E2E Eligible Voter,e2e-voter-${unique}@demo.iaue.edu.ng,300,Computer Science\n` +
    `${candidateMatric},E2E Candidate,e2e-candidate-${unique}@demo.iaue.edu.ng,300,Computer Science\n`
  const importToken = await csrf(request)
  const importResponse = await request.post(`/api/admin/elections/${electionId}/voters/import`, {
    headers: { 'X-CSRF-TOKEN': importToken },
    multipart: {
      file: {
        name: 'e2e-voters.csv',
        mimeType: 'text/csv',
        buffer: Buffer.from(csv)
      }
    }
  })
  expect(importResponse.ok(), await importResponse.text()).toBeTruthy()
  const imported = (await importResponse.json()) as {
    activationTokens: Array<{ matricNumber: string; token: string }>
    errors: Array<{ rowNumber: number; message: string }>
  }
  expect(imported.errors, JSON.stringify(imported.errors)).toHaveLength(0)
  const voterActivation = imported.activationTokens.find((item) => item.matricNumber === voterMatric)
  const candidateActivation = imported.activationTokens.find((item) => item.matricNumber === candidateMatric)
  expect(voterActivation).toBeTruthy()
  expect(candidateActivation).toBeTruthy()

  const candidateContext = await playwrightRequest.newContext({ baseURL: apiBase })
  try {
    const activateToken = await csrf(candidateContext)
    const activate = await candidateContext.post('/api/auth/activate', {
      headers: { 'X-CSRF-TOKEN': activateToken },
      data: {
        matricNumber: candidateMatric,
        token: candidateActivation!.token,
        password: candidatePassword,
        confirmPassword: candidatePassword
      }
    })
    expect(activate.ok(), await activate.text()).toBeTruthy()
    const nomination = await authed(candidateContext, 'post', '/api/candidate/nominations', {
      electionId,
      positionId,
      manifesto: 'I will represent every student with transparency, accessibility and accountable communication.'
    })
    expect(nomination.status()).toBe(201)
    const profiles = await authed(request, 'get', `/api/admin/elections/${electionId}/candidates`)
    const candidates = (await profiles.json()) as Array<{ id: string; matricNumber: string }>
    const candidate = candidates.find((item) => item.matricNumber === candidateMatric)
    expect(candidate).toBeTruthy()
    const approve = await authed(request, 'post',
      `/api/admin/elections/${electionId}/candidates/${candidate!.id}/approve`, {})
    expect(approve.status()).toBe(204)
  } finally {
    await candidateContext.dispose()
  }

  const schedule = await authed(request, 'post', `/api/admin/elections/${electionId}/actions/schedule`, {
    reason: 'E2E register finalised.'
  })
  expect(schedule.status()).toBe(204)

  await page.waitForTimeout(Math.max(0, new Date(opensAt).getTime() - Date.now() + 1500))
  const open = await authed(request, 'post', `/api/admin/elections/${electionId}/actions/open`, {
    reason: 'E2E voting opened.'
  })
  expect(open.status()).toBe(204)

  await page.goto('/activate')
  await page.getByLabel('Matriculation number').fill(voterMatric)
  await page.getByLabel('Activation token').fill(voterActivation!.token)
  await page.getByLabel('New password').fill(voterPassword)
  await page.getByLabel('Confirm password').fill(voterPassword)
  await page.getByRole('button', { name: 'Activate account' }).click()
  await expect(page).toHaveURL(/\/student$/, { timeout: 30_000 })

  await page.goto('/student/elections')
  await page.getByText(electionTitle, { exact: true }).first().click()
  await page.getByRole('button', { name: /Cast your ballot/i }).click()
  await page.locator('[data-testid^="candidate-"]').first().click()
  await expect(page.getByTestId('review-ballot')).toBeEnabled()
  await page.getByTestId('review-ballot').click()
  await page.getByRole('checkbox').check()
  await page.getByRole('button', { name: /Submit final ballot/i }).click()
  await page.getByRole('button', { name: 'Submit ballot', exact: true }).click()
  await expect(page).toHaveURL(/\/student\/elections\/.*\/confirmed/, { timeout: 30_000 })
  await expect(page.getByText('Your vote has been recorded.')).toBeVisible()

  const close = await authed(request, 'post', `/api/admin/elections/${electionId}/actions/close`, {
    reason: 'E2E voting completed.'
  })
  expect(close.status()).toBe(204)
  const review = await authed(request, 'post', `/api/elections/${electionId}/results/review`)
  expect(review.status()).toBe(200)
  const provisional = (await review.json()) as { id: string }

  const auditorContext = await playwrightRequest.newContext({ baseURL: apiBase })
  try {
    await login(auditorContext, auditorEmail, demoPassword)
    const verify = await authed(auditorContext, 'post',
      `/api/elections/${electionId}/results/${provisional.id}/verify`,
      { notes: 'E2E aggregate reconciliation checked.' })
    expect(verify.status()).toBe(200)
  } finally {
    await auditorContext.dispose()
  }

  const superContext = await playwrightRequest.newContext({ baseURL: apiBase })
  try {
    await login(superContext, superAdminEmail, demoPassword)
    const publish = await authed(superContext, 'post',
      `/api/elections/${electionId}/results/${provisional.id}/publish`,
      { notes: 'E2E publication approved.' })
    expect(publish.status()).toBe(200)
  } finally {
    await superContext.dispose()
  }

  await page.goto('/student/results')
  await page.getByText(electionTitle, { exact: true }).first().click()
  await expect(page.getByText('E2E Candidate')).toBeVisible()
  await expect(page.getByText('Official result')).toBeVisible()
})
