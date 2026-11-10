import { describe, expect, it } from 'vitest'
import { loadSession, saveSession } from './session'

const session = { token: 't', email: 'me@example.com', expiresAt: '2026-11-10T20:00:00Z' }

describe('session storage', () => {
  it('restores a saved, unexpired session', () => {
    saveSession(session)
    expect(loadSession(new Date('2026-11-10T12:00:00Z'))).toEqual(session)
  })

  it('drops an expired session', () => {
    saveSession(session)
    expect(loadSession(new Date('2026-11-10T20:00:01Z'))).toBeNull()
    expect(localStorage.getItem('oppsview.session')).toBeNull()
  })

  it('ignores corrupt data', () => {
    localStorage.setItem('oppsview.session', '{not json')
    expect(loadSession()).toBeNull()
  })
})
