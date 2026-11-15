import { useState } from 'react'
import { createFromReviewItem, dismissReviewItem } from '../api/email'
import type { ReviewItem } from '../api/types'
import { formatDateTime, localToday } from '../lib/dates'
import { KIND_LABELS } from '../lib/email'
import { ApplicationForm } from './ApplicationForm'

interface Props {
  items: ReviewItem[]
  /** Called after an item is resolved, so the page can refresh. */
  onResolved: () => void
}

/**
 * Emails OppsView wasn't sure about. For each, the user either adds it as an
 * application (with the details OppsView found filled in) or ignores it.
 */
export function ReviewList({ items, onResolved }: Props) {
  const [openId, setOpenId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (items.length === 0) return null

  async function dismiss(id: string) {
    setError(null)
    try {
      await dismissReviewItem(id)
      onResolved()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not ignore this email.')
    }
  }

  return (
    <div className="review-list">
      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}
      <ul>
        {items.map((item) => (
          <li key={item.id} className="review-item">
            <div className="review-summary">
              <div>
                <strong>{item.subject || '(no subject)'}</strong>
                <div className="muted small">
                  {item.sender} · {formatDateTime(item.receivedAt)} · {KIND_LABELS[item.kind]}
                </div>
                {item.reason && <div className="small">{item.reason}</div>}
              </div>
              <div className="row-actions">
                {!item.applicationId && (
                  <button
                    type="button"
                    className="link"
                    aria-expanded={openId === item.id}
                    onClick={() => setOpenId(openId === item.id ? null : item.id)}
                  >
                    Add as application
                  </button>
                )}
                <button type="button" className="link" onClick={() => dismiss(item.id)}>
                  Ignore
                </button>
              </div>
            </div>
            {openId === item.id && (
              <ApplicationForm
                idPrefix={`review-${item.id}`}
                submitLabel="Add application"
                prefill={{
                  company: item.company ?? '',
                  role: item.role ?? '',
                  platform: item.platform ?? undefined,
                  // Confirmations are sent the day you apply; for later
                  // emails (rejections...) the user may want to adjust this.
                  appliedOn: localToday(new Date(item.receivedAt)),
                }}
                onCancel={() => setOpenId(null)}
                onSubmit={async (body) => {
                  await createFromReviewItem(item.id, body)
                  setOpenId(null)
                  onResolved()
                }}
              />
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}
