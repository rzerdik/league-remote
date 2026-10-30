import type { LobbyState } from './contracts'

/** Status colours. `blue`: busy elsewhere, not a problem (a friend in a game), so never red. */
export type StatusTone = 'green' | 'orange' | 'red' | 'gray' | 'blue'

/** Queue ids verified against https://static.developer.riotgames.com/docs/lol/queues.json */
export const QUEUES: { id: number; name: string; map: string }[] = [
  { id: 490, name: 'Quickplay', map: "Summoner's Rift" },
  { id: 480, name: 'Swiftplay', map: "Summoner's Rift" },
  { id: 400, name: 'Draft Pick', map: "Summoner's Rift" },
  { id: 420, name: 'Ranked Solo/Duo', map: "Summoner's Rift" },
  { id: 440, name: 'Ranked Flex', map: "Summoner's Rift" },
  { id: 450, name: 'ARAM', map: 'Howling Abyss' },
  { id: 2400, name: 'ARAM: Mayhem', map: 'Howling Abyss' },
  // Arena runs under a new queue id now and then (1700 → 1750 "Arena 3x6"); the connector reports which one is live.
  { id: 1700, name: 'Arena', map: 'Rings of Wrath' },
  { id: 1750, name: 'Arena', map: 'Rings of Wrath' },
]

/** A Play-menu queue by id. */
export function queueById(id: number | null | undefined) {
  return QUEUES.find((q) => q.id === id)
}

/** Queue name for subtitles, or '' for custom/unknown queues. */
export function queueLabel(queueId: number | null | undefined): string {
  if (queueId == null || queueId <= 0) return ''
  return queueById(queueId)?.name ?? ''
}

/** Queue name for titles: "Queue 1234" for one the phone doesn't list. */
export function queueName(id: number): string {
  return queueLabel(id) || `Queue ${id}`
}

const PRACTICE_TOOL = 'PRACTICETOOL'

/** Lobby screen title: the queue name (Practice Tool / Custom Game for custom lobbies). */
export function lobbyTitle(lobby: LobbyState): string {
  if (lobby.gameMode === PRACTICE_TOOL) return 'Practice Tool'
  if (lobby.isCustom) return 'Custom Game'
  return queueName(lobby.queueId)
}

/** The map a lobby plays on, or '' when unknown. */
export function lobbyMap(lobby: LobbyState): string {
  if (lobby.gameMode === PRACTICE_TOOL) return "Summoner's Rift"
  return queueById(lobby.queueId)?.map ?? ''
}

export function isPracticeTool(lobby: LobbyState | null | undefined): boolean {
  return lobby?.gameMode === PRACTICE_TOOL
}

/** Queues whose party is smaller than five (Ranked Solo/Duo: a duo at most). */
const SMALL_PARTIES: Record<number, number> = { 420: 2 }

/**
 * How many players the lobby's party may hold, as the client's "2/5" shows it; null where the phone doesn't know
 * (custom games and Practice Tool, Arena's changing queues, queues it doesn't list).
 */
export function partyMax(lobby: LobbyState): number | null {
  if (lobby.isCustom || isPracticeTool(lobby) || lobby.gameMode === 'CHERRY') return null
  if (SMALL_PARTIES[lobby.queueId]) return SMALL_PARTIES[lobby.queueId]!
  return queueById(lobby.queueId) ? 5 : null
}

const PHASES: Record<string, string> = {
  None: 'Menus',
  Lobby: 'Lobby',
  Matchmaking: 'In Queue',
  CheckedIntoTournament: 'Tournament',
  ReadyCheck: 'Match Found',
  ChampSelect: 'Champ Select',
  GameStart: 'Starting',
  FailedToLaunch: 'Launch Failed',
  InProgress: 'In Game',
  Reconnect: 'Reconnect',
  WaitingForStats: 'Post-Game',
  PreEndOfGame: 'Game Ending',
  EndOfGame: 'Post-Game',
  TerminatedInError: 'Game Error',
}

export function phaseLabel(phase: string | undefined | null): string {
  if (!phase) return 'Unknown'
  return PHASES[phase] ?? phase
}

/** The five roles as League names them (assignedPosition, match history), in the client's order. */
export const ROLES = ['TOP', 'JUNGLE', 'MIDDLE', 'BOTTOM', 'UTILITY'] as const

const POSITIONS: Record<string, string> = {
  top: 'Top',
  jungle: 'Jungle',
  middle: 'Mid',
  mid: 'Mid',
  bottom: 'Bot',
  bot: 'Bot',
  utility: 'Support',
  support: 'Support',
  fill: 'Fill',
  unselected: '',
  none: '',
}

export function positionLabel(p: string | null | undefined): string {
  if (!p) return ''
  return POSITIONS[p.toLowerCase()] ?? p
}

const TIERS: Record<string, string> = {
  IRON: 'Iron',
  BRONZE: 'Bronze',
  SILVER: 'Silver',
  GOLD: 'Gold',
  PLATINUM: 'Platinum',
  EMERALD: 'Emerald',
  DIAMOND: 'Diamond',
  MASTER: 'Master',
  GRANDMASTER: 'Grandmaster',
  CHALLENGER: 'Challenger',
}

/** "Emerald I", "Master"; '' when unranked. */
export function rankLabel(tier: string, division: string): string {
  if (!tier) return ''
  const name = TIERS[tier] ?? tier
  return division ? `${name} ${division}` : name
}

const RANKED_QUEUES: Record<string, string> = {
  RANKED_SOLO_5x5: 'Ranked Solo/Duo',
  RANKED_FLEX_SR: 'Ranked Flex',
}

/** A ranked queue type as the client names it ("Ranked Solo/Duo"); other types are spelled out from their id. */
export function rankedQueueName(queueType: string): string {
  const known = RANKED_QUEUES[queueType]
  if (known) return known
  const words = queueType
    .split('_')
    .filter((w) => w && !/^\d+x\d+$/i.test(w))
    .map((w) => w.charAt(0) + w.slice(1).toLowerCase())
  return words.join(' ') || 'Ranked'
}

/**
 * Queues that only show up in match history (not in the Play menu, so not in QUEUES). Ids from Riot's queues.json.
 */
const HISTORY_QUEUES: Record<number, string> = {
  0: 'Custom Game',
  700: 'Clash',
  720: 'ARAM Clash',
  830: 'Co-op vs. AI',
  840: 'Co-op vs. AI',
  850: 'Co-op vs. AI',
  870: 'Co-op vs. AI',
  880: 'Co-op vs. AI',
  890: 'Co-op vs. AI',
  900: 'ARURF',
  1020: 'One for All',
  1300: 'Nexus Blitz',
  1400: 'Ultimate Spellbook',
  1900: 'URF',
  3140: 'Practice Tool',
}

/** The queue a finished game was played in ("Ranked Solo/Duo", "Practice Tool", "Custom Game", "ARAM"). */
export function gameQueueName(queueId: number, gameMode: string, gameType: string): string {
  if (gameMode === 'PRACTICETOOL') return 'Practice Tool'
  const queue = queueById(queueId)?.name ?? HISTORY_QUEUES[queueId]
  if (queue && queueId > 0) return queue
  if (gameType === 'CUSTOM_GAME') return 'Custom Game'
  if (gameMode === 'ARAM') return 'ARAM'
  if (gameMode === 'CHERRY') return 'Arena'
  return queue ?? `Queue ${queueId}`
}
