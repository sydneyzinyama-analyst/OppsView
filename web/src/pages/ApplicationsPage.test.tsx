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
  makeApplication({ id: 'a2', company: 'Globex', role: 'Data Analyst', status: 'Offer', allowedNextStatuses: [] }),
  makeApplication({ id: 'a3', company: 'Initech', role: 'Backend Developer', status: 'Rejected', allowedNextStatuses: [] }),
]

function renderPage(url = '/') {
  render(
    <MemoryRouter initialEntries={[url]}>
      <ApplicationsPage />
    </MemoryRouter>,
  )
}

const companies = () =>
  screen
    .queryAllByRole('row')
    .slice(1)
    .map((row) => within(row).queryByText(/^(Acme|Globex|Initech|Umbrella)$/)?.textContent)

describe('ApplicationsPage (the ledger)', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.spyOn(applicationsApi, 'listApplications').mockResolvedValue(list)
    vi.spyOn(emailApi, 'getEmailStatus').mockRejectedValue(new Error('not set up'))
    vi.spyOn(emailApi, 'listReviewItems').mockResolvedValue([])
  })

  it('writes the headline and the numbers', async () => {
    renderPage()
    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Three letters out.One offer in.')
    const numbers = screen.getByRole('region', { name: 'Your numbers' })
    expect(within(numbers).getByText('Sent').previousSibling).toHaveTextContent('3')
    expect(within(numbers).getByText(/33% of sent/)).toBeInTheDocument()
  })

  it('notes the offer in the margins', async () => {
    renderPage()
    const margins = await screen.findByRole('complementary', { name: 'In the margins' })
    expect(await within(margins).findByText("Answer Globex's offer.")).toBeInTheDocument()
  })

  it('filters open and closed applications', async () => {
    renderPage()
    await screen.findByText('Globex')
    await userEvent.click(screen.getByRole('button', { name: 'Closed 1' }))
    expect(companies()).toEqual(['Initech'])
    await userEvent.click(screen.getByRole('button', { name: 'Open 2' }))
    expect(companies()).toEqual(['Acme', 'Globex'])
  })

  it('searches from ?q= in the address', async () => {
    renderPage('/?q=backend')
    await screen.findByText('Initech')
    expect(companies()).toEqual(['Initech'])
    expect(screen.getByText(/Showing matches for “backend”/)).toBeInTheDocument()
  })

  it('says when nothing matches', async () => {
    renderPage('/?q=nothing%20like%20this')
    expect(await screen.findByText(/No applications match/)).toBeInTheDocument()
  })

  it('opens "New entry" from ?new=1 and adds the application', async () => {
    const created = makeApplication({ id: 'a4', company: 'Umbrella', role: 'QA Engineer', appliedOn: '2026-11-16' })
    const create = vi.spyOn(applicationsApi, 'createApplication').mockResolvedValue(created)
    renderPage('/?new=1')

    const dialog = await screen.findByRole('dialog', { name: 'New entry' })
    await userEvent.type(within(dialog).getByLabelText('Company'), 'Umbrella')
    await userEvent.type(within(dialog).getByLabelText('Role'), 'QA Engineer')
    await userEvent.selectOptions(within(dialog).getByLabelText('Platform'), 'LinkedIn')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add application' }))

    expect(create).toHaveBeenCalledWith(expect.objectContaining({ company: 'Umbrella', platform: 'LinkedIn' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByText('Umbrella')).toBeInTheDocument()
  })

  it('closes the pop-up window with Escape', async () => {
    renderPage('/?new=1')
    await screen.findByRole('dialog', { name: 'New entry' })
    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
