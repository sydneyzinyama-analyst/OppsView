import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as applicationsApi from '../api/applications'
import * as matchingApi from '../api/matching'
import type { MatchResult } from '../api/types'
import { makeApplication } from '../test/fixtures'
import { MatchPage } from './MatchPage'

const cv = { fileName: 'cv.pdf', uploadedAt: '2026-11-15T10:00:00Z', characters: 500, preview: '…' }
const result: MatchResult = {
  score: 60,
  required: { matched: ['C#'], missing: ['Docker'] },
  niceToHave: { matched: [], missing: [] },
  extraInCv: [],
  summary: '60% match: you have 1 of 2 required skills.',
  applicationId: 'a1',
  matchedAt: '2026-11-15T10:00:00Z',
}

function renderPage(url = '/match') {
  vi.spyOn(applicationsApi, 'listApplications').mockResolvedValue([makeApplication({ id: 'a1', company: 'Acme', role: 'Dev' })])
  render(
    <MemoryRouter initialEntries={[url]}>
      <MatchPage />
    </MemoryRouter>,
  )
}

describe('MatchPage', () => {
  it('asks for a CV before matching', async () => {
    vi.spyOn(matchingApi, 'getCv').mockResolvedValue(null)
    renderPage()

    expect(await screen.findByText('Upload your CV above first.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Match my CV' })).toBeDisabled()
  })

  it('matches the pasted ad, saving it to the application from the link', async () => {
    vi.spyOn(matchingApi, 'getCv').mockResolvedValue(cv)
    const match = vi.spyOn(matchingApi, 'matchCv').mockResolvedValue(result)
    renderPage('/match?applicationId=a1')

    await screen.findByText('cv.pdf')
    expect(await screen.findByRole('option', { name: 'Acme – Dev' })).toBeInTheDocument()
    expect(screen.getByLabelText(/Save the result/)).toHaveValue('a1')

    await userEvent.type(screen.getByLabelText('Job ad'), 'Requirements: C#, Docker')
    await userEvent.click(screen.getByRole('button', { name: 'Match my CV' }))

    expect(match).toHaveBeenCalledWith('Requirements: C#, Docker', 'a1')
    expect(await screen.findByText('60%')).toBeInTheDocument()
    expect(screen.getByText(/saved to Acme – Dev/)).toBeInTheDocument()
  })

  it('asks for the ad if it is empty', async () => {
    vi.spyOn(matchingApi, 'getCv').mockResolvedValue(cv)
    const match = vi.spyOn(matchingApi, 'matchCv')
    renderPage()

    await screen.findByText('cv.pdf')
    await userEvent.click(screen.getByRole('button', { name: 'Match my CV' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Paste the job ad first.')
    expect(match).not.toHaveBeenCalled()
  })

  it('explains when the matcher is not running', async () => {
    vi.spyOn(matchingApi, 'getCv').mockResolvedValue(cv)
    const { ApiError } = await import('../api/client')
    vi.spyOn(matchingApi, 'matchCv').mockRejectedValue(new ApiError(503, "The CV matcher isn't running."))
    renderPage()

    await screen.findByText('cv.pdf')
    await userEvent.type(screen.getByLabelText('Job ad'), 'C#')
    await userEvent.click(screen.getByRole('button', { name: 'Match my CV' }))

    expect(await screen.findByRole('alert')).toHaveTextContent("isn't running")
  })
})
