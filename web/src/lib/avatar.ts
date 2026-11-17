/** Up to two letters for a company's avatar: "Standard Bank" -> "SB", "Takealot" -> "TA". */
export function initials(name: string): string {
  const words = name.trim().split(/[\s\-_.&]+/).filter((w) => /[a-z0-9]/i.test(w))
  if (words.length === 0) return '?'
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase()
  return (words[0][0] + words[1][0]).toUpperCase()
}

/** Six avatar tints; a company always gets the same one (hash of its name). */
export const AVATAR_TINTS = 6

export function avatarTint(name: string): number {
  let hash = 0
  for (const ch of name.trim().toLowerCase()) hash = (hash * 31 + ch.charCodeAt(0)) | 0
  return Math.abs(hash) % AVATAR_TINTS
}
