import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as remindersApi from '../api/reminders'
import { ReminderBell } from './ReminderBell'

function renderBell() {
  render(
    <MemoryRouter>
      <ReminderBell />
    </MemoryRouter>,
  )
}

describe('ReminderBell', () => {
  it('shows how many reminders are waiting and links to them', async () => {
    vi.spyOn(remindersApi, 'countReminders').mockResolvedValue(3)
    renderBell()

    const bell = await screen.findByRole('link', { name: 'Reminders, 3 waiting' })
    expect(bell).toHaveAttribute('href', '/reminders')
    expect(bell).toHaveTextContent('3')
  })

  it('shows no number when there is nothing waiting', async () => {
    vi.spyOn(remindersApi, 'countReminders').mockResolvedValue(0)
    renderBell()
    expect(await screen.findByRole('link', { name: 'Reminders' })).not.toHaveTextContent(/\d/)
  })

  it('caps the number at 9+', async () => {
    vi.spyOn(remindersApi, 'countReminders').mockResolvedValue(14)
    renderBell()
    expect(await screen.findByText('9+')).toBeInTheDocument()
  })

  it('stays quiet if the count cannot be loaded', async () => {
    vi.spyOn(remindersApi, 'countReminders').mockRejectedValue(new Error('offline'))
    renderBell()
    expect(await screen.findByRole('link', { name: 'Reminders' })).toBeInTheDocument()
  })
})
