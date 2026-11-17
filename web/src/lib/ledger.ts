import type { Application, ApplicationStatus } from '../api/types'

/**
 * The words on the ledger page: the headline, the summary sentence and the
 * notes "in the margins", all worked out from the applications, so they are
 * plain functions that are easy to test.
 */

/** After this many days without news, an application counts as "quiet". */
export const QUIET_AFTER_DAYS = 7

const WORDS = ['No', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve']

/** 5 -> "Five"; numbers above twelve stay as digits. */
export function numberWord(n: number): string {
  return WORDS[n] ?? String(n)
}

const plural = (n: number, word: string) => `${word}${n === 1 ? '' : 's'}`

export interface Tally {
  sent: number
  awaiting: number
  interviews: number
  offers: number
  rejected: number
}

export function tally(list: Application[]): Tally {
  const by = (s: ApplicationStatus) => list.filter((a) => a.status === s).length
  return {
    sent: list.length,
    awaiting: by('Applied') + by('Viewed'),
    interviews: by('Interview'),
    offers: by('Offer'),
    rejected: by('Rejected'),
  }
}

/** Two short lines: what went out, and the best news that came back. */
export function headline(t: Tally): [string, string] {
  if (t.sent === 0) return ['A fresh ledger.', 'Your first letter awaits.']
  const first = `${numberWord(t.sent)} ${plural(t.sent, 'letter')} out.`
  if (t.offers > 0) return [first, `${numberWord(t.offers)} ${plural(t.offers, 'offer')} in.`]
  if (t.interviews > 0) return [first, `${numberWord(t.interviews)} ${plural(t.interviews, 'interview')} lined up.`]
  if (t.awaiting > 0) return [first, `${numberWord(t.awaiting)} awaiting word.`]
  return [first, 'Keep them coming.']
}

/** Whole days between a yyyy-MM-dd (or ISO timestamp) date and today. */
export function daysSince(date: string, now: Date = new Date()): number {
  const [y, m, d] = date.slice(0, 10).split('-').map(Number)
  const then = new Date(y, m - 1, d).getTime()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  return Math.max(0, Math.round((today - then) / 86_400_000))
}

/** Days without news, for an application still waiting on a reply; otherwise null. */
export function quietDays(a: Application, now: Date = new Date()): number | null {
  if (a.status !== 'Applied' && a.status !== 'Viewed') return null
  const days = daysSince(a.status === 'Applied' ? a.appliedOn : a.updatedAt, now)
  return days >= QUIET_AFTER_DAYS ? days : null
}

/** Up to three short sentences about what needs attention. */
export function summary(list: Application[], now: Date = new Date()): string {
  const parts: string[] = []
  const offer = list.find((a) => a.status === 'Offer')
  if (offer) parts.push(`${offer.company} made you an offer.`)
  const interview = list.find((a) => a.status === 'Interview')
  if (interview) parts.push(`${interview.company} wants to meet you.`)
  const quiet = mostQuiet(list, now)
  if (quiet) parts.push(`${quiet.app.company} has gone quiet for ${quiet.days} days.`)
  return parts.length > 0 ? parts.join(' ') : 'Every email is an opportunity.'
}

function mostQuiet(list: Application[], now: Date) {
  return list
    .map((app) => ({ app, days: quietDays(app, now) }))
    .filter((q): q is { app: Application; days: number } => q.days !== null)
    .sort((a, b) => b.days - a.days)[0]
}

export interface MarginNote {
  key: string
  kicker: string
  title: string
  body: string
  /** Where the ↗ goes. */
  to: string
  tone: 'urgent' | 'plain' | 'quiet'
}

const shortDate = (date: string) => {
  const [y, m, d] = date.slice(0, 10).split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' }).toUpperCase()
}

/** The notes in the right-hand column: offers first, then interviews, then the quietest applications. */
export function marginNotes(list: Application[], now: Date = new Date(), max = 3): MarginNote[] {
  const notes: MarginNote[] = []
  for (const a of list.filter((x) => x.status === 'Offer'))
    notes.push({
      key: `offer-${a.id}`,
      kicker: `${shortDate(a.updatedAt)} · offer`,
      title: `Answer ${a.company}'s offer.`,
      body: 'Read the letter and compare it with your other applications.',
      to: `/?highlight=${a.id}`,
      tone: 'urgent',
    })
  for (const a of list.filter((x) => x.status === 'Interview'))
    notes.push({
      key: `interview-${a.id}`,
      kicker: `${shortDate(a.updatedAt)} · interview`,
      title: `${a.company} interview.`,
      body: `Prepare for the ${a.role} role: match your CV to it and see what to brush up on.`,
      to: `/match?applicationId=${a.id}`,
      tone: 'plain',
    })
  const quiet = list
    .map((app) => ({ app, days: quietDays(app, now) }))
    .filter((q): q is { app: Application; days: number } => q.days !== null)
    .sort((a, b) => b.days - a.days)
  for (const { app, days } of quiet)
    notes.push({
      key: `quiet-${app.id}`,
      kicker: `${days} days quiet`,
      title: `A polite nudge to ${app.company}.`,
      body: 'A short follow-up email often gets a reply.',
      to: `/?highlight=${app.id}`,
      tone: 'quiet',
    })
  return notes.slice(0, max)
}

/** How far along the pipeline an application got, out of four steps (Applied, Viewed, Interview, Offer). */
export function progressSteps(status: ApplicationStatus): number {
  return { Applied: 1, Viewed: 2, Interview: 3, Offer: 4, Rejected: 1 }[status]
}

/** The stamp on each row: ledger words for the statuses. */
export const STAMP_LABELS: Record<ApplicationStatus, string> = {
  Applied: 'Sent',
  Viewed: 'Viewed',
  Interview: 'Interview',
  Offer: 'Offer',
  Rejected: 'Declined',
}

/** Entry numbers in the order the applications were added: the first is 1. */
export function entryNumbers(list: Application[]): Map<string, number> {
  const sorted = [...list].sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))
  return new Map(sorted.map((a, i) => [a.id, i + 1]))
}

/** "TUESDAY 17 NOVEMBER · WEEK 47" (ISO week number). */
export function dateline(now: Date = new Date()): string {
  const day = now.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' }).replace(',', '')
  return `${day} · Week ${isoWeek(now)}`.toUpperCase()
}

export function isoWeek(date: Date): number {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()))
  const dayNum = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() + 4 - dayNum)
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1))
  return Math.ceil(((d.getTime() - yearStart.getTime()) / 86_400_000 + 1) / 7)
}
