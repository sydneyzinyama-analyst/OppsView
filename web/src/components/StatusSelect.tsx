import type { Application, ApplicationStatus } from '../api/types'
import { StatusBadge } from './StatusBadge'

interface Props {
  application: Application
  disabled?: boolean
  onChange: (status: ApplicationStatus) => void
  /** Words to show for each status (the ledger says "Sent" for Applied). */
  labels?: Partial<Record<ApplicationStatus, string>>
}

/**
 * Only offers the moves the server says are allowed (allowedNextStatuses),
 * so the transition rules live in one place: the API. A final status
 * (Offer, Rejected) has no moves left and is shown as a plain badge.
 */
export function StatusSelect({ application, disabled, onChange, labels = {} }: Props) {
  const { status, allowedNextStatuses: next } = application
  if (next.length === 0) return <StatusBadge status={status} label={labels[status]} />

  return (
    <select
      className={`status-select badge-${status.toLowerCase()}`}
      aria-label={`Status for ${application.company}`}
      value={status}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value as ApplicationStatus)}
    >
      <option value={status}>{labels[status] ?? status}</option>
      {next.map((s) => (
        <option key={s} value={s}>
          → {labels[s] ?? s}
        </option>
      ))}
    </select>
  )
}
