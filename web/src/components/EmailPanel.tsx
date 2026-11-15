import { useState } from 'react'
import { disconnectGmail, startGmailConnect, syncEmail } from '../api/email'
import type { EmailStatus } from '../api/types'
import { formatDateTime } from '../lib/dates'
import { describeSync } from '../lib/email'

interface Props {
  status: EmailStatus
  /** Called after anything that may have changed applications or the status. */
  onChanged: () => void
  /** Sends the browser to Google; replaceable in tests. */
  navigate?: (url: string) => void
}

/** "Connect Gmail" / "Sync now": OppsView filling itself in from your inbox. */
export function EmailPanel({ status, onChanged, navigate = (url) => window.location.assign(url) }: Props) {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : 'Something went wrong.' })
    } finally {
      setBusy(false)
    }
  }

  const connect = () => run(async () => navigate(await startGmailConnect()))

  const sync = () =>
    run(async () => {
      try {
        const summary = await syncEmail()
        setMessage({ ok: true, text: describeSync(summary) })
      } finally {
        // Refresh even when the sync failed: the server may have just found
        // that Gmail access was revoked, and the panel should say so.
        onChanged()
      }
    })

  const disconnect = () => {
    if (!window.confirm('Disconnect Gmail? OppsView will stop reading your email. Your applications stay as they are.')) return
    void run(async () => {
      await disconnectGmail()
      onChanged()
    })
  }

  if (!status.available) {
    return (
      <p className="muted">
        Email import isn't set up on this server yet. See "Connecting Gmail" in the README.
      </p>
    )
  }

  return (
    <div className="email-panel">
      {!status.connected ? (
        <>
          <p>
            Connect Gmail and OppsView adds applications and updates their status from your job emails. It only ever
            <strong> reads</strong> your email.
          </p>
          <button type="button" className="primary" onClick={connect} disabled={busy}>
            Connect Gmail
          </button>
        </>
      ) : status.needsReconnect ? (
        <>
          <p className="alert" role="alert">
            Google stopped OppsView's access to {status.emailAddress}. While the Google app is in Testing mode this
            happens every 7 days. Please connect again.
          </p>
          <button type="button" className="primary" onClick={connect} disabled={busy}>
            Reconnect Gmail
          </button>
        </>
      ) : (
        <div className="email-connected">
          <p>
            Connected to <strong>{status.emailAddress}</strong>
            <br />
            <span className="muted">
              {status.lastSyncedAt ? `Last synced ${formatDateTime(status.lastSyncedAt)}` : 'Not synced yet'}
            </span>
          </p>
          <div className="actions">
            <button type="button" className="link danger" onClick={disconnect} disabled={busy}>
              Disconnect
            </button>
            <button type="button" className="primary" onClick={sync} disabled={busy}>
              {busy ? 'Syncing…' : 'Sync now'}
            </button>
          </div>
        </div>
      )}
      {/* In the reconnect state the box above already explains the problem. */}
      {message && !(status.needsReconnect && !message.ok) && (
        <p className={message.ok ? 'notice' : 'alert'} role="status">
          {message.text}
        </p>
      )}
    </div>
  )
}
