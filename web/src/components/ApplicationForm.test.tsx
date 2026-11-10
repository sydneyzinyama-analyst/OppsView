import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import { makeApplication } from '../test/fixtures'
import { ApplicationForm } from './ApplicationForm'

describe('ApplicationForm', () => {
  it('shows field errors and does not submit an empty form', async () => {
    const onSubmit = vi.fn()
    render(<ApplicationForm onSubmit={onSubmit} />)

    await userEvent.click(screen.getByRole('button', { name: 'Add application' }))

    expect(screen.getByText('Company is required.')).toBeInTheDocument()
    expect(screen.getByText('Role is required.')).toBeInTheDocument()
    expect(screen.getByText('Choose where you applied.')).toBeInTheDocument()
    expect(screen.getByLabelText('Company')).toHaveAttribute('aria-invalid', 'true')
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits trimmed values, sends blank optional fields as null, then resets', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined)
    render(<ApplicationForm onSubmit={onSubmit} />)

    await userEvent.type(screen.getByLabelText('Company'), '  Acme  ')
    await userEvent.type(screen.getByLabelText('Role'), 'Developer')
    await userEvent.selectOptions(screen.getByLabelText('Platform'), 'LinkedIn')
    await userEvent.click(screen.getByRole('button', { name: 'Add application' }))

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ company: 'Acme', role: 'Developer', platform: 'LinkedIn', jobUrl: null, notes: null }),
    )
    expect(screen.getByLabelText('Company')).toHaveValue('')
  })

  it('shows server-side field errors next to the right input', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError(400, 'Validation failed', { appliedOn: "Applied date can't be in the future." }),
    )
    render(<ApplicationForm onSubmit={onSubmit} />)

    await userEvent.type(screen.getByLabelText('Company'), 'Acme')
    await userEvent.type(screen.getByLabelText('Role'), 'Dev')
    await userEvent.selectOptions(screen.getByLabelText('Platform'), 'Pnet')
    await userEvent.click(screen.getByRole('button', { name: 'Add application' }))

    expect(await screen.findByText("Applied date can't be in the future.")).toBeInTheDocument()
    expect(screen.getByLabelText('Applied on')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Company')).toHaveValue('Acme') // not reset on failure
  })

  it('in edit mode, starts with the current values and can be cancelled', async () => {
    const onCancel = vi.fn()
    render(
      <ApplicationForm
        initial={makeApplication({ company: 'Globex', notes: 'Call back Friday' })}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    )

    expect(screen.getByLabelText('Company')).toHaveValue('Globex')
    expect(screen.getByLabelText('Notes (optional)')).toHaveValue('Call back Friday')
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(onCancel).toHaveBeenCalled()
  })
})
