import { describe, expect, it } from 'vitest'
import { describeSync } from './email'

const none = { created: 0, updated: 0, noChange: 0, ignored: 0, needsReview: 0, alreadyProcessed: 0, failed: 0 }

describe('describeSync', () => {
  it('summarises what changed', () => {
    expect(describeSync({ ...none, created: 2, updated: 1, needsReview: 3 })).toBe(
      'Added 2 applications, updated 1. 3 emails need your review.',
    )
  })

  it('uses singular forms', () => {
    expect(describeSync({ ...none, created: 1, needsReview: 1 })).toBe('Added 1 application. 1 email needs your review.')
  })

  it('mentions emails that will be retried', () => {
    expect(describeSync({ ...none, created: 1, failed: 2 })).toBe(
      "Added 1 application. 2 emails couldn't be read from Gmail right now and will be tried again next sync.",
    )
  })

  it('says when there was nothing new', () => {
    expect(describeSync({ ...none, alreadyProcessed: 4 })).toBe('No new application emails.')
  })
})
