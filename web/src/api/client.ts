/**
 * The one place that talks HTTP. Adds the bearer token, parses JSON, and turns
 * error responses into ApiError, including the per-field errors from ASP.NET's
 * ValidationProblemDetails ({ errors: { company: ["Company is required."] } }).
 */

const BASE_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5226').replace(/\/$/, '')

export type FieldErrors = Record<string, string>

export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: FieldErrors

  constructor(status: number, message: string, fieldErrors: FieldErrors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

// Module state rather than React state, so this file doesn't depend on React.
// AuthProvider keeps it in sync with the signed-in session.
let token: string | null = null
let onUnauthorized: () => void = () => {}

export function setAuthToken(value: string | null) {
  token = value
}

/** Called on any 401 from an authenticated request (expired or revoked token). */
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

export async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const sentToken = token
  if (sentToken) headers.Authorization = `Bearer ${sentToken}`

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, "Can't reach the OppsView API. Is it running?")
  }

  if (response.status === 401 && sentToken) onUnauthorized()

  if (!response.ok) throw await toApiError(response)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: { title?: string; errors?: Record<string, string[]> } = {}
  try {
    problem = await response.json()
  } catch {
    // Not JSON (e.g. a proxy error page). Fall back to the status text.
  }

  const fieldErrors: FieldErrors = {}
  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    // Keys are camelCase on our API; lower-case the first letter anyway so
    // "$.platform"-style or PascalCase keys from other sources still match.
    const key = field.replace(/^\$\./, '').replace(/^./, (c) => c.toLowerCase())
    if (messages.length > 0) fieldErrors[key] = messages[0]
  }

  const message = problem.title ?? (response.statusText || `Request failed (${response.status})`)
  return new ApiError(response.status, message, fieldErrors)
}
