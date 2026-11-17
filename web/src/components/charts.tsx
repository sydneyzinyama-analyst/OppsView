/**
 * Small, dependency-free charts for the Insights page, drawn with HTML and CSS.
 *
 * Every chart here shows ONE measure, so it uses one colour (--viz-series-1)
 * and needs no legend: the chart's title names what's measured. Exact values
 * are shown as text next to the bars, in a hover / keyboard-focus tooltip,
 * and in a visually hidden table for screen readers.
 */

export interface BarItem {
  label: string
  value: number
  /** What's written next to the bar (defaults to the value). */
  display?: string
  /** Extra detail in the tooltip, e.g. "2 of 4 replied". */
  detail?: string
}

interface ChartProps {
  title: string
  /** Column headings for the screen-reader table. */
  tableHeadings: [string, string]
  items: BarItem[]
  /** The value that fills the whole track (defaults to the largest value). */
  max?: number
  empty?: string
}

/** Horizontal bars: label, bar, value. For rankings and shares. */
export function BarList({ title, tableHeadings, items, max, empty = 'Nothing to show yet.' }: ChartProps) {
  const top = max ?? Math.max(1, ...items.map((i) => i.value))
  return (
    <figure className="chart">
      <figcaption className="chart-title">{title}</figcaption>
      {items.length === 0 ? (
        <p className="muted small">{empty}</p>
      ) : (
        <>
          <ul className="bar-list" aria-hidden="true">
            {items.map((item) => (
              <li key={item.label} className="bar-row">
                <span className="bar-label">{item.label}</span>
                <span
                  className="bar-track"
                  tabIndex={0}
                  data-tip={tip(item)}
                  aria-label={tip(item)}
                >
                  <span className="bar-fill" style={{ width: `${Math.max(0, (item.value / top) * 100)}%` }} />
                </span>
                <span className="bar-value">{item.display ?? item.value}</span>
              </li>
            ))}
          </ul>
          <DataTable headings={tableHeadings} items={items} />
        </>
      )}
    </figure>
  )
}

/** Vertical columns along a time axis. Only the highest and the latest columns are labelled. */
export function ColumnChart({ title, tableHeadings, items, empty = 'Nothing to show yet.' }: ChartProps) {
  const top = Math.max(1, ...items.map((i) => i.value))
  const highest = items.reduce((best, item, i) => (item.value > items[best].value ? i : best), 0)
  return (
    <figure className="chart">
      <figcaption className="chart-title">{title}</figcaption>
      {items.every((i) => i.value === 0) ? (
        <p className="muted small">{empty}</p>
      ) : (
        <>
          <div className="columns" aria-hidden="true">
            {items.map((item, i) => (
              <div key={item.label} className="column" tabIndex={0} data-tip={tip(item)} aria-label={tip(item)}>
                <span className="column-value">
                  {(i === highest || i === items.length - 1) && item.value > 0 ? item.value : ''}
                </span>
                <span className="column-fill" style={{ height: `${(item.value / top) * 100}%` }} />
                <span className="column-label">{item.label}</span>
              </div>
            ))}
          </div>
          <DataTable headings={tableHeadings} items={items} />
        </>
      )}
    </figure>
  )
}

/** A headline number. */
export function StatTile({ label, value, hint, tone }: { label: string; value: string; hint?: string; tone?: string }) {
  return (
    <div className={tone ? `stat-tile tone-${tone}` : 'stat-tile'}>
      <span className="stat-value">{value}</span>
      <span className="stat-label">{label}</span>
      {hint && <span className="muted small">{hint}</span>}
    </div>
  )
}

function tip(item: BarItem) {
  return `${item.label}: ${item.display ?? item.value}${item.detail ? ` (${item.detail})` : ''}`
}

function DataTable({ headings, items }: { headings: [string, string]; items: BarItem[] }) {
  return (
    <table className="visually-hidden">
      <thead>
        <tr>
          <th>{headings[0]}</th>
          <th>{headings[1]}</th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.label}>
            <td>{item.label}</td>
            <td>
              {item.display ?? item.value}
              {item.detail ? ` (${item.detail})` : ''}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
