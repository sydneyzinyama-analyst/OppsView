import { useState, type FormEvent } from 'react'
import { ApiError, type FieldErrors } from '../api/client'
import { PLATFORMS, PLATFORM_LABELS, type Application, type JobPlatform, type SaveApplicationRequest } from '../api/types'
import { localToday } from '../lib/dates'
import { validateApplication, type FormState } from '../lib/validation'

interface Props {
  /** When given, the form edits this application instead of creating one. */
  initial?: Application
  /** Starting values for a new application (e.g. details found in an email). */
  prefill?: Partial<SaveApplicationRequest>
  /** Makes input ids unique when several forms are on the page. */
  idPrefix?: string
  submitLabel?: string
  onSubmit: (body: SaveApplicationRequest) => Promise<void>
  onCancel?: () => void
}


function toFormState(a?: Partial<SaveApplicationRequest>): FormState {
  return {
    company: a?.company ?? '',
    role: a?.role ?? '',
    platform: a?.platform ?? '',
    appliedOn: a?.appliedOn ?? localToday(),
    jobUrl: a?.jobUrl ?? '',
    notes: a?.notes ?? '',
  }
}

export function ApplicationForm({ initial, prefill, idPrefix: idPrefixProp, submitLabel, onSubmit, onCancel }: Props) {
  const isEdit = !!initial
  const [form, setForm] = useState<FormState>(() => toFormState(initial ?? prefill))
  const [errors, setErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const idPrefix = idPrefixProp ?? (isEdit ? `edit-${initial.id}` : 'new')

  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((f) => ({ ...f, [key]: value }))
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    setFormError(null)
    const clientErrors = validateApplication(form)
    setErrors(clientErrors)
    if (Object.keys(clientErrors).length > 0) return

    setSubmitting(true)
    try {
      await onSubmit({
        company: form.company.trim(),
        role: form.role.trim(),
        platform: form.platform as JobPlatform,
        appliedOn: form.appliedOn,
        jobUrl: form.jobUrl.trim() || null,
        notes: form.notes.trim() || null,
      })
      if (!isEdit && !prefill) setForm(toFormState())
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) setErrors(err.fieldErrors)
      else setFormError(err instanceof Error ? err.message : 'Could not save the application.')
    } finally {
      setSubmitting(false)
    }
  }

  const field = (key: keyof FormState) => ({
    id: `${idPrefix}-${key}`,
    'aria-invalid': !!errors[key],
    'aria-describedby': errors[key] ? `${idPrefix}-${key}-error` : undefined,
  })
  const error = (key: keyof FormState) =>
    errors[key] && (
      <span id={`${idPrefix}-${key}-error`} className="field-error">
        {errors[key]}
      </span>
    )

  return (
    <form className="application-form" onSubmit={handleSubmit} noValidate aria-label={isEdit ? 'Edit application' : 'Add application'}>
      {formError && (
        <p className="alert" role="alert">
          {formError}
        </p>
      )}
      <div className="grid">
        <div className="field">
          <label htmlFor={`${idPrefix}-company`}>Company</label>
          <input {...field('company')} value={form.company} onChange={(e) => set('company', e.target.value)} />
          {error('company')}
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-role`}>Role</label>
          <input {...field('role')} value={form.role} onChange={(e) => set('role', e.target.value)} />
          {error('role')}
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-platform`}>Platform</label>
          <select
            {...field('platform')}
            value={form.platform}
            onChange={(e) => set('platform', e.target.value as JobPlatform | '')}
          >
            <option value="">Choose…</option>
            {PLATFORMS.map((p) => (
              <option key={p} value={p}>
                {PLATFORM_LABELS[p]}
              </option>
            ))}
          </select>
          {error('platform')}
        </div>
        <div className="field">
          <label htmlFor={`${idPrefix}-appliedOn`}>Applied on</label>
          <input
            {...field('appliedOn')}
            type="date"
            max={localToday()}
            value={form.appliedOn}
            onChange={(e) => set('appliedOn', e.target.value)}
          />
          {error('appliedOn')}
        </div>
        <div className="field span-2">
          <label htmlFor={`${idPrefix}-jobUrl`}>Job ad link (optional)</label>
          <input
            {...field('jobUrl')}
            type="url"
            placeholder="https://…"
            value={form.jobUrl}
            onChange={(e) => set('jobUrl', e.target.value)}
          />
          {error('jobUrl')}
        </div>
        <div className="field span-2">
          <label htmlFor={`${idPrefix}-notes`}>Notes (optional)</label>
          <textarea {...field('notes')} rows={2} value={form.notes} onChange={(e) => set('notes', e.target.value)} />
          {error('notes')}
        </div>
      </div>
      <div className="actions">
        {onCancel && (
          <button type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </button>
        )}
        <button type="submit" className="primary" disabled={submitting}>
          {submitting ? 'Saving…' : (submitLabel ?? (isEdit ? 'Save changes' : 'Add application'))}
        </button>
      </div>
    </form>
  )
}
