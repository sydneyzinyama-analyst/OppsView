import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth'
import { setAuthToken, setUnauthorizedHandler } from '../api/client'
import { AuthContext, type AuthContextValue } from './context'
import { clearSession, loadSession, saveSession, type Session } from './session'

/**
 * Holds the signed-in session and keeps the API client's token in step with it.
 * The token is pushed to the client at the moment it changes (initial load,
 * sign-in, sign-out) rather than in an effect, because child components'
 * effects run before this provider's and may fire requests straight away.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => {
    const restored = loadSession()
    setAuthToken(restored?.token ?? null)
    return restored
  })

  const logout = useCallback(() => {
    clearSession()
    setAuthToken(null)
    setSession(null)
  }, [])

  const signIn = useCallback((next: Session) => {
    saveSession(next)
    setAuthToken(next.token)
    setSession(next)
  }, [])

  useEffect(() => setUnauthorizedHandler(logout), [logout])

  // Log out the moment the token expires instead of waiting for the next 401.
  useEffect(() => {
    if (!session) return
    const msLeft = new Date(session.expiresAt).getTime() - Date.now()
    // setTimeout overflows above ~24.8 days; our tokens are far shorter.
    const timer = setTimeout(logout, Math.max(0, Math.min(msLeft, 2 ** 31 - 1)))
    return () => clearTimeout(timer)
  }, [session, logout])

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      logout,
      login: async (email, password) => signIn(await authApi.login({ email, password })),
      register: async (email, password) => signIn(await authApi.register({ email, password })),
    }),
    [session, logout, signIn],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
