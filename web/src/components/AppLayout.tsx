import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { NavLink, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { userInitials } from '../lib/avatar'
import { BellContext } from './bell'
import { PlusIcon, SearchIcon } from './icons'
import { ReminderBell } from './ReminderBell'

/**
 * The signed-in frame: the masthead (logo, menu, search, "New entry", you)
 * and the page underneath. Search and "New entry" work from any page: they
 * go to the ledger with ?q=… or ?new=1 in the address, which the ledger reads.
 */
export function AppLayout({ children }: { children: ReactNode }) {
  const { session, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const onLedger = location.pathname === '/'

  // Bumped when a page reports a reminder was dealt with, so the count refreshes.
  const [bellKey, setBellKey] = useState(0)
  const refreshBell = useCallback(() => setBellKey((k) => k + 1), [])

  const query = onLedger ? (params.get('q') ?? '') : ''
  function search(value: string) {
    const next = value ? `/?q=${encodeURIComponent(value)}` : '/'
    // Typing on the ledger filters as you type, without filling the back button's history.
    navigate(next, { replace: onLedger })
  }

  const email = session?.email ?? ''
  const [menuOpen, setMenuOpen] = useState(false)
  const menu = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!menuOpen) return
    const close = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !menu.current?.contains(e.target as Node)) setMenuOpen(false)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [menuOpen])

  return (
    <BellContext.Provider value={refreshBell}>
      <div className="page">
        <header className="masthead">
          <NavLink to="/" className="logo" aria-label="OppsView, home">
            Opps<em>View</em>
          </NavLink>
          <nav aria-label="Main" className="nav">
            <NavLink to="/" end>
              Ledger
            </NavLink>
            <NavLink to="/insights">Insights</NavLink>
            <NavLink to="/match">CV match</NavLink>
            <ReminderBell refreshKey={bellKey} />
          </nav>
          <form className="masthead-search" role="search" onSubmit={(e) => e.preventDefault()}>
            <SearchIcon />
            <label className="visually-hidden" htmlFor="ledger-search">
              Search applications
            </label>
            <input
              id="ledger-search"
              type="search"
              placeholder="Search the ledger"
              value={query}
              onChange={(e) => search(e.target.value)}
            />
          </form>
          <button type="button" className="accent new-entry" onClick={() => navigate('/?new=1')}>
            <PlusIcon /> New entry
          </button>
          <div className="me" ref={menu}>
            <button
              type="button"
              className="me-button"
              aria-haspopup="true"
              aria-expanded={menuOpen}
              aria-label={`Account: ${email}`}
              onClick={() => setMenuOpen((o) => !o)}
            >
              {userInitials(email)}
            </button>
            {menuOpen && (
              <div className="me-menu">
                <p className="me-email">{email}</p>
                <button type="button" className="me-signout" onClick={logout}>
                  Sign out
                </button>
              </div>
            )}
          </div>
        </header>
        {children}
      </div>
    </BellContext.Provider>
  )
}
