import type { MatchResult } from '../api/types'
import { scoreClass } from '../lib/score'

/** A match result: the score, then matched and missing skills by importance. */
export function MatchResultView({ result }: { result: MatchResult }) {
  const { score, required, niceToHave, extraInCv, summary } = result
  return (
    <div className="match-result" aria-label="Match result">
      <div className="match-score">
        {score === null ? (
          <span className="score-value muted">–</span>
        ) : (
          <span className={`score-value ${scoreClass(score)}`}>{score}%</span>
        )}
        <p>{summary}</p>
      </div>
      <SkillList title="Required" group={required} />
      <SkillList title="Nice to have" group={niceToHave} />
      {extraInCv.length > 0 && (
        <div className="skill-block">
          <h3>Also on your CV</h3>
          <p className="muted small">Skills you have that this ad doesn't ask for.</p>
          <ul className="chips">
            {extraInCv.map((s) => (
              <li key={s} className="chip">
                {s}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function SkillList({ title, group }: { title: string; group: MatchResult['required'] }) {
  if (group.matched.length + group.missing.length === 0) return null
  return (
    <div className="skill-block">
      <h3>
        {title}{' '}
        <span className="count">
          {group.matched.length} of {group.matched.length + group.missing.length}
        </span>
      </h3>
      <ul className="chips">
        {group.matched.map((s) => (
          <li key={s} className="chip chip-have">
            <span aria-hidden="true">✓ </span>
            {s}
            <span className="visually-hidden"> (you have this)</span>
          </li>
        ))}
        {group.missing.map((s) => (
          <li key={s} className="chip chip-missing">
            <span aria-hidden="true">✗ </span>
            {s}
            <span className="visually-hidden"> (missing)</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
