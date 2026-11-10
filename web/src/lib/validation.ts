import type { FieldErrors } from '../api/client'
import type { JobPlatform } from '../api/types'
import { localToday } from './dates'

/** What the application form edits: strings throughout, as inputs produce them. */
export interface FormState {
  company: string
  role: string
  platform: JobPlatform | ''
  appliedOn: string
  jobUrl: string
  notes: string
}

/**
 * Mirrors the server's rules for instant feedback. The server is still the
 * authority: its field errors are shown in the same places if anything slips
 * through.
 */
export function validateApplication(form: FormState, today: string = localToday()): FieldErrors {
  const errors: FieldErrors = {}
  if (!form.company.trim()) errors.company = 'Company is required.'
  if (!form.role.trim()) errors.role = 'Role is required.'
  if (!form.platform) errors.platform = 'Choose where you applied.'
  if (!form.appliedOn) errors.appliedOn = 'Applied date is required.'
  else if (form.appliedOn > today) errors.appliedOn = "Applied date can't be in the future."
  if (form.jobUrl.trim() && !/^https?:\/\/\S+$/i.test(form.jobUrl.trim()))
    errors.jobUrl = 'Job URL must start with http:// or https://'
  return errors
}

