import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import * as emailApi from '../api/email'
import type { ReviewItem } from '../api/types'
import { makeApplication } from '../test/fixtures'
import { ReviewList } from './ReviewList'

const rejection: ReviewItem = {
  id: 'r1',
  receivedAt: '2026-11-14T10:00:00Z',
  sender: 'careers@cartrack.co.za',
  subject: 'Your application',
  kind: 'Rejection',
  platform: 'CompanyPortal',
  company: 'Cartrack',
  role: 'IT Support Technician',
  reason: "Looks like a rejected email from Cartrack, but there's no application to them in OppsView.",
  applicationId: null,
}

describe('ReviewList', () => {
  it('shows why each email needs review', () => {
    render(<ReviewList items={[rejection]} onResolved={vi.fn()} />)
    expect(screen.getByText('Your application')).toBeInTheDocument()
    expect(screen.getByText(/no application to them/)).toBeInTheDocument()
    expect(screen.getByText(/Rejection/)).toBeInTheDocument()
  })

  it('adds an application with the details found in the email filled in', async () => {
    const create = vi.spyOn(emailApi, 'createFromReviewItem').mockResolvedValue(makeApplication())
    const onResolved = vi.fn()
    render(<ReviewList items={[rejection]} onResolved={onResolved} />)

    await userEvent.click(screen.getByRole('button', { name: 'Add as application' }))
    expect(screen.getByLabelText('Company')).toHaveValue('Cartrack')
    expect(screen.getByLabelText('Role')).toHaveValue('IT Support Technician')
    await userEvent.click(screen.getByRole('button', { name: 'Add application' }))

    expect(create).toHaveBeenCalledWith('r1', expect.objectContaining({ company: 'Cartrack', platform: 'CompanyPortal' }))
    expect(onResolved).toHaveBeenCalled()
  })

  it('ignores an email', async () => {
    const dismiss = vi.spyOn(emailApi, 'dismissReviewItem').mockResolvedValue(undefined)
    const onResolved = vi.fn()
    render(<ReviewList items={[rejection]} onResolved={onResolved} />)

    await userEvent.click(screen.getByRole('button', { name: 'Ignore' }))

    expect(dismiss).toHaveBeenCalledWith('r1')
    expect(onResolved).toHaveBeenCalled()
  })

  it('does not offer "Add" for an email already linked to an application', () => {
    render(<ReviewList items={[{ ...rejection, applicationId: 'a1' }]} onResolved={vi.fn()} />)
    expect(screen.queryByRole('button', { name: 'Add as application' })).not.toBeInTheDocument()
  })
})
