import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { dismissReminder, getReminderSettings, listReminders, markReminderDone, snoozeReminder } from '../api/reminders'
import type { Reminder, ReminderKind, ReminderSettings } from '../api/types'
import { useRefreshBell } from '../components/bell'
import { ReminderSettingsForm } from '../components/ReminderSettingsForm'
import { formatDate } from '../lib/dates'

const KIND_LABELS: Record<ReminderKind, string> = {
  FOLLOW_UP: 'Follow up',
  MOVE_ON: 'Time to move on?',
  AFTER_INTERVIEW: 'After the interview',
  REPLY_TO_OFFER: 'Offer waiting',
}

/** Follow-up reminders from the notifier, and the settings that control them. */
export function RemindersPage() {
  const refreshBell = useRefreshBell()
  const [reminders, setReminders] = useState<Reminder[] | null>(null)
  const [settings, setSettings] = useState<ReminderSettings | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    listReminders().then(
      (list) => !cancelled && setReminders(list),
      (e: Error) => !cancelled && setError(e.message),
    )
    getReminderSettings().then(
      (s) => !cancelled && setSettings(s),
      () => {},
    )
    return () => {
      cancelled = true
    }
  }, [])

  async function act(id: string, action: () => Promise<void>) {
    setError(null)
    try {
      await action()
      setReminders((list) => list?.filter((r) => r.id !== id) ?? null)
      refreshBell()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.')
    }
  }

  return (
    <main className="container">
      <section className="card">
        <h2>
          Reminders {reminders && reminders.length > 0 && <span className="count">{reminders.length}</span>}
        </h2>
        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
        {reminders === null ? (
          !error && <p className="muted">Loading…</p>
        ) : reminders.length === 0 ? (
          <p className="empty">Nothing to follow up on right now. OppsView will remind you when it's time.</p>
        ) : (
          <ul className="reminders">
            {reminders.map((r) => (
              <li key={r.id} className="reminder">
                <div>
                  <div className="reminder-title">
                    <strong>{KIND_LABELS[r.kind] ?? r.kind}</strong>
                    <span className="muted small"> · {r.company} – {r.role} · {formatDate(r.dueAt.slice(0, 10))}</span>
                  </div>
                  <p>{r.message}</p>
                  <Link className="link" to={`/?highlight=${r.applicationId}`}>
                    View application
                  </Link>
                </div>
                <div className="reminder-actions">
                  <button type="button" className="primary" onClick={() => act(r.id, () => markReminderDone(r.id))}>
                    Done
                  </button>
                  <button type="button" onClick={() => act(r.id, () => snoozeReminder(r.id, 3))}>
                    In 3 days
                  </button>
                  <button type="button" className="link" onClick={() => act(r.id, () => dismissReminder(r.id))}>
                    Dismiss
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card">
        <h2>When to remind me</h2>
        {settings ? <ReminderSettingsForm settings={settings} onSaved={setSettings} /> : <p className="muted">Loading…</p>}
      </section>
    </main>
  )
}
