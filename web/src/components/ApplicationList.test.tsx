import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import * as api from '../api/applications'
import { makeApplication } from '../test/fixtures'
import { ApplicationList } from './ApplicationList'

function renderList(overrides: Partial<Parameters<typeof ApplicationList>[0]> = {}) {
  const props = {
    applications: [makeApplication(), makeApplication({ id: 'a2', company: 'Globex', jobUrl: 'https://example.com/job' })],
    onUpdate: vi.fn().mockResolvedValue(undefined),
    onDelete: vi.fn().mockResolvedValue(undefined),
    onChangeStatus: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  }
  render(<ApplicationList {...props} />)
  return props
}

describe('ApplicationList', () => {
  it('shows an empty state', () => {
    renderList({ applications: [] })
    expect(screen.getByText(/No applications yet/)).toBeInTheDocument()
  })

  it('renders a row per application and links to the job ad', () => {
    renderList()
    expect(screen.getByText('Acme')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Globex' })).toHaveAttribute('href', 'https://example.com/job')
  })

  it('deletes only after confirmation', async () => {
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true)
    const { onDelete } = renderList()

    await userEvent.click(screen.getByRole('button', { name: 'Delete Acme' }))
    expect(onDelete).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Delete Acme' }))
    expect(onDelete).toHaveBeenCalledWith('a1')
    expect(confirm).toHaveBeenCalledTimes(2)
  })

  it('changes status from the dropdown', async () => {
    const { onChangeStatus } = renderList()
    await userEvent.selectOptions(screen.getByLabelText('Status for Globex'), 'Rejected')
    expect(onChangeStatus).toHaveBeenCalledWith('a2', 'Rejected')
  })

  it('opens an edit form and saves through onUpdate', async () => {
    const { onUpdate } = renderList()
    await userEvent.click(screen.getAllByRole('button', { name: 'Edit' })[0])

    const role = screen.getByLabelText('Role')
    await userEvent.clear(role)
    await userEvent.type(role, 'Senior Developer')
    await userEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(onUpdate).toHaveBeenCalledWith('a1', expect.objectContaining({ role: 'Senior Developer' }))
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument()
  })

  it('loads and shows the status history', async () => {
    vi.spyOn(api, 'getHistory').mockResolvedValue([
      { fromStatus: 'Applied', toStatus: 'Interview', changedAt: '2026-11-10T09:00:00Z' },
    ])
    renderList()

    await userEvent.click(screen.getAllByRole('button', { name: 'History' })[0])

    const timeline = await screen.findByRole('list', { name: 'Status history for Acme' })
    expect(timeline).toHaveTextContent('Interview')
    expect(timeline).toHaveTextContent('from Applied')
    expect(api.getHistory).toHaveBeenCalledWith('a1')
  })
})
