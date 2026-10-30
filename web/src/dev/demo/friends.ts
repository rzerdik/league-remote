import type { Friend, FriendRequest, FriendsState } from '../../contracts'
import { store } from '../../store'
import { held, minutesAgo, wait } from './core'
import { TARN, VEXA } from './people'

function friend(gameName: string, tagLine: string, availability: string, extra: Partial<Friend> = {}): Friend {
  const puuid = `demo-${gameName.toLowerCase()}`
  return {
    id: `${puuid}@eu2.pvp.net`,
    puuid,
    summonerId: 1000 + gameName.length,
    gameName,
    tagLine,
    groupId: 0,
    availability,
    activity: '',
    product: '',
    queueId: null,
    gameMode: '',
    championId: null,
    activitySince: null,
    statusMessage: '',
    note: '',
    profileIconId: 29,
    rankTier: '',
    rankDivision: '',
    rankQueue: '',
    lastSeenOnline: null,
    ...extra,
  }
}

/** Every presence the Friends tab shows, plus a user group ("Duo"); Vexa and Tarn have DMs in the demo chat. */
export function friendsState(): FriendsState {
  const solo = { rankQueue: 'RANKED_SOLO_5x5' }
  const list = [
    { ...friend(VEXA.gameName, VEXA.tagLine, 'online', { rankTier: 'EMERALD', rankDivision: 'II', ...solo, note: 'duo bot', groupId: 7 }), id: `${VEXA.puuid}@eu2.pvp.net`, puuid: VEXA.puuid },
    { ...friend(TARN.gameName, TARN.tagLine, 'busy', { activity: 'in-game', queueId: 420, gameMode: 'CLASSIC', championId: 555, activitySince: minutesAgo(12.5), rankTier: 'GOLD', rankDivision: 'I', ...solo }), id: `${TARN.puuid}@eu2.pvp.net`, puuid: TARN.puuid },
    friend('NightOwl', 'EUW', 'busy', { activity: 'champ-select', queueId: 400, activitySince: minutesAgo(1), groupId: 7 }),
    friend('Kestrel', 'EUW', 'busy', { activity: 'in-game', queueId: 2400, gameMode: 'KIWI', championId: 21, activitySince: minutesAgo(3.2) }),
    friend('Bramble', 'EUNE', 'busy', { activity: 'queue', queueId: 450, activitySince: minutesAgo(2) }),
    friend('Halcyon', 'EUNE', 'online', { activity: 'lobby', queueId: 2400, rankTier: 'MASTER', ...solo }),
    friend('Wisp', 'EUW', 'online', { product: 'Riot Client' }),
    friend('Juniper', 'EUW', 'away', { statusMessage: 'brb food', rankTier: 'GOLD', rankDivision: 'II', ...solo }),
    friend('Pebble', '2468', 'mobile'),
    friend('Mirelle', 'EUW', 'offline', { lastSeenOnline: minutesAgo(60 * 26) }),
    friend('Ödön', '1234', 'offline'),
    friend('Brakk', 'EUNE', 'offline', { lastSeenOnline: minutesAgo(60 * 24 * 9), groupId: 7 }),
    friend('Saffi', 'EUW', 'offline'),
  ]
  return {
    groups: [
      { id: 7, name: 'Duo', isDefault: false, priority: 1 },
      { id: 0, name: '', isDefault: true, priority: 2 },
    ],
    friends: list.sort((a, b) => a.gameName.localeCompare(b.gameName)),
    requests: friendRequests(),
    updatedAt: new Date().toISOString(),
  }
}

export function request(gameName: string, tagLine: string, direction: 'incoming' | 'outgoing', profileIconId = 0): FriendRequest {
  return { puuid: `demo-req-${gameName.toLowerCase().replace(/\W/g, '')}`, direction, gameName, tagLine, profileIconId, note: '' }
}

/** Two incoming, two sent (on the Friends tab of every scenario, like League's friend list). */
function friendRequests(): FriendRequest[] {
  return [
    request('Moonlit Fox', 'EUW', 'incoming', 29),
    request('Tidecaller', '1234', 'incoming', 5132),
    request('Alpha Wolf', '1717', 'outgoing'),
    request('Ząbkó Test', 'xyz', 'outgoing'),
  ]
}

function pushFriends(next: FriendsState) {
  held.friends = next
  store.friends = next
}

/**
 * Accept / decline / cancel land a moment later; Add Friend refuses "Nobody#404" (like League's 404) and otherwise
 * adds a sent request.
 */
export async function demoFriends(cmd: { type: string; puuid?: string | null; gameName?: string | null; tagLine?: string | null; requestId: string }) {
  await wait(500)
  const f = held.friends
  if (!f) return { requestId: cmd.requestId, success: false, error: 'League client is not running.' }
  if (cmd.type === 'SendFriendRequest') {
    if ((cmd.gameName ?? '').toLowerCase() === 'nobody') return { requestId: cmd.requestId, success: false, error: 'No player with that Riot ID.' }
    pushFriends({ ...f, requests: [...f.requests, request(cmd.gameName ?? '', cmd.tagLine ?? '', 'outgoing')], updatedAt: new Date().toISOString() })
    return { requestId: cmd.requestId, success: true }
  }
  pushFriends({ ...f, requests: f.requests.filter((r) => r.puuid !== cmd.puuid), updatedAt: new Date().toISOString() })
  return { requestId: cmd.requestId, success: true }
}

/** Remove friend: the next (throttled) friends push no longer has them. */
export async function demoRemoveFriend(friendId: string | null | undefined) {
  await wait(500)
  if (held.friends) pushFriends({ ...held.friends, friends: held.friends.friends.filter((f) => f.id !== friendId) })
}

/** ?demo=friends: a new incoming request arrives a few seconds after the demo opens. */
export function friendRequestLater() {
  setTimeout(() => {
    if (held.friends) pushFriends({ ...held.friends, requests: [request('Night Owl', 'NA1', 'incoming', 4), ...held.friends.requests] })
  }, 6000)
}
