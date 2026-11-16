import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import * as api from '../api/applications'
import { getEmailStatus, listReviewItems } from '../api/email'
import type { Application, ApplicationStatus, EmailStatus, ReviewItem, SaveApplicationRequest } from '../api/types'
import { ApplicationForm } from '../components/ApplicationForm'
import { ApplicationList } from '../components/ApplicationList'
import { EmailPanel } from '../components/EmailPanel'
import { ReviewList } from '../components/ReviewList'
import { CONNECT_RESULTS } from '../lib/email'

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

  return (
      <main className="container">
        {connectResult && (
          <p className={connectResult.ok ? 'notice' : 'alert'} role="status">
            {connectResult.text}
          </p>
        )}

        {emailStatus && (
          <section className="card">
            <h2>Email import</h2>
            <EmailPanel status={emailStatus} onChanged={reload} />
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

        <section className="card">
          <h2>Add an application</h2>
          <ApplicationForm onSubmit={handleCreate} />
        </section>

        <section className="card">
          <h2>
            Your applications{' '}
            {applications && <span className="count">{applications.length}</span>}
          </h2>
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
          ) : (
            <ApplicationList
              applications={applications}
              highlightId={highlightId}
              onUpdate={handleUpdate}
              onDelete={handleDelete}
              onChangeStatus={handleChangeStatus}
            />
          )}
        </section>
      </main>
  )
}
