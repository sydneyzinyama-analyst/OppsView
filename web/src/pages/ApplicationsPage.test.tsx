import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import * as applicationsApi from '../api/applications'
import * as emailApi from '../api/email'
import { makeApplication } from '../test/fixtures'
import { ApplicationsPage } from './ApplicationsPage'

const list = [
  makeApplication({ id: 'a1', company: 'Acme', role: 'Junior Developer', status: 'Applied' }),
  makeApplication({ id: 'a2', company: 'Globex', role: 'Data Analyst', status: 'Interview', allowedNextStatuses: ['Offer', 'Rejected'] }),
  makeApplication({ id: 'a3', company: 'Initech', role: 'Backend Developer', status: 'Rejected', allowedNextStatuses: [] }),
]

function renderPage() {
  render(
    <MemoryRouter>
      <ApplicationsPage />
    </MemoryRouter>,
  )
}

const companies = () =>
  within(screen.getByRole('list')).queryAllByText(/^(Acme|Globex|Initech|Umbrella)$/).map((el) => el.textContent)

describe('ApplicationsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(applicationsApi, 'listApplications').mockResolvedValue(list)
    vi.spyOn(emailApi, 'getEmailStatus').mockRejectedValue(new Error('not set up'))
    vi.spyOn(emailApi, 'listReviewItems').mockResolvedValue([])
  })

  it('shows your numbers', async () => {
    renderPage()
    const numbers = await screen.findByRole('region', { name: 'Your numbers' })
    expect(within(numbers).getByText('Applications').parentElement).toHaveTextContent('3')
    expect(within(numbers).getByText('Interviews').parentElement).toHaveTextContent('1')
  })

  it('filters by status', async () => {
    renderPage()
    await screen.findByText('Globex')
    await userEvent.click(screen.getByRole('button', { name: /^Interview/ }))
    expect(companies()).toEqual(['Globex'])
    expect(screen.getByRole('button', { name: /^Interview/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('searches by company or role', async () => {
    renderPage()
    await screen.findByText('Acme')
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search applications' }), 'backend')
    expect(companies()).toEqual(['Initech'])
    await userEvent.clear(screen.getByRole('searchbox', { name: 'Search applications' }))
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search applications' }), 'nothing like this')
    expect(screen.getByText(/No applications match/)).toBeInTheDocument()
  })

  it('adds an application from the pop-up window', async () => {
    const created = makeApplication({ id: 'a4', company: 'Umbrella', role: 'QA Engineer', appliedOn: '2026-11-16' })
    const create = vi.spyOn(applicationsApi, 'createApplication').mockResolvedValue(created)
    renderPage()
    await screen.findByText('Acme')

    await userEvent.click(screen.getByRole('button', { name: 'New application' }))
    const dialog = screen.getByRole('dialog', { name: 'New application' })
    expect(within(dialog).getByLabelText('Company')).toHaveFocus()

    await userEvent.type(within(dialog).getByLabelText('Company'), 'Umbrella')
    await userEvent.type(within(dialog).getByLabelText('Role'), 'QA Engineer')
    await userEvent.selectOptions(within(dialog).getByLabelText('Platform'), 'LinkedIn')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add application' }))

    expect(create).toHaveBeenCalledWith(expect.objectContaining({ company: 'Umbrella', platform: 'LinkedIn' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByText('Umbrella')).toBeInTheDocument()
  })

  it('closes the pop-up window with Escape', async () => {
    renderPage()
    await screen.findByText('Acme')
    await userEvent.click(screen.getByRole('button', { name: 'New application' }))
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New application' })).toHaveFocus()
  })
})
