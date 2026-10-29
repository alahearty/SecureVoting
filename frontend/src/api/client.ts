import type { ProblemDetails } from './types'

export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

let csrfToken = ''
let csrfPromise: Promise<string> | null = null

async function readProblem(response: Response): Promise<ApiError> {
  let body: ProblemDetails | null = null
  try {
    body = (await response.json()) as ProblemDetails
  } catch {
    body = null
  }

  const message =
    body?.detail ||
    body?.title ||
    (response.status === 401
      ? 'Your session is not authorised.'
      : 'The request could not be completed.')
  return new ApiError(response.status, message, body?.errors ?? {})
}

export async function fetchCsrf(force = false): Promise<string> {
  if (csrfToken && !force) {
    return csrfToken
  }

  if (csrfPromise && !force) {
    return csrfPromise
  }

  csrfPromise = fetch('/api/auth/csrf', {
    credentials: 'include',
    headers: { Accept: 'application/json' }
  }).then(async (response) => {
    if (!response.ok) {
      throw await readProblem(response)
    }

    const payload = (await response.json()) as { token: string }
    csrfToken = payload.token
    return csrfToken
  })

  try {
    return await csrfPromise
  } finally {
    csrfPromise = null
  }
}

export function clearCsrf(): void {
  csrfToken = ''
}

export interface ApiOptions extends Omit<RequestInit, 'body'> {
  body?: unknown
}

export async function api<T>(path: string, init: ApiOptions = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase()
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')

  let body = init.body
  if (body !== undefined && body !== null && !(body instanceof FormData) && typeof body !== 'string') {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(body)
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method)) {
    headers.set('X-CSRF-TOKEN', await fetchCsrf())
  }

  const request: RequestInit = {
    method,
    headers,
    credentials: 'include'
  }
  if (body !== undefined) {
    request.body = body as BodyInit
  }

  const response = await fetch(`/api${path}`, request)

  if (response.status === 401) {
    window.dispatchEvent(new CustomEvent('securevote:unauthorized'))
  }

  if (!response.ok) {
    throw await readProblem(response)
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  return (await response.json()) as T
}

export async function download(path: string, filename: string): Promise<void> {
  const response = await fetch(`/api${path}`, {
    credentials: 'include',
    headers: { Accept: 'text/csv' }
  })
  if (!response.ok) {
    throw await readProblem(response)
  }

  const blob = await response.blob()
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}
