import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import * as api from '../api/applications'
import { getEmailStatus, listReviewItems } from '../api/email'
import { STATUSES, type Application, type ApplicationStatus, type EmailStatus, type ReviewItem, type SaveApplicationRequest } from '../api/types'
import { ApplicationForm } from '../components/ApplicationForm'
import { ApplicationList } from '../components/ApplicationList'
import { EmailPanel } from '../components/EmailPanel'
import { ReviewList } from '../components/ReviewList'
import { CONNECT_RESULTS } from '../lib/email'
import { StatTile } from '../components/charts'
import { MailIcon, PlusIcon, SearchIcon } from '../components/icons'
import { Modal } from '../components/Modal'
import { PageHeader } from '../components/PageHeader'

type Filter = 'All' | ApplicationStatus

function countByStatus(list: Application[]): Record<ApplicationStatus, number> {
  const counts = { Applied: 0, Viewed: 0, Interview: 0, Offer: 0, Rejected: 0 }
  for (const a of list) counts[a.status]++
  return counts
}

function filterApplications(list: Application[], filter: Filter, query: string): Application[] {
  const q = query.trim().toLowerCase()
  return list.filter(
    (a) =>
      (filter === 'All' || a.status === filter) &&
      (!q || a.company.toLowerCase().includes(q) || a.role.toLowerCase().includes(q)),
  )
}

function greeting(now = new Date()): string {
  const h = now.getHours()
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'
}

/**
 * Owns the list state. Child components call back with intent ("delete this")
 * and this page talks to the API and swaps in the server's response, so the
 * screen always shows what the server actually saved.
 */
