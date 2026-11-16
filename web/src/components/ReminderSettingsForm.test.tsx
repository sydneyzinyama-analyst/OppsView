import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import * as remindersApi from '../api/reminders'
import { ReminderSettingsForm } from './ReminderSettingsForm'

const settings = { enabled: true, followUpDays: 7, moveOnDays: 21, afterInterviewDays: 5, offerReplyDays: 2 }

describe('ReminderSettingsForm', () => {
  it('saves changed days', async () => {
    const save = vi.spyOn(remindersApi, 'saveReminderSettings').mockImplementation(async (s) => s)
    const onSaved = vi.fn()
    render(<ReminderSettingsForm settings={settings} onSaved={onSaved} />)

    const followUp = screen.getByLabelText('Follow up')
    await userEvent.clear(followUp)
    await userEvent.type(followUp, '10')
    await userEvent.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(save).toHaveBeenCalledWith({ ...settings, followUpDays: 10 })
    expect(await screen.findByRole('status')).toHaveTextContent('re-planned')
    expect(onSaved).toHaveBeenCalled()
  })

  it('can switch reminders off, which disables the day fields', async () => {
    const save = vi.spyOn(remindersApi, 'saveReminderSettings').mockImplementation(async (s) => s)
    render(<ReminderSettingsForm settings={settings} onSaved={vi.fn()} />)

    await userEvent.click(screen.getByLabelText('Send me follow-up reminders'))
    expect(screen.getByLabelText('Follow up')).toBeDisabled()
    await userEvent.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(save).toHaveBeenCalledWith({ ...settings, enabled: false })
  })

  it("shows the server's message next to the right field", async () => {
    vi.spyOn(remindersApi, 'saveReminderSettings').mockRejectedValue(
      new ApiError(400, 'Validation failed', { moveOnDays: '"Move on" should come after "follow up".' }),
    )
    render(<ReminderSettingsForm settings={settings} onSaved={vi.fn()} />)

    await userEvent.click(screen.getByRole('button', { name: 'Save settings' }))

    expect(await screen.findByText(/should come after/)).toBeInTheDocument()
    expect(screen.getByLabelText('Time to move on')).toHaveAttribute('aria-invalid', 'true')
  })
})
