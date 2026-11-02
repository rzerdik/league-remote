import { computed } from 'vue'
import type { MatchParticipant, PostGameMastery, PostGameRank, PostGameSummary } from './contracts'
import { championName } from './ddragon'
import { formatCount, ordinal, plural } from './format'
import { isPostGamePhase } from './gameflow'
import { gameQueueName, rankLabel } from './labels'
import { resultLabel } from './profile'
import { sendCommand } from './hub'
import { liveState } from './store'

/**
 * The client's post-game screen on the phone: the summary the connector sends with the state while the phase is
 * EndOfGame (`ClientState.postGame`), and its two actions, Play Again and Leave. Before the stats arrive
 * (WaitingForStats, PreEndOfGame: the PC shows the honor vote) the screen waits.
 */

/** The post-game phase, while there is one: the Home tab shows the post-game screen. */
export const postGamePhase = computed(() => {
  const phase = liveState.value?.gameflowPhase
  return phase && isPostGamePhase(phase) ? phase : null
})

/** The summary, only on the post-game screen itself. */
export const postGame = computed<PostGameSummary | null>(() =>
  liveState.value?.gameflowPhase === 'EndOfGame' ? (liveState.value.postGame ?? null) : null,
)

/** Play Again and Leave work only on the post-game screen (the connector refuses them elsewhere). */
export const canAnswer = computed(() => liveState.value?.gameflowPhase === 'EndOfGame')

export function myLine(s: PostGameSummary): MatchParticipant | null {
  for (const t of s.teams) {
    const me = t.participants.find((p) => p.isMe)
    if (me) return me
  }
  return null
}

/**
 * "Victory", "Defeat", "Remake"; Arena: your place ("2nd Place"), as the client's own post-game says it. "Game Over"
 * when the PC couldn't tell which line is yours (the result would only be a guess then).
 */
export function postGameTitle(s: PostGameSummary): string {
  const me = myLine(s)
  if (!me) return 'Game Over'
  return me.placement > 0 ? `${ordinal(me.placement)} Place` : resultLabel(s.result)
}

/** "Ranked Solo/Duo", "ARAM", "Arena"… */
export function postGameQueue(s: PostGameSummary): string {
  return s.queueId > 0 || s.gameMode ? gameQueueName(s.queueId, s.gameMode, '') : ''
}

/** "+21 LP", "−18 LP", "0 LP" (a typographic minus, like the client). */
export function lpDelta(rank: PostGameRank): string {
  const d = rank.leaguePointsDelta
  return d > 0 ? `+${d} LP` : d < 0 ? `−${-d} LP` : '0 LP'
}

/** VoiceOver for lpDelta(): "Gained 21 LP", "Lost 18 LP", "No LP change". */
export function lpDeltaSpoken(rank: PostGameRank): string {
  const d = rank.leaguePointsDelta
  return d > 0 ? `Gained ${d} LP` : d < 0 ? `Lost ${-d} LP` : 'No LP change'
}

/** The rank after the game: "Emerald III · 68 LP", "Placements: 3 Games Left", "Master · 120 LP". */
export function rankAfter(rank: PostGameRank): string {
  if (rank.provisionalGamesRemaining > 0) return `Placements: ${plural(rank.provisionalGamesRemaining, 'Game')} Left`
  const name = rankLabel(rank.tier, rank.division)
  return name ? `${name} · ${rank.leaguePoints} LP` : 'Unranked'
}

/** "Promoted" / "Demoted" for the result card's tag, '' otherwise. */
export function rankChange(rank: PostGameRank): string {
  return rank.change === 'promoted' ? 'Promoted' : rank.change === 'demoted' ? 'Demoted' : ''
}

/** "+1,240 Points". */
export function masteryGained(m: PostGameMastery): string {
  return `+${plural(m.pointsGained, 'Point')}`
}

/** "Level 12 · 9,760 to Level 13"; just the level when the next one's points aren't known. */
export function masteryLine(m: PostGameMastery): string {
  const next = m.pointsUntilNextLevel > 0 ? ` · ${formatCount(m.pointsUntilNextLevel)} to Level ${m.level + 1}` : ''
  return `Level ${m.level}${next}`
}

export function masteryTitle(m: PostGameMastery): string {
  const name = championName(m.championId)
  return name ? `${name} Mastery` : 'Mastery'
}

// --- Commands -----------------------------------------------------------------------------------------------
// sendCommand toasts refusals and a PC that doesn't answer; the state that follows moves the phone on.

/** Play Again: back to a lobby for this queue with your party (the phone follows to the Lobby). */
export async function playAgain(): Promise<boolean> {
  return !!(await sendCommand({ type: 'PlayAgain' }))?.success
}

/** Leave: closes the post-game screen (the phone follows to Play). */
export async function leavePostGame(): Promise<boolean> {
  return !!(await sendCommand({ type: 'LeavePostGame' }))?.success
}
