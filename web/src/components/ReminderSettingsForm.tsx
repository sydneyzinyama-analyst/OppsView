import { useState, type FormEvent } from 'react'
import { ApiError, type FieldErrors } from '../api/client'
import { saveReminderSettings } from '../api/reminders'
import type { ReminderSettings } from '../api/types'

interface Props {
  settings: ReminderSettings
  onSaved: (settings: ReminderSettings) => void
}

const FIELDS: { key: keyof Omit<ReminderSettings, 'enabled'>; label: string; help: string }[] = [
  { key: 'followUpDays', label: 'Follow up', help: 'days after applying, if there’s no reply' },
  { key: 'moveOnDays', label: 'Time to move on', help: 'days after applying, if there’s still no reply' },
  { key: 'afterInterviewDays', label: 'After an interview', help: 'days after the invitation: thank-you or ask for feedback' },
  { key: 'offerReplyDays', label: 'Reply to an offer', help: 'days after receiving an offer' },
]

/** When reminders are sent. 0 switches one off. */
export function ReminderSettingsForm({ settings, onSaved }: Props) {
  const [form, setForm] = useState(settings)
  const [errors, setErrors] = useState<FieldErrors>({})
  const [saved, setSaved] = useState(false)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setErrors({})
    setSaved(false)
    try {
      onSaved(await saveReminderSettings(form))
      setSaved(true)
    } catch (err) {
      setErrors(err instanceof ApiError ? { ...err.fieldErrors, form: err.message } : { form: 'Could not save.' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <form className="reminder-settings" onSubmit={handleSubmit} noValidate aria-label="Reminder settings">
      <label className="toggle">
        <input type="checkbox" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} />
        Send me follow-up reminders
      </label>

      <fieldset disabled={!form.enabled}>
        {FIELDS.map(({ key, label, help }) => (
          <div className="field settings-row" key={key}>
            <label htmlFor={`setting-${key}`}>{label}</label>
            <input
              id={`setting-${key}`}
              type="number"
              min={0}
              max={90}
              value={form[key]}
              aria-invalid={!!errors[key]}
              onChange={(e) => setForm({ ...form, [key]: Number(e.target.value) })}
            />
            <span className="muted small">{help}</span>
            {errors[key] && <span className="field-error">{errors[key]}</span>}
          </div>
        ))}
        <p className="muted small">0 switches a reminder off. Reminders arrive in the morning.</p>
      </fieldset>

      {errors.form && !FIELDS.some((f) => errors[f.key]) && (
        <p className="alert" role="alert">
          {errors.form}
        </p>
      )}
      {saved && (
        <p className="notice" role="status">
          Saved. Your reminders have been re-planned.
        </p>
      )}
      <div className="actions">
        <button type="submit" className="primary" disabled={busy}>
          {busy ? 'Saving…' : 'Save settings'}
        </button>
      </div>
    </form>
  )
}
