import { computed } from 'vue'
import type {
  MatchDetails,
  MatchParticipant,
  MatchSummary,
  PlayerProfile,
  ProfileSummoner,
  RankedQueueStats,
} from './contracts'
import { championName } from './ddragon'
import { formatSeconds, perMinute } from './format'
import { gameQueueName } from './labels'
import { whenReady } from './lib/timeout'
import { sendCommand } from './hub'
import { sameRiotId } from './riotId'
import { liveProfile } from './store'

/**
 * The Profile tab: the local player's Riot ID and level, ranks, champion mastery and match history, as the connector
 * publishes them (ProfileUpdated), plus the full details of games the phone opened, and the profiles of friends, party
 * members and champ-select teammates opened with View Profile, of teammates tapped in a game and of players found by
 * Riot ID (`players`). In a game's details enemies come without names or puuids (the connector drops them), so they
 * are always shown by their champion and can't be opened.
 */

export const summoner = computed<ProfileSummoner | null>(() => liveProfile.value?.summoner ?? null)
export const ranks = computed<RankedQueueStats[]>(() => liveProfile.value?.ranks ?? [])
export const mastery = computed(() => liveProfile.value?.mastery ?? null)
export const matches = computed<MatchSummary[]>(() => liveProfile.value?.matches ?? [])

export function matchById(gameId: number | null | undefined): MatchSummary | null {
  return (gameId != null && matches.value.find((m) => m.gameId === gameId)) || null
}

/**
 * Another player's profile once 'LoadPlayerProfile' or 'SearchPlayer' brought it (friends, party, your champ-select team,
 * teammates in a game, a searched Riot ID); null until then.
 */
export function playerProfile(puuid: string | null | undefined): PlayerProfile | null {
  if (!puuid) return null
  const key = puuid.toLowerCase()
  return liveProfile.value?.players.find((p) => p.puuid.toLowerCase() === key) ?? null
}

/** One of that player's games (their match history); null when it isn't (or no longer) in it. */
export function playerMatchById(puuid: string | null | undefined, gameId: number | null | undefined): MatchSummary | null {
  return (gameId != null && playerProfile(puuid)?.matches.find((m) => m.gameId === gameId)) || null
}

/**
 * A game's teams once 'LoadMatchDetails' brought them; null until then. With `puuid`, one of that player's games, seen
 * from their side.
 */
export function detailsFor(gameId: number | null | undefined, puuid?: string | null): MatchDetails | null {
  if (gameId == null) return null
  const list = puuid ? playerProfile(puuid)?.details : liveProfile.value?.details
  return list?.find((d) => d.gameId === gameId) ?? null
}

// --- Commands -----------------------------------------------------------------------------------------------
// Silent: the views show refusals themselves. What a command asks for arrives later with a ProfileUpdated, so each
// load waits for it (bounded) after the connector said yes.

type Outcome = { ok: true } | { ok: false; error: string }

/** The connector's answer as an outcome; no answer at all (offline, timed out) gets a generic refusal. */
function outcome(result: Awaited<ReturnType<typeof sendCommand>>): Outcome {
  return result?.success ? { ok: true } : { ok: false, error: result?.error || "The PC didn't answer. Try again." }
}

/**
 * Ask for a fresh profile (sent whenever the tab opens; the connector rereads the client at most every
 * ProfileRefreshIntervalMs and otherwise republishes what it has). The tab shows what it has meanwhile.
 */
export function refreshProfile() {
  void sendCommand({ type: 'RefreshProfile' }, { silent: true })
}

/** How long a game's details may take to arrive once asked for (the connector reads one game from the client). */
export const MATCH_DETAILS_TIMEOUT_MS = 12_000

/** Ask for a game's details; they arrive with the next ProfileUpdated (`detailsFor`). */
export async function loadMatchDetails(gameId: number, puuid?: string | null): Promise<Outcome> {
  const command = puuid ? { type: 'LoadMatchDetails' as const, gameId, puuid } : { type: 'LoadMatchDetails' as const, gameId }
  return outcome(await sendCommand(command, { silent: true }))
}

/** A player's profile reads four endpoints, and the client takes ~2 s for the history of someone it hasn't loaded. */
export const PLAYER_PROFILE_TIMEOUT_MS = 15_000

