import { describe, expect, it } from 'vitest'
import { validateApplication, type FormState } from './validation'

const valid: FormState = {
  company: 'Acme',
  role: 'Developer',
  platform: 'Indeed',
  appliedOn: '2026-11-10',
  jobUrl: '',
  notes: '',
}

describe('validateApplication', () => {
  it('accepts a valid form', () => {
    expect(validateApplication(valid, '2026-11-10')).toEqual({})
  })

  it('requires company, role, platform and date', () => {
    const errors = validateApplication({ ...valid, company: ' ', role: '', platform: '', appliedOn: '' }, '2026-11-10')
    expect(Object.keys(errors).sort()).toEqual(['appliedOn', 'company', 'platform', 'role'])
  })

  it('rejects future dates', () => {
    expect(validateApplication({ ...valid, appliedOn: '2026-11-11' }, '2026-11-10').appliedOn).toMatch(/future/)
  })

  it('only accepts http(s) job links', () => {
    expect(validateApplication({ ...valid, jobUrl: 'pnet.co.za/job' }, '2026-11-10').jobUrl).toBeDefined()
    expect(validateApplication({ ...valid, jobUrl: 'https://pnet.co.za/job' }, '2026-11-10').jobUrl).toBeUndefined()
  })
})
