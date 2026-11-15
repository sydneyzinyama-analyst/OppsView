import { useEffect, useState } from 'react'
import { getSavedMatch } from '../api/matching'
import type { SavedMatch } from '../api/types'
import { MatchResultView } from './MatchResultView'

/** The CV match saved on an application, shown in its detail panel. */
export function SavedMatchView({ applicationId }: { applicationId: string }) {
  const [saved, setSaved] = useState<{ id: string; match: SavedMatch | null } | null>(null)

  useEffect(() => {
    let cancelled = false
    getSavedMatch(applicationId).then(
      (match) => !cancelled && setSaved({ id: applicationId, match }),
      () => !cancelled && setSaved({ id: applicationId, match: null }),
    )
    return () => {
      cancelled = true
    }
  }, [applicationId])

  if (!saved || saved.id !== applicationId || !saved.match) return null
  return (
    <div className="saved-match">
      <h3>CV match</h3>
      <MatchResultView result={saved.match.result} />
    </div>
  )
}
