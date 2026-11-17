import { describe, expect, it } from 'vitest'
import { AVATAR_TINTS, avatarTint, initials } from './avatar'
import { timeAgo } from './dates'

describe('initials', () => {
  it('takes the first letters of the first two words', () => {
    expect(initials('Standard Bank')).toBe('SB')
    expect(initials('Dimension Data (NTT)')).toBe('DD')
  })
  it('takes two letters of a single word', () => expect(initials('Takealot')).toBe('TA'))
  it('copes with an empty name', () => expect(initials('  ')).toBe('?'))
})

describe('avatarTint', () => {
  it('is stable for a company and within range', () => {
    expect(avatarTint('Yoco')).toBe(avatarTint(' yoco '))
    for (const name of ['Acme', 'Globex', 'Initech', 'Umbrella']) {
      expect(avatarTint(name)).toBeGreaterThanOrEqual(0)
      expect(avatarTint(name)).toBeLessThan(AVATAR_TINTS)
    }
  })
})

describe('timeAgo', () => {
  const now = new Date(2026, 10, 17, 9, 30)
  it.each([
    ['2026-11-17', 'today'],
    ['2026-11-16', 'yesterday'],
    ['2026-11-10', '7 days ago'],
    ['2026-10-20', '4 weeks ago'],
    ['2026-07-17', '4 months ago'],
    ['2025-06-01', '1 year ago'],
  ])('%s -> %s', (date, expected) => expect(timeAgo(date, now)).toBe(expected))
})
