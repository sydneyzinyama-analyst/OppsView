import { useCallback, useState, type ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { BellContext } from './bell'
import { BriefcaseIcon, ChartIcon, LogoMark, LogoutIcon, SparkleIcon } from './icons'
import { ReminderBell } from './ReminderBell'

/**
 * The signed-in frame. On a computer: a sidebar with the brand, the menu
 * and who's signed in. On a phone the same menu becomes a tab bar at the
 * bottom of the screen (thumb-friendly) and the brand sits on top. One menu
 * in the markup, two layouts in CSS.
 */
export function AppLayout({ children }: { children: ReactNode }) {
  const { session, logout } = useAuth()
  // Bumped when a page reports a reminder was dealt with, so the bell recounts.
  const [bellKey, setBellKey] = useState(0)
  const refreshBell = useCallback(() => setBellKey((k) => k + 1), [])
  const email = session?.email ?? ''

  return (
    <BellContext.Provider value={refreshBell}>
      <div className="shell">
        <aside className="sidebar">
          <div className="sidebar-brand">
            <LogoMark />
            <span className="brand">OppsView</span>
          </div>
          <nav aria-label="Main" className="nav">
            <NavLink to="/" end>
              <BriefcaseIcon />
              <span>Applications</span>
            </NavLink>
            <NavLink to="/insights">
              <ChartIcon />
              <span>Insights</span>
            </NavLink>
            <NavLink to="/match">
              <SparkleIcon />
              <span>CV match</span>
            </NavLink>
            <ReminderBell refreshKey={bellKey} />
          </nav>
          <div className="sidebar-user">
            <span className="avatar avatar-me" aria-hidden="true">
              {email.charAt(0).toUpperCase() || '?'}
            </span>
            <span className="sidebar-email" title={email}>
              {email}
            </span>
            <button type="button" className="icon-button" onClick={logout} aria-label="Sign out" title="Sign out">
              <LogoutIcon />
            </button>
          </div>
        </aside>
        <div className="main-area">{children}</div>
      </div>
    </BellContext.Provider>
  )
}
