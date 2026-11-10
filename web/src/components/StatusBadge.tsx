import type { ApplicationStatus } from '../api/types'

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{status}</span>
}
