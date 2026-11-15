import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import * as emailApi from '../api/email'
import type { EmailStatus } from '../api/types'
import { EmailPanel } from './EmailPanel'

const status = (overrides: Partial<EmailStatus> = {}): EmailStatus => ({
  available: true,
  connected: false,
  emailAddress: null,
  lastSyncedAt: null,
  needsReconnect: false,
  needsReviewCount: 0,
  ...overrides,
})

describe('EmailPanel', () => {
  it('explains when the server has no Gmail settings', () => {
    render(<EmailPanel status={status({ available: false })} onChanged={vi.fn()} />)
    expect(screen.getByText(/isn't set up on this server/)).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('sends the browser to Google to connect', async () => {
    vi.spyOn(emailApi, 'startGmailConnect').mockResolvedValue('https://accounts.google.com/consent')
    const navigate = vi.fn()
    render(<EmailPanel status={status()} onChanged={vi.fn()} navigate={navigate} />)

    await userEvent.click(screen.getByRole('button', { name: 'Connect Gmail' }))

    expect(navigate).toHaveBeenCalledWith('https://accounts.google.com/consent')
  })

  it('syncs and reports what happened', async () => {
    vi.spyOn(emailApi, 'syncEmail').mockResolvedValue({
      created: 2, updated: 1, noChange: 0, ignored: 5, needsReview: 1, alreadyProcessed: 0,
    })
    const onChanged = vi.fn()
    render(<EmailPanel status={status({ connected: true, emailAddress: 'me@gmail.com' })} onChanged={onChanged} />)

    expect(screen.getByText('me@gmail.com')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Sync now' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Added 2 applications, updated 1. 1 email needs your review.')
    expect(onChanged).toHaveBeenCalled()
  })

  it('shows sync errors and still refreshes the status', async () => {
    vi.spyOn(emailApi, 'syncEmail').mockRejectedValue(new Error("Gmail isn't connected. Please connect it again."))
    const onChanged = vi.fn()
    render(<EmailPanel status={status({ connected: true, emailAddress: 'me@gmail.com' })} onChanged={onChanged} />)

    await userEvent.click(screen.getByRole('button', { name: 'Sync now' }))

    expect(await screen.findByRole('status')).toHaveTextContent("Gmail isn't connected")
    // The server may have just flagged the connection; the page must reload it.
    expect(onChanged).toHaveBeenCalled()
  })

  it('asks to reconnect when Google stopped access', () => {
    render(<EmailPanel status={status({ connected: true, needsReconnect: true, emailAddress: 'me@gmail.com' })} onChanged={vi.fn()} />)
    expect(screen.getByRole('alert')).toHaveTextContent('every 7 days')
    expect(screen.getByRole('button', { name: 'Reconnect Gmail' })).toBeInTheDocument()
  })

  it('disconnects only after confirmation', async () => {
    const disconnect = vi.spyOn(emailApi, 'disconnectGmail').mockResolvedValue(undefined)
    vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true)
    render(<EmailPanel status={status({ connected: true, emailAddress: 'me@gmail.com' })} onChanged={vi.fn()} />)

    await userEvent.click(screen.getByRole('button', { name: 'Disconnect' }))
    expect(disconnect).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Disconnect' }))
    expect(disconnect).toHaveBeenCalledOnce()
  })
})
