import type { AuthResponse } from '../api/types'

/**
 * The signed-in session, persisted in localStorage so a page refresh doesn't
 * log you out.
 *
 * Trade-off: anything in localStorage is readable by JavaScript on the page,
 * so an XSS bug could steal the token. The safer alternative is an httpOnly
 * cookie set by the API, which needs CSRF protection and same-site hosting.
 * For this project a short-lived token (8h) in localStorage is the pragmatic
 * choice; React escapes rendered text, and we never use dangerouslySetInnerHTML.
 */
export type Session = AuthResponse

const KEY = 'oppsview.session'

export function loadSession(now: Date = new Date()): Session | null {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    if (!session.token || isExpired(session, now)) {
      localStorage.removeItem(KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: Session) {
  try {
    localStorage.setItem(KEY, JSON.stringify(session))
  } catch {
    // Private mode / storage disabled: the session still works until refresh.
  }
}

export function clearSession() {
  try {
    localStorage.removeItem(KEY)
  } catch {
    // ignore
  }
}

export function isExpired(session: Session, now: Date = new Date()): boolean {
  return new Date(session.expiresAt).getTime() <= now.getTime()
}
