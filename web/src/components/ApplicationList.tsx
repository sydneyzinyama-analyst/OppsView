import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { PLATFORM_LABELS, type Application, type ApplicationStatus, type SaveApplicationRequest } from '../api/types'
import { avatarTint, initials } from '../lib/avatar'
import { formatDate, timeAgo } from '../lib/dates'
import { scoreClass } from '../lib/score'
import { ApplicationForm } from './ApplicationForm'
import { MailIcon } from './icons'
import { SavedMatchView } from './SavedMatchView'
import { StatusHistory } from './StatusHistory'
import { StatusSelect } from './StatusSelect'

interface Props {
  applications: Application[]
  /** Scroll to and highlight this application (from a reminder's link). */
  highlightId?: string | null
  onUpdate: (id: string, body: SaveApplicationRequest) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onChangeStatus: (id: string, status: ApplicationStatus) => Promise<void>
}

type Expanded = { id: string; view: 'edit' | 'history' } | null

export function ApplicationList({ applications, highlightId, onUpdate, onDelete, onChangeStatus }: Props) {
  const [expanded, setExpanded] = useState<Expanded>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  useEffect(() => {
    if (highlightId) document.getElementById(`application-${highlightId}`)?.scrollIntoView?.({ block: 'center' })
  }, [highlightId])

  if (applications.length === 0) {
    return <p className="empty">No applications yet. Click New application to add your first one, or connect Gmail and let OppsView find them.</p>
  }

  const toggle = (id: string, view: 'edit' | 'history') =>
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
    <ul className="app-list">
      {applications.map((a) => {
        const isOpen = expanded?.id === a.id
        return (
          <li
            key={a.id}
            id={`application-${a.id}`}
            className={['app-item', isOpen && 'open', a.id === highlightId && 'highlight'].filter(Boolean).join(' ')}
          >
            <div className="app-row">
              <span className={`avatar tint-${avatarTint(a.company)}`} aria-hidden="true">
                {initials(a.company)}
              </span>
              <div className="app-main">
                <div className="app-title">
                  {a.jobUrl ? (
                    <a href={a.jobUrl} target="_blank" rel="noreferrer noopener" className="app-company">
                      {a.company}
                    </a>
                  ) : (
                    <span className="app-company">{a.company}</span>
                  )}
                  {a.source === 'Email' && (
                    <span className="source-tag" title="Added automatically from an email">
                      <MailIcon width={12} height={12} /> from email
                    </span>
                  )}
                </div>
                <div className="app-role">
                  {a.role}
                  {a.matchScore !== null && (
                    <span className={`score-tag ${scoreClass(a.matchScore)}`} title="How well your CV matched this job's ad">
                      {a.matchScore}% match
                    </span>
                  )}
                </div>
                <div className="app-meta">
                  <span>{PLATFORM_LABELS[a.platform]}</span>
                  <span aria-hidden="true">·</span>
                  <span title={`Applied on ${formatDate(a.appliedOn)}`}>Applied {timeAgo(a.appliedOn)}</span>
                </div>
              </div>
              <div className="app-status">
                <StatusSelect
                  application={a}
                  disabled={busyId === a.id}
                  onChange={(s) => withBusy(a.id, () => onChangeStatus(a.id, s))}
                />
                {a.noReplyDeadline && (
                  <span className="deadline-tag" title="No reply by this date means unsuccessful (from the employer's email)">
                    reply by {formatDate(a.noReplyDeadline)}
                  </span>
                )}
              </div>
              <div className="app-actions">
                <button
                  type="button"
                  className="ghost"
                  aria-expanded={isOpen && expanded.view === 'history'}
                  onClick={() => toggle(a.id, 'history')}
                >
                  History
                </button>
                <Link className="ghost" to={`/match?applicationId=${a.id}`}>
                  Match CV
                </Link>
                <button
                  type="button"
                  className="ghost"
                  aria-expanded={isOpen && expanded.view === 'edit'}
                  onClick={() => toggle(a.id, 'edit')}
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="ghost danger"
                  disabled={busyId === a.id}
                  onClick={() => handleDelete(a)}
                  aria-label={`Delete ${a.company}`}
                >
                  Delete
                </button>
              </div>
            </div>
            {isOpen && (
              <div className="app-detail">
                {expanded.view === 'edit' ? (
                  <ApplicationForm
                    initial={a}
                    onCancel={() => setExpanded(null)}
                    onSubmit={async (body) => {
                      await onUpdate(a.id, body)
                      setExpanded(null)
                    }}
                  />
                ) : (
                  <>
                    {a.notes && <p className="notes">{a.notes}</p>}
                    <StatusHistory application={a} />
                    {a.matchScore !== null && <SavedMatchView applicationId={a.id} />}
                  </>
                )}
              </div>
            )}
          </li>
        )
      })}
    </ul>
  )
}
