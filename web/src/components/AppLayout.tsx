import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'

/** The signed-in frame: brand, menu, who's signed in, and the page underneath. */
export function AppLayout({ children }: { children: ReactNode }) {
  const { session, logout } = useAuth()
  return (
    <>
      <header className="topbar">
        <div className="topbar-left">
          <span className="brand">OppsView</span>
          <nav aria-label="Main">
            <NavLink to="/" end>
              Applications
            </NavLink>
            <NavLink to="/match">CV match</NavLink>
          </nav>
        </div>
        <div className="user">
          <span className="muted">{session?.email}</span>
          <button type="button" onClick={logout}>
            Sign out
          </button>
        </div>
      </header>
      {children}
    </>
  )
}
