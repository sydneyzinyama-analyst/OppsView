import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as remindersApi from '../api/reminders'
import type { Reminder } from '../api/types'
import { BellContext } from '../components/bell'
import { RemindersPage } from './RemindersPage'

const reminder = (overrides: Partial<Reminder> = {}): Reminder => ({
  id: 'r1',
  applicationId: 'a1',
  company: 'Takealot',
  role: 'Junior Backend Developer',
  kind: 'FOLLOW_UP',
  message: "You applied to Takealot (Junior Backend Developer) 7 days ago and haven't heard back.",
  dueAt: '2026-11-15T08:00:00Z',
  ...overrides,
})

const settings = { enabled: true, followUpDays: 7, moveOnDays: 21, afterInterviewDays: 5, offerReplyDays: 2 }

function renderPage(reminders: Reminder[]) {
  vi.spyOn(remindersApi, 'listReminders').mockResolvedValue(reminders)
  vi.spyOn(remindersApi, 'getReminderSettings').mockResolvedValue(settings)
  const refreshBell = vi.fn()
  render(
    <MemoryRouter>
      <BellContext.Provider value={refreshBell}>
        <RemindersPage />
      </BellContext.Provider>
    </MemoryRouter>,
  )
  return refreshBell
}

describe('RemindersPage', () => {
  it('lists reminders with what to do and a link to the application', async () => {
    renderPage([reminder()])

    const item = (await screen.findByText(/7 days ago/)).closest('li')!
    expect(within(item).getByText('Follow up')).toBeInTheDocument()
    expect(within(item).getByRole('link', { name: 'View application' })).toHaveAttribute('href', '/?highlight=a1')
  })

  it.each([
    ['Done', 'markReminderDone'],
    ['Dismiss', 'dismissReminder'],
  ] as const)('%s removes the reminder and updates the bell', async (button, method) => {
    const call = vi.spyOn(remindersApi, method).mockResolvedValue(undefined)
    const refreshBell = renderPage([reminder()])

    await userEvent.click(await screen.findByRole('button', { name: button }))

    expect(call).toHaveBeenCalledWith('r1')
    expect(screen.queryByText(/7 days ago/)).not.toBeInTheDocument()
    expect(refreshBell).toHaveBeenCalled()
  })

  it('"In 3 days" snoozes for three days', async () => {
    const snooze = vi.spyOn(remindersApi, 'snoozeReminder').mockResolvedValue(undefined)
    renderPage([reminder()])

    await userEvent.click(await screen.findByRole('button', { name: 'In 3 days' }))

    expect(snooze).toHaveBeenCalledWith('r1', 3)
  })

  it('says when there is nothing to follow up on', async () => {
    renderPage([])
    expect(await screen.findByText(/Nothing to follow up on/)).toBeInTheDocument()
  })

  it('shows the settings form', async () => {
    renderPage([])
    expect(await screen.findByLabelText('Follow up')).toHaveValue(7)
  })
})
