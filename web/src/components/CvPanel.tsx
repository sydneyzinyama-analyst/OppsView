import { useRef, useState } from 'react'
import { ApiError } from '../api/client'
import { deleteCv, uploadCv } from '../api/matching'
import type { Cv } from '../api/types'
import { formatDateTime } from '../lib/dates'

interface Props {
  cv: Cv | null
  onChanged: (cv: Cv | null) => void
}

/** Shows the stored CV and lets the user upload, replace or remove it. */
export function CvPanel({ cv, onChanged }: Props) {
  const input = useRef<HTMLInputElement>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function upload(file: File | undefined) {
    if (!file) return
    setBusy(true)
    setError(null)
    try {
      onChanged(await uploadCv(file))
    } catch (e) {
      setError(e instanceof ApiError ? (e.fieldErrors.file ?? e.message) : 'Could not upload the CV.')
    } finally {
      setBusy(false)
      if (input.current) input.current.value = '' // allow choosing the same file again
    }
  }

  async function remove() {
    if (!window.confirm('Remove your CV from OppsView?')) return
    setBusy(true)
    try {
      await deleteCv()
      onChanged(null)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="cv-panel">
      {cv ? (
        <div>
          <p>
            <strong>{cv.fileName}</strong>{' '}
            <span className="muted small">uploaded {formatDateTime(cv.uploadedAt)}</span>
          </p>
          <details>
            <summary className="small">What OppsView read from it</summary>
            <p className="cv-preview">{cv.preview}</p>
          </details>
        </div>
      ) : (
        <p className="muted">
          Upload your CV once (PDF, Word or text, up to 5 MB). Only its text is kept, to compare with job ads.
        </p>
      )}

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <div className="actions">
        {cv && (
          <button type="button" className="link danger" onClick={remove} disabled={busy}>
            Remove
          </button>
        )}
        <label className={`button primary${busy ? ' disabled' : ''}`}>
          {busy ? 'Reading CV…' : cv ? 'Replace CV' : 'Upload CV'}
          <input
            ref={input}
            type="file"
            accept=".pdf,.docx,.txt"
            className="visually-hidden"
            aria-label={cv ? 'Replace CV' : 'Upload CV'}
            disabled={busy}
            onChange={(e) => upload(e.target.files?.[0])}
          />
        </label>
      </div>
    </div>
  )
}
