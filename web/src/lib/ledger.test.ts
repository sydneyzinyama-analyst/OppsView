import { describe, expect, it } from 'vitest'
import { makeApplication } from '../test/fixtures'
import { dateline, entryNumbers, headline, isoWeek, marginNotes, numberWord, quietDays, summary, tally } from './ledger'

const now = new Date(2026, 10, 17, 9, 0) // Tuesday 17 November 2026

const list = [
  makeApplication({ id: 'a1', company: 'Entelect', status: 'Offer', updatedAt: '2026-11-16T10:00:00Z', createdAt: '2026-10-20T08:00:00Z' }),
  makeApplication({ id: 'a2', company: 'Takealot', status: 'Interview', updatedAt: '2026-11-15T10:00:00Z', createdAt: '2026-11-14T08:00:00Z' }),
  makeApplication({ id: 'a3', company: 'Discovery', status: 'Applied', appliedOn: '2026-11-07', createdAt: '2026-11-07T08:00:00Z' }),
  makeApplication({ id: 'a4', company: 'Super Group', status: 'Applied', appliedOn: '2026-11-12', createdAt: '2026-11-12T08:00:00Z' }),
  makeApplication({ id: 'a5', company: 'BBD', status: 'Rejected', appliedOn: '2026-10-27', createdAt: '2026-10-27T08:00:00Z' }),
]

describe('ledger words', () => {
  it('spells small numbers', () => {
    expect(numberWord(5)).toBe('Five')
    expect(numberWord(13)).toBe('13')
  })

  it('writes the headline from the best news', () => {
    expect(headline(tally(list))).toEqual(['Five letters out.', 'One offer in.'])
    expect(headline(tally(list.filter((a) => a.status !== 'Offer')))).toEqual(['Four letters out.', 'One interview lined up.'])
    expect(headline(tally([list[2]]))).toEqual(['One letter out.', 'One awaiting word.'])
    expect(headline(tally([]))).toEqual(['A fresh ledger.', 'Your first letter awaits.'])
  })

  it('counts only waiting applications as quiet, after a week', () => {
    expect(quietDays(list[2], now)).toBe(10)
    expect(quietDays(list[3], now)).toBeNull() // 5 days: not quiet yet
    expect(quietDays(list[4], now)).toBeNull() // rejected
  })

  it('summarises what needs attention', () => {
    expect(summary(list, now)).toBe(
      'Entelect made you an offer. Takealot wants to meet you. Discovery has gone quiet for 10 days.',
    )
    expect(summary([], now)).toBe('Every email is an opportunity.')
  })

  it('puts offers, then interviews, then quiet applications in the margins', () => {
    const notes = marginNotes(list, now)
    expect(notes.map((n) => n.title)).toEqual([
      "Answer Entelect's offer.",
      'Takealot interview.',
      'A polite nudge to Discovery.',
    ])
    expect(notes[0].tone).toBe('urgent')
    expect(notes[1].to).toBe('/match?applicationId=a2')
    expect(notes[2].kicker).toBe('10 days quiet')
  })

  it('numbers entries in the order they were added', () => {
    const numbers = entryNumbers(list)
    expect(numbers.get('a1')).toBe(1)
    expect(numbers.get('a2')).toBe(5)
    expect(numbers.get('a5')).toBe(2)
  })

  it('writes the dateline with the ISO week', () => {
    expect(isoWeek(new Date(2026, 0, 1))).toBe(1)
    expect(dateline(now)).toBe('TUESDAY 17 NOVEMBER · WEEK 47')
  })
})
