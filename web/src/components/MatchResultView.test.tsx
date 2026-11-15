import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { MatchResult } from '../api/types'
import { MatchResultView } from './MatchResultView'

const result: MatchResult = {
  score: 76,
  required: { matched: ['C#', 'Git'], missing: ['SQL Server'] },
  niceToHave: { matched: ['Angular or React'], missing: ['Docker'] },
  extraInCv: ['Python'],
  summary: '76% match: you have 2 of 3 required skills and 1 of 2 nice-to-have skills.',
  applicationId: null,
  matchedAt: '2026-11-15T10:00:00Z',
}

describe('MatchResultView', () => {
  it('shows the score, summary and skills marked as had or missing', () => {
    render(<MatchResultView result={result} />)

    expect(screen.getByText('76%')).toHaveClass('score-high')
    expect(screen.getByText(/2 of 3 required/)).toBeInTheDocument()
    expect(screen.getByText('SQL Server').closest('li')).toHaveTextContent('(missing)')
    expect(screen.getByText('C#').closest('li')).toHaveTextContent('(you have this)')
    expect(screen.getByText('Python')).toBeInTheDocument()
  })

  it('shows a dash when the ad had no known skills', () => {
    render(
      <MatchResultView
        result={{ ...result, score: null, required: { matched: [], missing: [] }, niceToHave: { matched: [], missing: [] } }}
      />,
    )
    expect(screen.getByText('–')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Required/ })).not.toBeInTheDocument()
  })
})
