import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as analyticsApi from '../api/analytics'
import type { Analytics } from '../api/analytics'
import { InsightsPage } from './InsightsPage'

const data: Analytics = {
  totals: { applications: 20, active: 9, interviews: 4, offers: 1, rejected: 6, responseRate: 0.55, averageDaysToResponse: 9.5 },
  perWeek: [
    { weekStart: '2026-11-02', applications: 3 },
    { weekStart: '2026-11-09', applications: 7 },
    { weekStart: '2026-11-16', applications: 2 },
  ],
  byPlatform: [
    { platform: 'Pnet', applications: 10, responses: 2, responseRate: 0.2 },
    { platform: 'LinkedIn', applications: 6, responses: 3, responseRate: 0.5 },
  ],
  funnel: [
    { stage: 'Applied', reached: 20 },
    { stage: 'Viewed', reached: 8 },
    { stage: 'Interview', reached: 4 },
    { stage: 'Offer', reached: 1 },
  ],
  rejectedAt: [{ from: 'Applied', count: 4 }, { from: 'Interview', count: 2 }],
  waiting: [
    { stage: 'Applied', count: 6, averageDays: 12.3, waitingOver14Days: 2 },
    { stage: 'Viewed', count: 1, averageDays: 3, waitingOver14Days: 0 },
    { stage: 'Interview', count: 2, averageDays: 5, waitingOver14Days: 0 },
  ],
  topMissingSkills: [{ skill: 'Docker', count: 5 }, { skill: 'Azure', count: 3 }],
  sources: { manual: 8, email: 12 },
}

function renderPage() {
  render(
    <MemoryRouter>
      <InsightsPage />
    </MemoryRouter>,
  )
}

describe('InsightsPage', () => {
  it('shows the headline numbers', async () => {
    vi.spyOn(analyticsApi, 'getAnalytics').mockResolvedValue(data)
    renderPage()

    const summary = await screen.findByRole('region', { name: 'Summary' })
    expect(within(summary).getByText('20')).toBeInTheDocument()
    expect(within(summary).getByText('55%')).toBeInTheDocument()
    expect(within(summary).getByText('9.5')).toBeInTheDocument()
  })

  it('draws each chart with an accessible table of its values', async () => {
    vi.spyOn(analyticsApi, 'getAnalytics').mockResolvedValue(data)
    renderPage()

    const platform = (await screen.findByText('Response rate by platform')).closest('figure')!
    const table = within(platform).getByRole('table', { hidden: true })
    expect(within(table).getByText('Pnet')).toBeInTheDocument()
    expect(within(table).getByText('20% (2 of 10 replied)')).toBeInTheDocument()

    const skills = screen.getByText(/Skills most often missing/).closest('figure')!
    expect(within(skills).getAllByText('Docker').length).toBeGreaterThan(0)

    expect(screen.getAllByText('After applying')).toHaveLength(2) // on the chart, and in its table
    expect(screen.getByText(/filled in 60% of your applications from email/)).toBeInTheDocument()
  })

  it('labels only the highest and the latest week', async () => {
    vi.spyOn(analyticsApi, 'getAnalytics').mockResolvedValue(data)
    renderPage()

    const weekly = (await screen.findByText(/Applications per week/)).closest('figure')!
    const labels = [...weekly.querySelectorAll('.column-value')].map((e) => e.textContent)
    expect(labels).toEqual(['', '7', '2'])
  })

  it('invites a new user to add applications', async () => {
    vi.spyOn(analyticsApi, 'getAnalytics').mockResolvedValue({
      ...data,
      totals: { ...data.totals, applications: 0 },
    })
    renderPage()
    expect(await screen.findByText(/No applications yet/)).toBeInTheDocument()
  })

  it('shows errors', async () => {
    vi.spyOn(analyticsApi, 'getAnalytics').mockRejectedValue(new Error("Can't reach the OppsView API."))
    renderPage()
    expect(await screen.findByRole('alert')).toHaveTextContent("Can't reach")
  })
})
