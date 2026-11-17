/**
 * Today's date in the user's time zone, as yyyy-MM-dd.
 * (new Date().toISOString() would give the UTC date, which is "yesterday"
 * for someone in Johannesburg just after midnight.)
 */
export function localToday(now: Date = new Date()): string {
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const d = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/** "2026-11-10" -> "10 Nov 2026", without time-zone shifts. */
export function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

export function formatDateTime(isoTimestamp: string): string {
  return new Date(isoTimestamp).toLocaleString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** "2026-11-10" -> "today", "yesterday", "5 days ago", "3 weeks ago", "2 months ago". */
export function timeAgo(isoDate: string, now: Date = new Date()): string {
  const [y, m, d] = isoDate.split('-').map(Number)
  const then = new Date(y, m - 1, d)
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const days = Math.round((today.getTime() - then.getTime()) / 86_400_000)
  if (days <= 0) return 'today'
  if (days === 1) return 'yesterday'
  if (days < 14) return `${days} days ago`
  if (days < 60) return `${Math.floor(days / 7)} weeks ago`
  const months = Math.floor(days / 30)
  return months < 12 ? `${months} months ago` : `${Math.floor(days / 365)} year${days < 730 ? '' : 's'} ago`
}
