// Mirror of src/LeagueRemote.Contracts/RiotIds.cs.

/** RiotIds: Riot's own Riot ID rules, and the longest puuid the commands accept. */
export const RiotIds = { minGameName: 3, maxGameName: 16, minTagLine: 3, maxTagLine: 5, maxPuuidLength: 128 } as const

const hasControlChars = (s: string) => /[\u0000-\u001f\u007f-\u009f]/.test(s)

/** Null when valid (trim first; spaces inside are fine, '#' is not). Same messages as the connector. */
export function validateGameName(g: string | null | undefined): string | null {
  if (!g || !g.trim()) return 'Enter a game name.'
  if (g.length < RiotIds.minGameName || g.length > RiotIds.maxGameName)
    return `Game names are ${RiotIds.minGameName}–${RiotIds.maxGameName} characters.`
  return hasControlChars(g) || g.includes('#') || g !== g.trim() ? "That game name isn't valid." : null
}

/** Null when valid: 3–5 letters or digits. */
export function validateTagLine(t: string | null | undefined): string | null {
  if (!t || !t.trim()) return 'Enter a tag line (the part after #).'
  if (t.length < RiotIds.minTagLine || t.length > RiotIds.maxTagLine)
    return `Tag lines are ${RiotIds.minTagLine}–${RiotIds.maxTagLine} characters.`
  return /^[\p{L}\p{Nd}]+$/u.test(t) ? null : 'Tag lines are letters and digits only.'
}

/** RiotIds.IsPuuid: letters, digits, '-' and '_' only. */
export const isPuuid = (p: string | null | undefined) =>
  !!p && p.length <= RiotIds.maxPuuidLength && /^[A-Za-z0-9_-]+$/.test(p)
