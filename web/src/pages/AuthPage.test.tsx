import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../auth/AuthProvider'
import { RequireAuth } from '../auth/RequireAuth'
import { jsonResponse } from '../test/fixtures'
import { AuthPage } from './AuthPage'

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal('fetch', fetchMock)

function renderApp(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<AuthPage mode="login" />} />
          <Route path="/register" element={<AuthPage mode="register" />} />
          <Route
            path="/"
            element={
              <RequireAuth>
                <p>Signed-in home</p>
              </RequireAuth>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

const authResponse = { token: 'jwt', email: 'me@example.com', expiresAt: new Date(Date.now() + 3_600_000).toISOString() }

describe('auth flow', () => {
  it('redirects signed-out users to the login page', () => {
    renderApp('/')
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('signs in, stores the session and goes to the home page', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, authResponse))
    renderApp('/')

    await userEvent.type(screen.getByLabelText('Email'), 'me@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'password123')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Signed-in home')).toBeInTheDocument()
    expect(JSON.parse(localStorage.getItem('oppsview.session')!).token).toBe('jwt')
  })

  it('shows the server message for wrong credentials', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { title: 'Incorrect email or password.' }))
    renderApp('/login')

    await userEvent.type(screen.getByLabelText('Email'), 'me@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'nope-nope')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect email or password.')
  })

  it('validates registration before calling the API', async () => {
    renderApp('/register')

    await userEvent.type(screen.getByLabelText('Email'), 'not-an-email')
    await userEvent.type(screen.getByLabelText('Password'), 'short')
    await userEvent.click(screen.getByRole('button', { name: 'Create account' }))

    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText('Use at least 8 characters.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('restores a saved session on load', () => {
    localStorage.setItem('oppsview.session', JSON.stringify(authResponse))
    renderApp('/')
    expect(screen.getByText('Signed-in home')).toBeInTheDocument()
  })
})
