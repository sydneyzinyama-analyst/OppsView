import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import * as api from '../api/applications'
import { getEmailStatus, listReviewItems } from '../api/email'
import type { Application, ApplicationStatus, EmailStatus, ReviewItem, SaveApplicationRequest } from '../api/types'
import { ApplicationForm } from '../components/ApplicationForm'
import { ApplicationList } from '../components/ApplicationList'
import { EmailPanel } from '../components/EmailPanel'
import { Modal } from '../components/Modal'
import { ReviewList } from '../components/ReviewList'
import { CONNECT_RESULTS } from '../lib/email'
import { dateline, entryNumbers, headline, marginNotes, summary, tally } from '../lib/ledger'

type Filter = 'All' | 'Open' | 'Closed'

/** Closed = turned down; everything else (offers included) is still open. */
const isClosed = (a: Application) => a.status === 'Rejected'

function filterApplications(list: Application[], filter: Filter, query: string): Application[] {
  const q = query.trim().toLowerCase()
  return list.filter(
    (a) =>
      (filter === 'All' || (filter === 'Closed') === isClosed(a)) &&
      (!q || a.company.toLowerCase().includes(q) || a.role.toLowerCase().includes(q)),
  )
}

/**
 * The ledger (home page). Owns the list state: child components call back
 * with intent ("delete this") and this page talks to the API and swaps in
 * the server's response, so the screen always shows what the server saved.
 */
export function ApplicationsPage() {
  const [applications, setApplications] = useState<Application[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [emailStatus, setEmailStatus] = useState<EmailStatus | null>(null)
  const [reviewItems, setReviewItems] = useState<ReviewItem[]>([])
  const [filter, setFilter] = useState<Filter>('All')

  // The address carries one-off signals: ?gmail=… (back from Google),
  // ?highlight=<id> (a reminder's link) and ?new=1 (the "New entry" button).
  // Read them once, then tidy the URL. ?q= (search) stays: it's the search box's value.
  const [searchParams, setSearchParams] = useSearchParams()
  const [connectResult] = useState(() => CONNECT_RESULTS[searchParams.get('gmail') ?? ''] ?? null)
  const [highlightId] = useState(() => searchParams.get('highlight'))
  const [adding, setAdding] = useState(false)
  const query = searchParams.get('q') ?? ''
  const wantsNew = searchParams.get('new') === '1'
  // "New entry" pressed: open the window (adjusting state while rendering,
  // React's pattern for state that follows a changed input).
  if (wantsNew && !adding) setAdding(true)
  useEffect(() => {
    if (searchParams.has('gmail') || searchParams.has('highlight') || wantsNew) {
      const keep = new URLSearchParams()
      if (searchParams.get('q')) keep.set('q', searchParams.get('q')!)
      setSearchParams(keep, { replace: true })
    }
  }, [searchParams, setSearchParams, wantsNew])

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

  const all = applications ?? []
  const t = tally(all)
  const [line1, line2] = headline(t)
  const shown = filterApplications(all, filter, query)
  const notes = marginNotes(all)
  const closed = all.filter(isClosed).length
  const offerShare = t.sent ? Math.round((t.offers / t.sent) * 100) : 0

  return (
    <main className="container ledger-page">
      <div className="ledger-main">
        <section className="hero" aria-label="Your week">
          <p className="dateline mono">{dateline()}</p>
          <h1 className="hero-title">
            {line1}
            <br />
            <em>{line2}</em>
          </h1>
          <p className="hero-lede">{applications ? summary(all) : 'Opening the ledger…'}</p>
        </section>

        {connectResult && (
          <p className={connectResult.ok ? 'notice' : 'alert'} role="status">
            {connectResult.text}
          </p>
        )}

        <section className="tally" aria-label="Your numbers">
          <div className="tally-item">
            <span className="tally-value">{t.sent}</span>
            <span className="tally-label">Sent</span>
          </div>
          <div className="tally-item">
            <span className="tally-value">{t.awaiting}</span>
            <span className="tally-label">Awaiting word</span>
          </div>
          <div className="tally-item">
            <span className="tally-value">{t.interviews}</span>
            <span className="tally-label">{t.interviews === 1 ? 'Interview' : 'Interviews'}</span>
          </div>
          <div className="tally-item tally-accent">
            <span className="tally-value">{t.offers}</span>
            <span className="tally-label">
              {t.offers === 1 ? 'Offer' : 'Offers'} · {offerShare}% of sent
            </span>
          </div>
        </section>

        <section className="ledger-section" aria-labelledby="applications-heading">
          <div className="section-head">
            <h2 id="applications-heading" className="section-title">
              Applications
            </h2>
            {all.length > 0 && (
              <div className="pills" role="group" aria-label="Show applications">
                {(
                  [
                    ['All', all.length],
                    ['Open', all.length - closed],
                    ['Closed', closed],
                  ] as const
                ).map(([f, n]) => (
                  <button key={f} type="button" className="pill" aria-pressed={filter === f} onClick={() => setFilter(f)}>
                    {f} {n}
                  </button>
                ))}
              </div>
            )}
          </div>
          {query && (
            <p className="muted search-note">
              Showing matches for “{query}”. <Link to="/">Clear search</Link>
            </p>
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
          ) : all.length > 0 && shown.length === 0 ? (
            <p className="empty">No applications match. Try another search or filter.</p>
          ) : (
            <ApplicationList
              applications={shown}
              numbers={entryNumbers(all)}
              highlightId={highlightId}
              onUpdate={handleUpdate}
              onDelete={handleDelete}
              onChangeStatus={handleChangeStatus}
            />
          )}
        </section>

        {reviewItems.length > 0 && (
          <section className="ledger-section" aria-labelledby="review-heading">
            <h2 id="review-heading" className="section-title">
              Needs a look <span className="count">{reviewItems.length}</span>
            </h2>
            <p className="muted">OppsView wasn't sure about these emails. Add them as applications or ignore them.</p>
            <ReviewList items={reviewItems} onResolved={reload} />
          </section>
        )}
      </div>

      <aside className="margins" aria-labelledby="margins-heading">
        <h2 id="margins-heading" className="margins-title">
          In the margins
        </h2>
        {notes.length === 0 && applications && (
          <p className="muted margin-empty">Nothing needs you right now. Notes about offers, interviews and quiet companies appear here.</p>
        )}
        {notes.map((n) => (
          <Link key={n.key} to={n.to} className={`margin-note note-${n.tone}`}>
            <span className="note-kicker mono">{n.kicker}</span>
            <span className="note-title">{n.title}</span>
            <span className="note-body">{n.body}</span>
            <span className="note-arrow" aria-hidden="true">
              ↗
            </span>
          </Link>
        ))}
        {emailStatus && (
          <div className="margin-email">
            <EmailPanel status={emailStatus} onChanged={reload} compact />
          </div>
        )}
      </aside>

      {adding && (
        <Modal title="New entry" onClose={() => setAdding(false)}>
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
