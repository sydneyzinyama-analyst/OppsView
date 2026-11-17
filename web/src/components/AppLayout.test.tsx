import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as remindersApi from '../api/reminders'
import { AuthContext, type AuthContextValue } from '../auth/context'
import { userInitials } from '../lib/avatar'
import { AppLayout } from './AppLayout'

function Where() {
  const location = useLocation()
  return <p data-testid="where">{location.pathname + location.search}</p>
}

function renderLayout(url = '/insights') {
  const logout = vi.fn()
  const auth = { session: { email: 'sydney.zinyama@example.com' }, logout } as unknown as AuthContextValue
  render(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route
            path="*"
            element={
              <AppLayout>
                <Where />
              </AppLayout>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  )
  return { logout }
}

describe('AppLayout (masthead)', () => {
  beforeEach(() => {
    vi.spyOn(remindersApi, 'countReminders').mockResolvedValue(0)
  })

  it('makes initials from the email address', () => {
    expect(userInitials('sydney.zinyama@example.com')).toBe('SZ')
    expect(userInitials('me@example.com')).toBe('ME')
  })

  it('searches the ledger from any page', async () => {
    renderLayout('/insights')
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search applications' }), 'a')
    expect(screen.getByTestId('where')).toHaveTextContent('/?q=a')
  })

  it('opens a new entry on the ledger', async () => {
    renderLayout('/match')
    await userEvent.click(screen.getByRole('button', { name: 'New entry' }))
    expect(screen.getByTestId('where')).toHaveTextContent('/?new=1')
  })

  it('signs out from the account menu', async () => {
    const { logout } = renderLayout()
    await userEvent.click(screen.getByRole('button', { name: /Account/ }))
    expect(screen.getByText('sydney.zinyama@example.com')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(logout).toHaveBeenCalled()
  })
})
