import type { Application, ApplicationStatus } from '../api/types'
import { StatusBadge } from './StatusBadge'

interface Props {
  application: Application
  disabled?: boolean
  onChange: (status: ApplicationStatus) => void
}

/**
 * Only offers the moves the server says are allowed (allowedNextStatuses),
 * so the transition rules live in one place: the API. A final status
 * (Offer, Rejected) has no moves left and is shown as a plain badge.
 */
export function StatusSelect({ application, disabled, onChange }: Props) {
  const { status, allowedNextStatuses: next } = application
  if (next.length === 0) return <StatusBadge status={status} />

  return (
    <select
      className={`status-select badge-${status.toLowerCase()}`}
      aria-label={`Status for ${application.company}`}
      value={status}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value as ApplicationStatus)}
    >
      <option value={status}>{status}</option>
      {next.map((s) => (
        <option key={s} value={s}>
          → {s}
        </option>
      ))}
    </select>
  )
}
