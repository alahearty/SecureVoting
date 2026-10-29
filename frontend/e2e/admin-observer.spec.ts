import { expect, test, type Page } from '@playwright/test'

const password = 'DemoPass!2026Secure'

async function signIn(page: Page, identifier: string): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Matriculation number or email').fill(identifier)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
}

test('electoral officer sees the administration workspace', async ({ page }) => {
  await signIn(page, 'officer@demo.iaue.edu.ng')
  await expect(page).toHaveURL(/\/admin$/, { timeout: 20_000 })
  await expect(page.getByText('Electoral operations')).toBeVisible()
  await page.getByRole('link', { name: 'Elections', exact: true }).click()
  await expect(page).toHaveURL(/\/admin\/elections$/)
  await expect(page.getByText('Election administration')).toBeVisible()
})

test('observer can open the audit trail and verify the chain', async ({ page }) => {
  await signIn(page, 'auditor@demo.iaue.edu.ng')
  await expect(page).toHaveURL(/\/observer$/, { timeout: 20_000 })
  await page.getByRole('link', { name: 'Audit trail', exact: true }).click()
  await expect(page).toHaveURL(/\/observer\/audit$/)
  await page.getByRole('button', { name: 'Verify chain' }).click()
  await expect(page.getByText('Audit chain verified')).toBeVisible({ timeout: 15_000 })
})
