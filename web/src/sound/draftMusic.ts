import type { ChampSelectState, ClientMusicKey } from '../contracts'
import type { MusicTrack } from './music'

/**
 * Champion Select music as the client plays it (`rcp-fe-lol-champ-select` music mixin: `startChampSelectMusic`,
 * `processDraftMusicChange`; read Oct 2026, client 16.21):
 *
 * - **Draft-like modes** (sequential picks, blind pick with bans, "random with bans") layer adaptive music. Declare
 *   Intent loops `pickintent`; the bans play `ban-base` plus intensity layers by bans done (simultaneous bans count as
 *   4, i.e. all layers), preceded once by the `pickintent-to-ban` stinger; the picks play `pick-base` plus up to four
 *   intensity layers as picks lock (blind with bans counts as 10), preceded by the `ban-to-pick` stinger; finalization
 *   plays the 62 s `finalization-60sec` track once, joined at 60 − seconds left (with the stinger when joined late).
 *   "Random with bans" uses the map's ban-phase track for the bans and its champ select track at the end.
 * - **Other modes** play the map's `champ-select-background-sound` once with a 3 s fade-in (not looped); All Random
 *   starts it so that its 76.8 s mark lands on the end of champ select (`startMusicSyncedToEndOfChampSelect`).
 *
 * The client's volumes are 0.37 (full), 0.2775 and 0.185 of its music channel; here 1, 0.75 and 0.5.
 */

/** The client plays a few stingers only once per stretch; this remembers which (one per champ select). */
export interface DraftMusicFlags {
  intentToBan: boolean
  banToPick: boolean
  finalization: boolean
}

export function newDraftMusicFlags(): DraftMusicFlags {
  return { intentToBan: false, banToPick: false, finalization: false }
}

/** How the draft is shaped, from the turns the connector sends. */
interface DraftShape {
  layered: boolean
  blindWithBans: boolean
  randomWithBans: boolean
  simultaneousBans: boolean
}

function shape(cs: ChampSelectState): DraftShape {
  const turns = cs.turns
  const bans = turns.filter((t) => t.type === 'ban')
  const picks = turns.filter((t) => t.type === 'pick')
  const hasBans = bans.length > 0
  const sequentialPicks = picks.some((t) => t.team === 'ally' || t.team === 'enemy')
  const blindWithBans = hasBans && picks.length > 0 && !sequentialPicks
  const randomWithBans = hasBans && picks.length === 0
  return {
    layered: sequentialPicks || blindWithBans || randomWithBans,
    blindWithBans,
    randomWithBans,
    simultaneousBans: bans.some((t) => t.team === 'both'),
  }
}

/** All Random's track is timed to end with champ select at this mark (ms in the client). */
const ARAM_SYNC_S = 76.831
/** The client adds the finalization it expects after ARAM's ban/pick phase (`AramFinalizationDurationOverrides`, 45 s). */
const ARAM_FINALIZATION_S = 45

export interface DraftMusic {
  tracks: MusicTrack[]
  stinger: ClientMusicKey | null
}

