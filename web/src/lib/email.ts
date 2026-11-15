import type { EmailKind, ImportSummary } from '../api/types'

/** One line describing what a sync did, e.g. "Added 2 applications, updated 1. 3 emails need your review." */
export function describeSync(s: ImportSummary): string {
  const parts: string[] = []
  if (s.created) parts.push(`added ${s.created} application${s.created === 1 ? '' : 's'}`)
  if (s.updated) parts.push(`updated ${s.updated}`)
  let text = parts.length ? capitalize(parts.join(', ')) + '.' : 'No new application emails.'
  if (s.needsReview) text += ` ${s.needsReview} email${s.needsReview === 1 ? ' needs' : 's need'} your review.`
  if (s.failed)
    text += ` ${s.failed} email${s.failed === 1 ? '' : 's'} couldn't be read from Gmail right now and will be tried again next sync.`
  return text
}

/** Messages for the ?gmail=... result Google's redirect brings back. */
export const CONNECT_RESULTS: Record<string, { ok: boolean; text: string }> = {
  connected: { ok: true, text: 'Gmail connected. Click "Sync now" to import your job emails.' },
  denied: { ok: false, text: "Gmail wasn't connected: access was declined on Google's screen." },
  scope: { ok: false, text: 'Gmail wasn\'t connected: OppsView needs the "read your email" permission. Please try again and leave it ticked.' },
  expired: { ok: false, text: 'That connection attempt expired. Please try again.' },
  error: { ok: false, text: "Something went wrong connecting Gmail. Please try again." },
}

export const KIND_LABELS: Record<EmailKind, string> = {
  ApplicationReceived: 'Application confirmation',
  ApplicationViewed: 'Application viewed',
  InterviewInvite: 'Interview invitation',
  Offer: 'Offer',
  Rejection: 'Rejection',
  NotRelevant: 'Not about an application',
  Unknown: 'Unclear',
}

function capitalize(s: string) {
  return s.charAt(0).toUpperCase() + s.slice(1)
}