/**
 * Ask for another player's profile; it arrives with the next ProfileUpdated (`playerProfile`). The connector re-checks
 * that they are a friend, in your party or on your champ-select team, and refuses anyone else.
 */
export async function loadPlayerProfile(puuid: string): Promise<Outcome> {
  return outcome(await sendCommand({ type: 'LoadPlayerProfile', puuid }, { silent: true }))
}

/**
 * A line of a game you can open as a profile: a named teammate the connector sent a puuid for (never an enemy: they come
 * without both), not you and not the player whose game it is.
 */
export function canOpenPlayer(p: MatchParticipant): p is MatchParticipant & { puuid: string } {
  return !!p.puuid && !!p.gameName && !p.isMe
}

/** Whether a Riot ID is your own (the search then just shows your profile). */
export function isOwnRiotId(gameName: string, tagLine: string): boolean {
  const me = summoner.value
  return !!me && sameRiotId(me, { gameName, tagLine })
}

/** A searched player's profile on the phone: by Riot ID, the most recently opened first. */
function playerByRiotId(gameName: string, tagLine: string): PlayerProfile | null {
  return liveProfile.value?.players.find((p) => sameRiotId(p.summoner, { gameName, tagLine })) ?? null
}

/** A search is a Riot ID lookup plus a whole profile read (four endpoints): a little longer than opening a profile. */
const PLAYER_SEARCH_TIMEOUT_MS = 20_000

/**
 * Look a Riot ID up and load that player's profile ('SearchPlayer'); resolves with their puuid once the profile is on
 * the phone, or the connector's refusal ("No player with that Riot ID."). The connector puts the found profile first in
 * `players`: a first entry that is new since the search was sent (another player, or the same one read again) is the
 * answer — compared with what was first before, not with the phone's clock, which may differ from the PC's. When
 * nothing changed (that profile was already first and read < 30 s ago), the Riot ID picks it.
 */
export async function searchPlayer(gameName: string, tagLine: string): Promise<{ ok: true; puuid: string } | { ok: false; error: string }> {
  const firstBefore = liveProfile.value?.players[0] ?? null
  const sent = outcome(await sendCommand({ type: 'SearchPlayer', gameName, tagLine }, { silent: true, timeoutMs: PLAYER_SEARCH_TIMEOUT_MS }))
  if (!sent.ok) return sent
  const found = await whenReady(() => {
    const first = liveProfile.value?.players[0] ?? null
    const isNew = !!first && (!firstBefore || first.puuid !== firstBefore.puuid || first.loadedAt !== firstBefore.loadedAt)
    return isNew ? first : playerByRiotId(gameName, tagLine)
  }, PLAYER_SEARCH_TIMEOUT_MS)
  return found ? { ok: true, puuid: found.puuid } : { ok: false, error: "This profile didn't come through. Try again." }
}

// --- Labels -------------------------------------------------------------------------------------------------

/** XP progress in the current level, 0–1; null when the client didn't say how much the level needs. */
export function xpProgress(s: ProfileSummoner): number | null {
  if (s.xpForNextLevel <= 0) return null
  return Math.min(1, Math.max(0, s.xpSinceLastLevel / s.xpForNextLevel))
}

/** "Victory" / "Defeat" / "Remake" / "Aborted". */
export function resultLabel(result: string): string {
  switch (result) {
    case 'win':
      return 'Victory'
    case 'loss':
      return 'Defeat'
    case 'remake':
      return 'Remake'
    case 'aborted':
      return 'Aborted'
    default:
      return 'Game'
  }
}

/** "Ranked Solo/Duo", "Practice Tool", "Arena"… */
export function matchQueue(m: MatchSummary): string {
  return gameQueueName(m.queueId, m.gameMode, m.gameType)
}

/** "31:24". */
export function matchDuration(m: MatchSummary): string {
  return formatSeconds(m.durationSeconds)
}

/** "182 CS (5.8)" — creep score and per minute; just "182 CS" for games under a minute. */
export function csLine(p: MatchParticipant, durationSeconds: number): string {
  const rate = perMinute(p.cs, durationSeconds)
  return rate ? `${p.cs} CS (${rate})` : `${p.cs} CS`
}

/**
 * A player in a game's details: their game name on your own team, the champion for everyone else (the connector never
 * sends enemy names; never show anything else for them).
 */
export function participantName(p: MatchParticipant): string {
  return p.gameName || championName(p.championId) || 'Unknown'
}