/** The tracks for this moment of champ select and the stinger to play on the way in, if any. Updates `flags`. */
export function champSelectMusic(cs: ChampSelectState, gameMode: string | null | undefined, flags: DraftMusicFlags): DraftMusic {
  const aram = gameMode === 'ARAM'
  const timeLeft = Math.max(0, cs.timeLeftMs / 1000)
  const s = shape(cs)

  if (!s.layered) {
    if (aram) {
      const remaining = cs.timerPhase === 'BAN_PICK' ? timeLeft + ARAM_FINALIZATION_S : timeLeft
      return {
        tracks: [
          {
            key: 'musicAram',
            volume: 1,
            loop: false,
            master: true,
            fadeInMs: 3000,
            offset: Math.max(ARAM_SYNC_S - remaining, 0) || undefined,
            delayMs: Math.max(remaining - ARAM_SYNC_S, 0) * 1000 || undefined,
          },
        ],
        stinger: null,
      }
    }
    return { tracks: [{ key: 'musicBlindPick', volume: 1, loop: false, master: true, fadeInMs: 3000 }], stinger: null }
  }

  const turns = cs.turns
  const active = turns.find((t) => t.isInProgress && !t.completed) ?? null
  const bansDone = s.simultaneousBans
    ? 4
    : turns.filter((t) => t.type === 'ban' && t.completed).reduce((n, t) => n + Math.max(1, t.cellIds.length), 0)
  const picksDone = s.blindWithBans
    ? 10
    : cs.myTeam.filter((m) => m.isLocked).length + cs.theirTeam.filter((m) => m.isLocked).length
  const banning = active ? active.type === 'ban' || active.type === 'reveal' : picksDone === 0
  let stinger: ClientMusicKey | null = null

  if (cs.timerPhase === 'PLANNING') {
    flags.intentToBan = false
    return { tracks: [{ key: 'musicDraftIntent', volume: 1, master: true, stopDelayMs: 1578 }], stinger: null }
  }

  if (cs.timerPhase === 'BAN_PICK' && banning) {
    if (picksDone === 0 && !flags.intentToBan) {
      stinger = 'musicDraftIntentToBan'
      flags.intentToBan = true
    }
    flags.banToPick = false
    flags.finalization = false
    if (s.randomWithBans) return { tracks: [{ key: 'musicAramBan', volume: 1, master: true }], stinger }
    const v = new Map<ClientMusicKey, number>([['musicDraftBan', 1]])
    if (bansDone >= 1) v.set('musicDraftBanLayer1', 0.5)
    if (bansDone >= 2) v.set('musicDraftBanLayer1', 0.75).set('musicDraftBanLayer2', 0.5)
    if (bansDone >= 3) v.set('musicDraftBanLayer1', 1).set('musicDraftBanLayer2', 0.75)
    if (bansDone >= 4) v.set('musicDraftBanLayer2', 1)
    return {
      tracks: [...v].map(([key, volume]) =>
        key === 'musicDraftBan' ? { key, volume, master: true, delayMs: 1578 } : { key, volume },
      ),
      stinger,
    }
  }

  if (cs.timerPhase === 'BAN_PICK') {
    if ((picksDone === 0 || s.blindWithBans) && !flags.banToPick) {
      stinger = 'musicDraftBanToPick'
      flags.banToPick = true
    }
    const v = new Map<ClientMusicKey, number>([['musicDraftPick', 1]])
    if (picksDone >= 1) v.set('musicDraftPickLayer1', 0.5)
    if (picksDone >= 2) v.set('musicDraftPickLayer2', 0.5)
    if (picksDone >= 3) v.set('musicDraftPickLayer1', 0.75)
    if (picksDone >= 4) v.set('musicDraftPickLayer2', 0.75).set('musicDraftPickLayer3', 0.5)
    if (picksDone >= 5) v.set('musicDraftPickLayer1', 1)
    if (picksDone >= 6) v.set('musicDraftPickLayer2', 1).set('musicDraftPickLayer3', 0.75).set('musicDraftPickLayer4', 0.5)
    if (picksDone >= 7) v.set('musicDraftPickLayer3', 1).set('musicDraftPickLayer4', 0.75)
    if (picksDone >= 8) v.set('musicDraftPickLayer4', 1)
    return { tracks: [...v].map(([key, volume]) => ({ key, volume, master: key === 'musicDraftPick' })), stinger }
  }

  if (cs.timerPhase === 'FINALIZATION') {
    const into = Math.max(0, 60 - Math.floor(timeLeft))
    if (!flags.finalization && into > 0) stinger = 'musicDraftBanToPick'
    flags.finalization = true
    const key: ClientMusicKey = s.randomWithBans ? (aram ? 'musicAram' : 'musicBlindPick') : 'musicDraftFinal'
    return { tracks: [{ key, volume: 1, loop: false, master: true, offset: into || undefined }], stinger }
  }

  return { tracks: [], stinger: null }
}
