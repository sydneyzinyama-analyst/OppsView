import type { ApplicationStatus } from '../api/types'

export function StatusBadge({ status, label }: { status: ApplicationStatus; label?: string }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{label ?? status}</span>
}
