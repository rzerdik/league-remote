import type { MatchParticipant, ProfileSummoner } from './contracts'

/** A Riot ID in parts, as friends, friend requests and profiles carry it. */
interface RiotIdParts {
  gameName: string
  tagLine: string
}

/**
 * "Name #TAG", as the client writes a Riot ID (friends and friend requests). The space is a no-break space, so a
 * wrapping line never leaves "#TAG" alone at the start of the next one.
 */
export function riotId(p: RiotIdParts): string {
  return p.tagLine ? `${p.gameName} #${p.tagLine}` : p.gameName
}

/**
 * A player name from the connector (lobby members, invites: "Name#TAG") written the way riotId() writes one, so the
 * same player reads the same everywhere.
 */
export function playerName(name: string): string {
  const hash = name.lastIndexOf('#')
  return hash > 0 ? riotId({ gameName: name.slice(0, hash), tagLine: name.slice(hash + 1) }) : name
}

/**
 * A connector player name ("Name#TAG") as lists and selection rows show it: the game name only. The full Riot ID
 * (playerName / riotId) is for detail sheets, where telling two "Name"s apart matters.
 */
export function playerShortName(name: string): string {
  const hash = name.lastIndexOf('#')
  return hash > 0 ? name.slice(0, hash) : name
}

/** The Riot ID the way connector names travel in `nav.player` ("Name#TAG"). */
export function participantRiotId(p: MatchParticipant): string {
  return p.tagLine ? `${p.gameName}#${p.tagLine}` : p.gameName
}

/** "Nova", "#EUW" — the Riot ID split for the profile header (the tag is drawn secondary). */
export function riotIdParts(s: ProfileSummoner): { name: string; tag: string } {
  return { name: s.gameName, tag: s.tagLine ? `#${s.tagLine}` : '' }
}

/**
 * Riot IDs match whatever the case (League looks them up that way) and Unicode form (NFKC: a composed and a decomposed
 * "ó" are one letter), ignoring spaces around them.
 */
export function sameRiotId(a: RiotIdParts, b: RiotIdParts): boolean {
  const key = (r: RiotIdParts) => `${r.gameName.trim()}#${r.tagLine.trim()}`.normalize('NFKC').toLowerCase()
  return key(a) === key(b)
}

/**
 * Splits what the user typed into a Riot ID: "Name#TAG" pasted into the name field works too. Both parts trimmed,
 * the tag without '#'.
 */
export function parseRiotId(gameName: string, tagLine: string): RiotIdParts {
  const hash = gameName.indexOf('#')
  if (hash >= 0 && !tagLine.trim()) return { gameName: gameName.slice(0, hash).trim(), tagLine: gameName.slice(hash + 1).trim() }
  return { gameName: gameName.trim(), tagLine: tagLine.trim().replace(/^#/, '') }
}
