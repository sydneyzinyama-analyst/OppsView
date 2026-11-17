import { useCallback, useState, type ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { BellContext } from './bell'
import { ReminderBell } from './ReminderBell'

/** The signed-in frame: brand, menu, who's signed in, and the page underneath. */
export function AppLayout({ children }: { children: ReactNode }) {
  const { session, logout } = useAuth()
  // Bumped when a page reports a reminder was dealt with, so the bell recounts.
  const [bellKey, setBellKey] = useState(0)
  const refreshBell = useCallback(() => setBellKey((k) => k + 1), [])
  return (
    <BellContext.Provider value={refreshBell}>
      <header className="topbar">
        <div className="topbar-left">
          <span className="brand">OppsView</span>
          <nav aria-label="Main">
            <NavLink to="/" end>
              Applications
            </NavLink>
            <NavLink to="/insights">Insights</NavLink>
            <NavLink to="/match">CV match</NavLink>
          </nav>
        </div>
        <div className="user">
          <ReminderBell refreshKey={bellKey} />
          <span className="muted">{session?.email}</span>
          <button type="button" onClick={logout}>
            Sign out
          </button>
        </div>
      </header>
      {children}
    </BellContext.Provider>
  )
}
