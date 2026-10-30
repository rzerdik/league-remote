import { computed } from 'vue'
import { now } from './clock'
import type { Friend, FriendGroup } from './contracts'
import { championName } from './ddragon'
import { formatSeconds } from './format'
import { queueById, rankLabel, type StatusTone } from './labels'
import { sendCommand } from './hub'
import { playerName, riotId } from './riotId'
import { liveFriends } from './store'

/**
 * League's friend list on the phone, as the connector publishes it (FriendsUpdated, throttled). Grouped by presence:
 * in game (blue), online (green), away (orange, plus mobile), offline.
 */

export const friends = computed<Friend[]>(() => liveFriends.value?.friends ?? [])

/** The user's own groups (not the default one); empty when they never made any. */
export const customGroups = computed<FriendGroup[]>(() => (liveFriends.value?.groups ?? []).filter((g) => !g.isDefault))

export function friendById(id: string | null | undefined): Friend | null {
  return (id && friends.value.find((f) => f.id === id)) || null
}

export type PresenceGroup = 'in-game' | 'online' | 'away' | 'offline'

export const PRESENCE_GROUPS: readonly { id: PresenceGroup; title: string }[] = [
  { id: 'in-game', title: 'In Game' },
  { id: 'online', title: 'Online' },
  { id: 'away', title: 'Away' },
  { id: 'offline', title: 'Offline' },
]

export function presenceGroup(f: Friend): PresenceGroup {
  switch (f.availability) {
    case 'busy':
      return 'in-game'
    case 'online':
      return 'online'
    case 'away':
    case 'mobile':
      return 'away'
    default:
      return 'offline'
  }
}

/**
 * Status-dot colour. The client draws "in game" red, but here red means a ban, the enemy or an error: busy in a game
 * is blue (the word next to the dot says which).
 */
export function presenceTone(f: Friend): StatusTone {
  switch (f.availability) {
    case 'busy':
      return 'blue'
    case 'online':
      return 'green'
    case 'away':
      return 'orange'
    default:
      return 'gray'
  }
}

/** Ticks once a second (for the in-game clocks). */
const nowSeconds = computed(() => {
  void now.value
  return Math.floor(Date.now() / 1000)
})

/** "12:30" since the activity began (the game's start while in game); '' when unknown. */
function activityClock(f: Friend): string {
  if (!f.activitySince) return ''
  const since = Date.parse(f.activitySince)
  if (Number.isNaN(since)) return ''
  return formatSeconds(nowSeconds.value - Math.floor(since / 1000))
}

/** The queue or mode they're in ("Ranked Solo/Duo", "ARAM"); '' when unknown. */
export function friendQueue(f: Friend): string {
  const queue = queueById(f.queueId)?.name
  if (queue) return queue
  return f.gameMode === 'ARAM' ? 'ARAM' : f.gameMode === 'CHERRY' ? 'Arena' : ''
}

/** The friend row's status line: "In Game 12:30 · Ranked Solo/Duo", "In Champ Select · Draft Pick", "Online". */
export function friendStatus(f: Friend): string {
  const queue = f.activity ? friendQueue(f) : ''
  const activity = friendActivity(f)
  return queue ? `${activity} · ${queue}` : activity
}

/** What they're doing, without the queue: "In Game 12:30", "In Lobby", "Away · brb", "Online 2 h ago". */
export function friendActivity(f: Friend): string {
  switch (f.activity) {
    case 'in-game': {
      const clock = activityClock(f)
      return clock ? `In Game ${clock}` : 'In Game'
    }
    case 'champ-select':
      return 'In Champ Select'
    case 'queue':
      return 'In Queue'
    case 'lobby':
      return 'In Lobby'
    case 'spectating':
      return 'Spectating'
  }
  switch (f.availability) {
    case 'online':
      return f.product ? `In ${f.product}` : f.statusMessage || 'Online'
    case 'away':
      return f.statusMessage ? `Away · ${f.statusMessage}` : 'Away'
    case 'busy':
      return 'Busy'
    case 'mobile':
      return 'On Mobile'
  }
  return lastSeen(f)
}

function lastSeen(f: Friend): string {
  const at = f.lastSeenOnline ? Date.parse(f.lastSeenOnline) : NaN
  if (Number.isNaN(at)) return 'Offline'
  const minutes = Math.max(0, Math.round((Date.now() - at) / 60000))
  if (minutes < 60) return `Online ${minutes} min ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `Online ${hours} h ago`
  const days = Math.round(hours / 24)
  return days === 1 ? 'Online yesterday' : `Online ${days} days ago`
}

/** The champion they're playing ("Pyke"); '' outside a game. */
export function friendChampion(f: Friend): string {
  return f.activity === 'in-game' && f.championId ? championName(f.championId) : ''
}

/** "Emerald I"; '' when unranked. */
export function friendRank(f: Friend): string {
  return rankLabel(f.rankTier, f.rankDivision)
}

/** Which queue `friendRank` is for: "Solo/Duo Rank", "Flex Rank" or "Rank". */
export function friendRankLabel(f: Friend): string {
  return f.rankQueue === 'RANKED_SOLO_5x5' ? 'Solo/Duo Rank' : f.rankQueue === 'RANKED_FLEX_SR' ? 'Flex Rank' : 'Rank'
}

/**
 * The friend a connector player (a lobby member) is: by puuid (a renamed friend still matches), or by name
 * ("Name#TAG") for a lobby member without a puuid.
 */
export function friendOf(player: { puuid?: string; riotId: string }): Friend | null {
  return friends.value.find((f) => isFriend(f, player)) ?? null
}

/** Whether a connector player (a lobby member) is this friend: by puuid when both have one, else by name. */
export function isFriend(f: Friend, player: { puuid?: string; riotId: string }): boolean {
  if (player.puuid && f.puuid) return player.puuid.toLowerCase() === f.puuid.toLowerCase()
  return isFriendNamed(f, player.riotId)
}

/** Whether a connector player name ("Name#TAG") is this friend: for lobby members without a puuid. */
function isFriendNamed(f: Friend, name: string): boolean {
  return playerName(name).toLowerCase() === riotId(f).toLowerCase()
}

/** Can get a lobby invite right now: in League, not in queue / draft / game (what the client allows). */
export function isInvitable(f: Friend): boolean {
  return (f.availability === 'online' || f.availability === 'away') && !f.product && (f.activity === '' || f.activity === 'lobby')
}

// --- Remove -------------------------------------------------------------------------------------------------

/** Removes the friend in League (the profile sheet confirms first). Resolves true when the client accepted it. */
export async function removeFriend(f: Friend): Promise<boolean> {
  const result = await sendCommand({ type: 'RemoveFriend', friendId: f.id })
  return !!result?.success
}
