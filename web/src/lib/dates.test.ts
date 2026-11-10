import { describe, expect, it } from 'vitest'
import { formatDate, localToday } from './dates'

describe('localToday', () => {
  it('uses the local calendar date, zero-padded', () => {
    expect(localToday(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05')
  })

  it('does not shift to the UTC date just after local midnight', () => {
    // 00:30 local on 10 Nov; in any zone ahead of UTC, toISOString() says 9 Nov.
    expect(localToday(new Date(2026, 10, 10, 0, 30))).toBe('2026-11-10')
  })
})

describe('formatDate', () => {
  it('formats a yyyy-MM-dd date without moving it a day', () => {
    expect(formatDate('2026-11-10')).toContain('10')
    expect(formatDate('2026-11-10')).toContain('2026')
  })
})
