// ?demo=reconnect: a ready check; at 3 s the connection drops ("Reconnecting…"), at 6 s it comes back to a PC with
// nothing cached, so nothing is replayed: Home says "Checking League…" (Chat, Friends and Profile say Loading) instead
// of the old ready check, until the RefreshState answer 6 s later brings the state as it is now: the ready check was
// accepted meanwhile and champion select is on. `&tab=chat` (profile, friends) watches that tab instead.
import { reactive } from 'vue'
import { nav } from '../../navigation'
import { devSignInWithoutReplay } from '../../hub'
import { store } from '../../store'
import { pushAutoPick } from './autoPick'
import { champSelect } from './champSelect'
import { base, lobby } from './client'
import { held, params, push, wait } from './core'

const RECONNECT_DROP_MS = 3000
const RECONNECT_BACK_MS = 6000
const RECONNECT_REFRESH_MS = 6000

/** True while the fake connection is down: index.ts stops holding it up. */
export const reconnect = reactive({ dropped: false })

export function demoReconnect() {
  const tab = params.get('tab')
  if (tab === 'chat' || tab === 'friends' || tab === 'profile') nav.tab = tab
  setTimeout(() => {
    reconnect.dropped = true
    store.authenticated = false
    store.connection = 'reconnecting'
  }, RECONNECT_DROP_MS)
  setTimeout(() => {
    reconnect.dropped = false
    store.connection = 'connected'
    devSignInWithoutReplay()
  }, RECONNECT_BACK_MS)
}

/** The connector's answer to the RefreshState sent on sign-in: every slice, current. */
export async function demoRefresh(cmd: { requestId: string }) {
  await wait(RECONNECT_REFRESH_MS)
  push({ ...base('ChampSelect'), lobby: lobby(false), champSelect: champSelect('planning') })
  store.chat = held.chat
  store.friends = held.friends
  store.profile = held.profile
  store.liveGame = held.liveGame
  if (held.autoPick) pushAutoPick(held.autoPick)
  Object.assign(store.syncing, { state: false, chat: false, friends: false, profile: false, liveGame: false, autoPick: false })
  return { requestId: cmd.requestId, success: true }
}
