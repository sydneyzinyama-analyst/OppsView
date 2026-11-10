import { useEffect, useState } from 'react'
import * as api from '../api/applications'
import type { Application, ApplicationStatus, SaveApplicationRequest } from '../api/types'
import { useAuth } from '../auth/useAuth'
import { ApplicationForm } from '../components/ApplicationForm'
import { ApplicationList } from '../components/ApplicationList'

/**
 * Owns the list state. Child components call back with intent ("delete this")
 * and this page talks to the API and swaps in the server's response, so the
 * screen always shows what the server actually saved.
 */
export function ApplicationsPage() {
  const { session, logout } = useAuth()
  const [applications, setApplications] = useState<Application[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  // Bumping this re-runs the load effect (used by the Retry button).
  const [reloadKey, setReloadKey] = useState(0)

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
    <>
      <header className="topbar">
        <div>
          <span className="brand">OppsView</span>
          <span className="tagline">Every email is an opportunity.</span>
        </div>
        <div className="user">
          <span className="muted">{session?.email}</span>
          <button type="button" onClick={logout}>
            Sign out
          </button>
        </div>
      </header>

      <main className="container">
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
              <button type="button" className="link" onClick={() => setReloadKey((k) => k + 1)}>
                Retry
              </button>
            </p>
          )}
          {applications === null ? (
            !error && <p className="muted">Loading…</p>
          ) : (
            <ApplicationList
              applications={applications}
              onUpdate={handleUpdate}
              onDelete={handleDelete}
              onChangeStatus={handleChangeStatus}
            />
          )}
        </section>
      </main>
    </>
  )
}
