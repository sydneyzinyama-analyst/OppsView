import { useEffect, useState } from 'react'
import { NavLink } from 'react-router-dom'
import { countReminders } from '../api/reminders'
import { BellIcon } from './icons'

/** How often the bell checks for new reminders. The notifier sends them at most once a minute. */
export const BELL_REFRESH_MS = 60_000

/** The Reminders menu item: a bell, a link to the reminders page and the number waiting. */
export function ReminderBell({ refreshKey = 0 }: { refreshKey?: number }) {
  const [count, setCount] = useState(0)

  useEffect(() => {
    let cancelled = false
    const load = () =>
      countReminders().then(
        (n) => !cancelled && setCount(n),
        () => {}, // the bell is a nice-to-have: never break the page over it
      )
    void load()
    const timer = setInterval(load, BELL_REFRESH_MS)
    return () => {
      cancelled = true
      clearInterval(timer)
    }
  }, [refreshKey])

  const label = count === 0 ? 'Reminders' : `Reminders, ${count} waiting`
  return (
    <NavLink to="/reminders" className="bell" aria-label={label} title={label}>
      <BellIcon />
      <span aria-hidden="true">Reminders</span>
      {count > 0 && (
        <span className="bell-count" aria-hidden="true">
          {count > 9 ? '9+' : count}
        </span>
      )}
    </NavLink>
  )
}
