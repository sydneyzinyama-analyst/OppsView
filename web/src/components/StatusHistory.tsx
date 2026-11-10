import { useEffect, useState } from 'react'
import { getHistory } from '../api/applications'
import type { Application, StatusChange } from '../api/types'
import { formatDateTime } from '../lib/dates'
import { StatusBadge } from './StatusBadge'

interface Props {
  application: Application
}

type Loaded = { key: string; history: StatusChange[] } | { key: string; error: string }

/** Timeline of status changes. Re-fetches whenever the application's status changes. */
export function StatusHistory({ application }: Props) {
  const { id, status, createdAt, company } = application
  const key = `${id}:${status}`
  // Results are tagged with the key they were fetched for; anything tagged
  // with an older key is stale, which we treat as "loading". That way state
  // is only set when a request finishes, never synchronously in the effect.
  const [loaded, setLoaded] = useState<Loaded | null>(null)

  useEffect(() => {
    let cancelled = false
    getHistory(id).then(
      (history) => !cancelled && setLoaded({ key, history }),
      (e: Error) => !cancelled && setLoaded({ key, error: e.message }),
    )
    return () => {
      cancelled = true
    }
  }, [id, key])

  if (!loaded || loaded.key !== key) return <p className="muted">Loading history…</p>
  if ('error' in loaded) return <p className="field-error">Couldn't load history: {loaded.error}</p>

  return (
    <ol className="timeline" aria-label={`Status history for ${company}`}>
      <li>
        <StatusBadge status="Applied" /> <span className="muted">added {formatDateTime(createdAt)}</span>
      </li>
      {loaded.history.map((h) => (
        <li key={h.changedAt + h.toStatus}>
          <StatusBadge status={h.toStatus} />{' '}
          <span className="muted">
            from {h.fromStatus}, {formatDateTime(h.changedAt)}
          </span>
        </li>
      ))}
    </ol>
  )
}
