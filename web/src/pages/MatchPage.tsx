import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { listApplications } from '../api/applications'
import { ApiError } from '../api/client'
import { getCv, matchCv } from '../api/matching'
import type { Application, Cv, MatchResult } from '../api/types'
import { CvPanel } from '../components/CvPanel'
import { MatchResultView } from '../components/MatchResultView'

/** Upload a CV, paste a job ad, see how well they match. */
export function MatchPage() {
  // "Match CV" on an application links here with ?applicationId=...
  const [searchParams] = useSearchParams()
  const [cv, setCv] = useState<Cv | null | undefined>(undefined) // undefined = still loading
  const [applications, setApplications] = useState<Application[]>([])
  const [applicationId, setApplicationId] = useState(searchParams.get('applicationId') ?? '')
  const [jobAd, setJobAd] = useState('')
  const [result, setResult] = useState<MatchResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    getCv().then(
      (c) => !cancelled && setCv(c),
      (e: Error) => !cancelled && setError(e.message),
    )
    listApplications().then(
      (list) => !cancelled && setApplications(list),
      () => {},
    )
    return () => {
      cancelled = true
    }
  }, [])

  async function handleMatch(e: FormEvent) {
    e.preventDefault()
    if (!jobAd.trim()) {
      setError('Paste the job ad first.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      setResult(await matchCv(jobAd, applicationId || null))
    } catch (err) {
      setResult(null)
      setError(err instanceof ApiError ? (err.fieldErrors.jobAd ?? err.message) : 'Could not match the CV.')
    } finally {
      setBusy(false)
    }
  }

  const selected = applications.find((a) => a.id === applicationId)

  return (
    <main className="container">
      <section className="card">
        <h2>Your CV</h2>
        {cv === undefined ? <p className="muted">Loading…</p> : <CvPanel cv={cv} onChanged={setCv} />}
      </section>

      <section className="card">
        <h2>Match a job ad</h2>
        <form onSubmit={handleMatch} noValidate aria-label="Match a job ad">
          <div className="field">
            <label htmlFor="job-ad">Job ad</label>
            <textarea
              id="job-ad"
              rows={10}
              placeholder="Paste the whole ad, including the requirements and the 'advantageous' section."
              value={jobAd}
              onChange={(e) => setJobAd(e.target.value)}
            />
          </div>
          <div className="field">
            <label htmlFor="match-application">Save the result to an application (optional)</label>
            <select id="match-application" value={applicationId} onChange={(e) => setApplicationId(e.target.value)}>
              <option value="">Don't save</option>
              {applications.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.company} – {a.role}
                </option>
              ))}
            </select>
          </div>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <div className="actions">
            <button type="submit" className="primary" disabled={busy || !cv}>
              {busy ? 'Matching…' : 'Match my CV'}
            </button>
          </div>
          {!cv && cv !== undefined && <p className="muted small">Upload your CV above first.</p>}
        </form>
      </section>

      {result && (
        <section className="card">
          <h2>
            Result
            {selected && result.applicationId && (
              <span className="muted small">
                {' '}
                saved to {selected.company} – {selected.role}
              </span>
            )}
          </h2>
          <MatchResultView result={result} />
        </section>
      )}
    </main>
  )
}
