import { Fragment, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { PLATFORM_LABELS, type Application, type ApplicationStatus, type SaveApplicationRequest } from '../api/types'
import { formatDate } from '../lib/dates'
import { entryNumbers, progressSteps, quietDays, STAMP_LABELS } from '../lib/ledger'
import { scoreClass } from '../lib/score'
import { ApplicationForm } from './ApplicationForm'
import { SavedMatchView } from './SavedMatchView'
import { StatusHistory } from './StatusHistory'
import { StatusSelect } from './StatusSelect'

interface Props {
  applications: Application[]
  /** Entry numbers (001, 002…); worked out from `applications` when not given. */
  numbers?: Map<string, number>
  /** Scroll to and highlight this application (from a reminder's link). */
  highlightId?: string | null
  onUpdate: (id: string, body: SaveApplicationRequest) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onChangeStatus: (id: string, status: ApplicationStatus) => Promise<void>
}

type Expanded = { id: string; view: 'details' | 'edit' } | null

/** "20 Oct" in the ledger's typewriter style. */
const shortDate = (iso: string) => {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' })
}

/**
 * The ledger: one row per application, numbered in the order they were
 * added. A row opens to show its history and the actions (match, edit, delete).
 */
export function ApplicationList({ applications, numbers, highlightId, onUpdate, onDelete, onChangeStatus }: Props) {
  const [expanded, setExpanded] = useState<Expanded>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  useEffect(() => {
    if (highlightId) document.getElementById(`application-${highlightId}`)?.scrollIntoView?.({ block: 'center' })
  }, [highlightId])

  if (applications.length === 0) {
    return <p className="empty">No applications yet. Add a new entry, or connect Gmail and let OppsView find them.</p>
  }

  const entryNo = numbers ?? entryNumbers(applications)
  const toggle = (id: string, view: 'details' | 'edit') =>
    setExpanded((e) => (e?.id === id && e.view === view ? null : { id, view }))

  async function withBusy(id: string, action: () => Promise<void>) {
    setBusyId(id)
    try {
      await action()
    } finally {
      setBusyId(null)
    }
  }

  async function handleDelete(a: Application) {
    if (!window.confirm(`Delete your application to ${a.company} (${a.role})? This can't be undone.`)) return
    await withBusy(a.id, () => onDelete(a.id))
  }

  return (
    <div className="ledger-wrap">
      <table className="ledger">
        <thead>
          <tr>
            <th className="col-no">No.</th>
            <th>Company</th>
            <th>Role</th>
            <th>Via</th>
            <th>Sent</th>
            <th>Progress</th>
            <th>Status</th>
            <th>
              <span className="visually-hidden">Details</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {applications.map((a) => {
            const isOpen = expanded?.id === a.id
            const quiet = quietDays(a)
            const steps = progressSteps(a.status)
            const rowClass = [
              `row-${a.status.toLowerCase()}`,
              isOpen && 'open',
              a.id === highlightId && 'highlight',
            ].filter(Boolean).join(' ')
            return (
              <Fragment key={a.id}>
                <tr id={`application-${a.id}`} className={rowClass}>
                  <td className="col-no mono" data-label="No.">
                    {String(entryNo.get(a.id) ?? 0).padStart(3, '0')}
                  </td>
                  <td className="col-company" data-label="Company">
                    {a.jobUrl ? (
                      <a href={a.jobUrl} target="_blank" rel="noreferrer noopener" className="company">
                        {a.company}
                      </a>
                    ) : (
                      <span className="company">{a.company}</span>
                    )}
                    {a.source === 'Email' && (
                      <span className="source-tag" title="Added automatically from an email">
                        from email
                      </span>
                    )}
                  </td>
                  <td className="col-role" data-label="Role">
                    {a.role}
                    {a.matchScore !== null && (
                      <span className={`score-tag ${scoreClass(a.matchScore)}`} title="How well your CV matched this job's ad">
                        {a.matchScore}% match
                      </span>
                    )}
                  </td>
                  <td className="col-via muted" data-label="Via">
                    {PLATFORM_LABELS[a.platform]}
                  </td>
                  <td className="col-sent mono" data-label="Sent" title={formatDate(a.appliedOn)}>
                    {shortDate(a.appliedOn)}
                  </td>
                  <td className="col-progress" data-label="Progress">
                    <span
                      className="progress"
                      role="img"
                      aria-label={a.status === 'Rejected' ? 'Declined' : `${steps} of 4 steps: ${a.status}`}
                    >
                      {[1, 2, 3, 4].map((i) => (
                        <span key={i} className={i <= steps ? 'on' : undefined} />
                      ))}
                    </span>
                  </td>
                  <td className="col-status" data-label="Status">
                    <StatusSelect
                      application={a}
                      labels={STAMP_LABELS}
                      disabled={busyId === a.id}
                      onChange={(s) => withBusy(a.id, () => onChangeStatus(a.id, s))}
                    />
                    {quiet !== null && <span className="quiet-tag">{quiet} days quiet</span>}
                    {a.noReplyDeadline && (
                      <span className="deadline-tag" title="No reply by this date means unsuccessful (from the employer's email)">
                        reply by {formatDate(a.noReplyDeadline)}
                      </span>
                    )}
                  </td>
                  <td className="col-open">
                    <button
                      type="button"
                      className="row-toggle"
                      aria-expanded={isOpen}
                      aria-label={`Details for ${a.company}`}
                      title="History, match, edit, delete"
                      onClick={() => toggle(a.id, 'details')}
                    >
                      <span aria-hidden="true">{isOpen ? '−' : '+'}</span>
                    </button>
                  </td>
                </tr>
                {isOpen && (
                  <tr className="detail-row">
                    <td colSpan={8}>
                      {expanded.view === 'edit' ? (
                        <ApplicationForm
                          initial={a}
                          onCancel={() => setExpanded({ id: a.id, view: 'details' })}
                          onSubmit={async (body) => {
                            await onUpdate(a.id, body)
                            setExpanded(null)
                          }}
                        />
                      ) : (
                        <div className="detail">
                          <div className="detail-body">
                            {a.notes && <p className="notes">{a.notes}</p>}
                            <StatusHistory application={a} />
                            {a.matchScore !== null && <SavedMatchView applicationId={a.id} />}
                          </div>
                          <div className="detail-actions">
                            <Link className="text-button" to={`/match?applicationId=${a.id}`}>
                              Match CV
                            </Link>
                            <button type="button" className="text-button" onClick={() => toggle(a.id, 'edit')}>
                              Edit
                            </button>
                            <button
                              type="button"
                              className="text-button danger"
                              disabled={busyId === a.id}
                              onClick={() => handleDelete(a)}
                              aria-label={`Delete ${a.company}`}
                            >
                              Delete
                            </button>
                          </div>
                        </div>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
