import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getAnalytics, type Analytics } from '../api/analytics'
import { PLATFORM_LABELS } from '../api/types'
import { BarList, ColumnChart, StatTile } from '../components/charts'

const percent = (share: number) => `${Math.round(share * 100)}%`
const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

const STAGE_LABELS: Record<string, string> = {
  Applied: 'Applied',
  Viewed: 'Viewed',
  Interview: 'Interview',
  Offer: 'Offer',
}

/** The dashboard: how the job hunt is going, from the API's /analytics numbers. */
export function InsightsPage() {
  const [data, setData] = useState<Analytics | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getAnalytics(12).then(
      (d) => !cancelled && setData(d),
      (e: Error) => !cancelled && setError(e.message),
    )
    return () => {
      cancelled = true
    }
  }, [])

  if (error)
    return (
      <main className="container">
        <p className="alert" role="alert">
          {error}
        </p>
      </main>
    )
  if (!data)
    return (
      <main className="container">
        <p className="muted">Loading…</p>
      </main>
    )

  const { totals, sources } = data
  if (totals.applications === 0)
    return (
      <main className="container">
        <section className="card">
          <h2>Insights</h2>
          <p className="empty">
            No applications yet. <Link to="/">Add one</Link> or connect Gmail, and your numbers will appear here.
          </p>
        </section>
      </main>
    )

  const automatic = sources.email / Math.max(1, sources.manual + sources.email)

  return (
    <main className="container insights">
      <section className="stat-row" aria-label="Summary">
        <StatTile label="Applications" value={String(totals.applications)} hint={`${totals.active} still open`} />
        <StatTile label="Response rate" value={percent(totals.responseRate)} hint="got an interview, offer or rejection" />
        <StatTile
          label="Days to a reply"
          value={totals.averageDaysToResponse === null ? '–' : String(totals.averageDaysToResponse)}
          hint="on average, from applying"
        />
        <StatTile label="Interviews" value={String(totals.interviews)} hint={plural(totals.offers, 'offer')} />
      </section>

      <section className="card">
        <ColumnChart
          title="Applications per week (last 12 weeks)"
          tableHeadings={['Week starting', 'Applications']}
          items={data.perWeek.map((w) => ({
            label: shortDate(w.weekStart),
            value: w.applications,
            detail: `week of ${shortDate(w.weekStart)}`,
          }))}
          empty="No applications in the last 12 weeks."
        />
      </section>

      <div className="chart-grid">
        <section className="card">
          <BarList
            title="Response rate by platform"
            tableHeadings={['Platform', 'Response rate']}
            max={1}
            items={data.byPlatform.map((p) => ({
              label: PLATFORM_LABELS[p.platform],
              value: p.responseRate,
              display: percent(p.responseRate),
              detail: `${p.responses} of ${p.applications} replied`,
            }))}
          />
        </section>

        <section className="card">
          <BarList
            title="How far applications get"
            tableHeadings={['Stage', 'Applications that reached it']}
            max={data.funnel[0]?.reached || 1}
            items={data.funnel.map((f) => ({ label: STAGE_LABELS[f.stage] ?? f.stage, value: f.reached }))}
          />
          <BarList
            title="Where rejections happen"
            tableHeadings={['Rejected after', 'Applications']}
            items={data.rejectedAt.map((r) => ({ label: `After ${r.from === 'Applied' ? 'applying' : r.from.toLowerCase()}`, value: r.count }))}
            empty="No rejections recorded."
          />
        </section>

        <section className="card">
          <BarList
            title="Skills most often missing (from your CV matches)"
            tableHeadings={['Skill', 'Times missing']}
            items={data.topMissingSkills.map((s) => ({ label: s.skill, value: s.count, detail: `missing in ${plural(s.count, 'match')}` }))}
            empty="Match your CV against some job ads to see which skills come up most."
          />
        </section>

        <section className="card">
          <h3 className="chart-title">Waiting for news</h3>
          <table className="waiting-table">
            <thead>
              <tr>
                <th>Stage</th>
                <th>Applications</th>
                <th>Average wait</th>
                <th>Over 2 weeks</th>
              </tr>
            </thead>
            <tbody>
              {data.waiting.map((w) => (
                <tr key={w.stage}>
                  <td>{STAGE_LABELS[w.stage] ?? w.stage}</td>
                  <td>{w.count}</td>
                  <td>{w.count ? `${w.averageDays} days` : '–'}</td>
                  <td>{w.waitingOver14Days}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="muted small">
            OppsView filled in {percent(automatic)} of your applications from email ({sources.email} of{' '}
            {sources.manual + sources.email}).
          </p>
        </section>
      </div>
    </main>
  )
}

/** "2026-11-16" -> "16 Nov" */
function shortDate(isoDate: string) {
  const [y, m, d] = isoDate.split('-').map(Number)
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })
}