export function ApplicationsPage() {
  const [applications, setApplications] = useState<Application[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [emailStatus, setEmailStatus] = useState<EmailStatus | null>(null)
  const [reviewItems, setReviewItems] = useState<ReviewItem[]>([])

  const [adding, setAdding] = useState(false)
  const [filter, setFilter] = useState<Filter>('All')
  const [query, setQuery] = useState('')

  // Google sends the browser back to /?gmail=connected (or an error code).
  // Read it once for the banner, then tidy the URL.
  const [searchParams, setSearchParams] = useSearchParams()
  const [connectResult] = useState(() => CONNECT_RESULTS[searchParams.get('gmail') ?? ''] ?? null)
  // A reminder's "View application" link: /?highlight=<id>.
  const [highlightId] = useState(() => searchParams.get('highlight'))
  useEffect(() => {
    if (searchParams.has('gmail') || searchParams.has('highlight')) setSearchParams({}, { replace: true })
  }, [searchParams, setSearchParams])

  // Bumping this reloads everything (Retry button, after a sync or review).
  const [reloadKey, setReloadKey] = useState(0)
  const reload = () => setReloadKey((k) => k + 1)

  useEffect(() => {
    let cancelled = false
    api.listApplications().then(
      (list) => {
        if (cancelled) return
        setApplications(list)
        setError(null)
      },
      (e: Error) => !cancelled && setError(e.message),
    )
    // Email import is optional: if these fail, the rest of the page still works.
    getEmailStatus().then(
      (status) => !cancelled && setEmailStatus(status),
      () => {},
    )
    listReviewItems().then(
      (items) => !cancelled && setReviewItems(items),
      () => {},
    )
    return () => {
      cancelled = true
    }
  }, [reloadKey])

  const replace = (updated: Application) =>
    setApplications((list) => list?.map((a) => (a.id === updated.id ? updated : a)) ?? null)

  async function handleCreate(body: SaveApplicationRequest) {
    const created = await api.createApplication(body)
    // Re-sort locally with the same order as the API (newest applied first).
    setApplications((list) =>
      [created, ...(list ?? [])].sort(
        (a, b) => b.appliedOn.localeCompare(a.appliedOn) || a.company.localeCompare(b.company),
      ),
    )
  }

  async function handleUpdate(id: string, body: SaveApplicationRequest) {
    replace(await api.updateApplication(id, body))
  }

  async function handleDelete(id: string) {
    try {
      await api.deleteApplication(id)
      setApplications((list) => list?.filter((a) => a.id !== id) ?? null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not delete the application.')
    }
  }

  async function handleChangeStatus(id: string, status: ApplicationStatus) {
    try {
      replace(await api.changeStatus(id, status))
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not change the status.')
    }
  }

  const counts = countByStatus(applications ?? [])
  const waiting = counts.Applied + counts.Viewed
  const shown = filterApplications(applications ?? [], filter, query)

  return (
    <main className="container">
      <PageHeader
        title={`${greeting()} 👋`}
        subtitle={
          applications === null
            ? 'Here’s how your job hunt is going.'
            : waiting === 0
              ? 'Nothing waiting on a reply right now.'
              : `${waiting} application${waiting === 1 ? ' is' : 's are'} waiting to hear back. Every email is an opportunity.`
        }
        actions={
          <button type="button" className="primary" onClick={() => setAdding(true)}>
            <PlusIcon /> New application
          </button>
        }
      />

      {connectResult && (
        <p className={connectResult.ok ? 'notice' : 'alert'} role="status">
          {connectResult.text}
        </p>
      )}

      {applications && applications.length > 0 && (
        <section className="stat-row" aria-label="Your numbers">
          <StatTile label="Applications" value={String(applications.length)} hint="in total" tone="brand" />
          <StatTile label="Waiting" value={String(waiting)} hint="applied or viewed" tone="viewed" />
          <StatTile label="Interviews" value={String(counts.Interview)} hint="in progress" tone="interview" />
          <StatTile label="Offers" value={String(counts.Offer)} hint={counts.Offer > 0 ? 'well done!' : 'keep going'} tone="offer" />
        </section>
      )}

      {emailStatus && (
        <section className="card email-card">
          <div className="card-icon" aria-hidden="true">
            <MailIcon />
          </div>
          <div className="email-card-body">
            <h2>Email import</h2>
            <EmailPanel status={emailStatus} onChanged={reload} />
          </div>
        </section>
      )}

      {reviewItems.length > 0 && (
        <section className="card">
          <h2>
            Needs review <span className="count">{reviewItems.length}</span>
          </h2>
          <p className="muted">OppsView wasn't sure about these emails. Add them as applications or ignore them.</p>
          <ReviewList items={reviewItems} onResolved={reload} />
        </section>
      )}

      <section className="card list-card">
        <div className="list-head">
          <h2>
            Your applications {applications && <span className="count">{applications.length}</span>}
          </h2>
          {applications && applications.length > 0 && (
            <label className="search">
              <SearchIcon />
              <span className="visually-hidden">Search applications</span>
              <input
                type="search"
                placeholder="Search company or role"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
              />
            </label>
          )}
        </div>

        {applications && applications.length > 0 && (
          <div className="filters" role="group" aria-label="Show applications by status">
            {(['All', ...STATUSES] as const).map((f) => (
              <button
                key={f}
                type="button"
                className={`filter${f !== 'All' ? ` filter-${f.toLowerCase()}` : ''}`}
                aria-pressed={filter === f}
                onClick={() => setFilter(f)}
              >
                {f} <span className="filter-count">{f === 'All' ? applications.length : counts[f]}</span>
              </button>
            ))}
          </div>
        )}

        {error && (
          <p className="alert" role="alert">
            {error}{' '}
            <button type="button" className="link" onClick={reload}>
              Retry
            </button>
          </p>
        )}
        {applications === null ? (
          !error && <p className="muted">Loading…</p>
        ) : applications.length > 0 && shown.length === 0 ? (
          <p className="empty">No applications match. Try another search or filter.</p>
        ) : (
          <ApplicationList
            applications={shown}
            highlightId={highlightId}
            onUpdate={handleUpdate}
            onDelete={handleDelete}
            onChangeStatus={handleChangeStatus}
          />
        )}
      </section>

      {adding && (
        <Modal title="New application" onClose={() => setAdding(false)}>
          <ApplicationForm
            onCancel={() => setAdding(false)}
            onSubmit={async (body) => {
              await handleCreate(body)
              setAdding(false)
            }}
          />
        </Modal>
      )}
    </main>
  )
}
