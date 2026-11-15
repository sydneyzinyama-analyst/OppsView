/** Colour band for a match score: green from 75, amber from 50, red below. */
export function scoreClass(score: number): string {
  return score >= 75 ? 'score-high' : score >= 50 ? 'score-mid' : 'score-low'
}
