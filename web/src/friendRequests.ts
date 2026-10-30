import { computed, reactive, watch } from 'vue'
import type { CommandType, FriendRequest } from './contracts'
import { haptic, tapFeedback } from './haptics'
import { sendCommand } from './hub'
import { parseRiotId, riotId } from './riotId'
import { liveFriends, toast } from './store'

/**
 * Friend requests: pending requests both ways from FriendsState.requests, accept / decline / cancel, and Add Friend by
 * Riot ID. Requests are keyed by puuid (the LCU's own key for them).
 */

/** Requests answered on the phone, hidden until the connector's list drops them (or a while passes). */
const answered = reactive(new Map<string, number>())
const ANSWERED_TTL_MS = 15000

/** Puuids with a command in flight (their row shows the activity indicator). */
export const busy = reactive(new Set<string>())

const requests = computed<FriendRequest[]>(() =>
  (liveFriends.value?.requests ?? []).filter((r) => !answered.has(r.puuid)),
)
export const incomingRequests = computed(() => requests.value.filter((r) => r.direction === 'incoming'))
export const outgoingRequests = computed(() => requests.value.filter((r) => r.direction === 'outgoing'))

/** Forget answered requests once League's list no longer has them. */
watch(
  () => liveFriends.value?.requests,
  (list) => {
    if (!list) return
    for (const puuid of [...answered.keys()]) if (!list.some((r) => r.puuid === puuid)) answered.delete(puuid)
  },
)

/** A toast when a new incoming request arrives while the app is open (not for the list the phone signs in to). */
let known: Set<string> | null = null
watch(
  () => liveFriends.value?.requests,
  (list) => {
    if (!list) {
      known = null
      return
    }
    const incoming = list.filter((r) => r.direction === 'incoming')
    const fresh = known ? incoming.filter((r) => !known!.has(r.puuid)) : []
    known = new Set(incoming.map((r) => r.puuid))
    if (fresh.length === 1) toast(`${riotId(fresh[0]!)} wants to be friends`, 'info')
    else if (fresh.length > 1) toast(`${fresh.length} new friend requests`, 'info')
  },
)

async function answer(r: FriendRequest, type: CommandType): Promise<boolean> {
  if (busy.has(r.puuid)) return false
  tapFeedback()
  busy.add(r.puuid)
  try {
    const result = await sendCommand({ type, puuid: r.puuid })
    if (!result?.success) {
      haptic('error')
      return false
    }
    haptic(type === 'AcceptFriendRequest' ? 'success' : 'light')
    answered.set(r.puuid, Date.now())
    setTimeout(() => answered.delete(r.puuid), ANSWERED_TTL_MS)
    return true
  } finally {
    busy.delete(r.puuid)
  }
}

export const acceptRequest = (r: FriendRequest) => answer(r, 'AcceptFriendRequest')
export const declineRequest = (r: FriendRequest) => answer(r, 'DeclineFriendRequest')
export const cancelRequest = (r: FriendRequest) => answer(r, 'CancelFriendRequest')

/** Sends the request; the connector's error (League's reason, e.g. "No player with that Riot ID.") comes back as is. */
export async function sendFriendRequest(gameName: string, tagLine: string): Promise<{ ok: boolean; error?: string }> {
  const id = parseRiotId(gameName, tagLine)
  const result = await sendCommand({ type: 'SendFriendRequest', ...id }, { silent: true })
  if (result?.success) {
    haptic('success')
    return { ok: true }
  }
  haptic('error')
  return { ok: false, error: result?.error || 'Offline' }
}
