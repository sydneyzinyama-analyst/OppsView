import { Fragment, useState } from 'react'
import { PLATFORM_LABELS, type Application, type ApplicationStatus, type SaveApplicationRequest } from '../api/types'
import { formatDate } from '../lib/dates'
import { ApplicationForm } from './ApplicationForm'
import { StatusHistory } from './StatusHistory'
import { StatusSelect } from './StatusSelect'

interface Props {
  applications: Application[]
  onUpdate: (id: string, body: SaveApplicationRequest) => Promise<void>
  onDelete: (id: string) => Promise<void>
  onChangeStatus: (id: string, status: ApplicationStatus) => Promise<void>
}

type Expanded = { id: string; view: 'edit' | 'history' } | null

export function ApplicationList({ applications, onUpdate, onDelete, onChangeStatus }: Props) {
  const [expanded, setExpanded] = useState<Expanded>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  if (applications.length === 0) {
    return <p className="empty">No applications yet. Add your first one above.</p>
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
    <div className="table-wrap">
      <table className="applications">
        <thead>
          <tr>
            <th>Company</th>
            <th>Role</th>
            <th>Platform</th>
            <th>Applied</th>
            <th>Status</th>
            <th>
              <span className="visually-hidden">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {applications.map((a) => {
            const isOpen = expanded?.id === a.id
            return (
              <Fragment key={a.id}>
                <tr className={isOpen ? 'open' : undefined}>
                  <td data-label="Company">
                    {a.jobUrl ? (
                      <a href={a.jobUrl} target="_blank" rel="noreferrer noopener">
                        {a.company}
                      </a>
                    ) : (
                      a.company
                    )}
                  </td>
                  <td data-label="Role">{a.role}</td>
                  <td data-label="Platform">{PLATFORM_LABELS[a.platform]}</td>
                  <td data-label="Applied">{formatDate(a.appliedOn)}</td>
                  <td data-label="Status">
                    <StatusSelect
                      application={a}
                      disabled={busyId === a.id}
                      onChange={(s) => withBusy(a.id, () => onChangeStatus(a.id, s))}
                    />
                  </td>
                  <td className="row-actions">
                    <button
                      type="button"
                      className="link"
                      aria-expanded={isOpen && expanded.view === 'history'}
                      onClick={() => toggle(a.id, 'history')}
                    >
                      History
                    </button>
                    <button
                      type="button"
                      className="link"
                      aria-expanded={isOpen && expanded.view === 'edit'}
                      onClick={() => toggle(a.id, 'edit')}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="link danger"
                      disabled={busyId === a.id}
                      onClick={() => handleDelete(a)}
                      aria-label={`Delete ${a.company}`}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
                {isOpen && (
                  <tr className="detail-row">
                    <td colSpan={6}>
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
                        </>
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
