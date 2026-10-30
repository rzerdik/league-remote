// Dev-only: `?demo=<scenario>` fills the store with a fake client state so every screen can be reviewed in a browser
// without the PC app or a League client. Loaded from main.ts only in `vite dev`; never part of a build. The scenarios
// are the `switch` in state() below plus `offline` and `pair` (handled at the end of this file); each module documents
// its own options (`&game=`, `&auto=`, …).
import { watchEffect } from 'vue'
import type { ClientState, CommandResult } from '../../contracts'
import { setFavouriteSkin } from '../../favouriteSkins'
import { nav } from '../../navigation'
import { devPush, pushStatus, type PushStatus } from '../../push'
import { devCommands } from '../../hub'
import { store, toast } from '../../store'
import { demoLicenses } from './about'
import { armAutoPickOnOpen, autoPickArm, autoPickState, autoPickUserHovered, demoAutoPick, openAutoPickDemo, pushAutoPick } from './autoPick'
import {
  aramAllySwaps,
  aramSelect,
  champSelect,
  completeMyAction,
  demoBench,
  demoDraftCommand,
  demoSkins,
  finalSeconds,
  loopFinalSeconds,
} from './champSelect'
import { chatState, demoChat, demoOpenDirect } from './chat'
import { base, lobby } from './client'
import { held, params, push, scenario, wait } from './core'
import { demoFriends, demoRemoveFriend, friendRequestLater, friendsState } from './friends'
import { demoInvite, invitingLobby, receivedInvites } from './invites'
import { liveAram, liveOff, liveStart, startLiveGame } from './liveGame'
import { demoPlayer, demoSearch, openProfileDemo } from './players'
import { demoPostGame, postGameStart, postGameToHistory } from './postGame'
import { demoProfile, profileState } from './profile'
import { demoReconnect, demoRefresh, reconnect } from './reconnect'

/** `&text=40` sets the root text size in px (≈ the largest accessibility size), for Dynamic Type checks. */
const textSize = Number(params.get('text'))
if (textSize > 0) document.documentElement.style.fontSize = `${textSize}px`

function state(): ClientState | null {
  // Not listed here: `offline` (no state, the phone stays offline) and `pair` (nothing paired), both further down.
  switch (scenario) {
    case 'play':
    case 'friends':
    case 'settings':
    case 'profile':
    case 'autopick':
      return base('None')
    case 'waiting':
      return { ...base('None'), lcuConnected: false }
    case 'lobby':
    case 'chat':
      return { ...base('Lobby'), lobby: lobby(false) }
    case 'queue':
      return { ...base('Matchmaking'), lobby: lobby(true) }
    case 'readycheck':
    case 'reconnect':
      return {
        ...base('ReadyCheck'),
        lobby: lobby(true),
        readyCheck: { state: 'InProgress', playerResponse: 'None', timer: 3 },
      }
    case 'planning':
    case 'pick':
    case 'ban':
    case 'enemypick':
    case 'final':
      return { ...base('ChampSelect'), lobby: lobby(false), champSelect: champSelect(scenario) }
    case 'aram':
      return { ...base('ChampSelect'), lobby: { ...lobby(false), queueId: 450, gameMode: 'ARAM', showPositionSelector: false }, champSelect: aramSelect() }
    case 'pick-final':
    case 'ban-final':
      return { ...base('ChampSelect'), lobby: lobby(false), champSelect: finalSeconds(champSelect(scenario === 'ban-final' ? 'ban' : 'pick')) }
    case 'skins': {
      const cs = champSelect('final')
      return { ...base('ChampSelect'), lobby: lobby(false), champSelect: cs && { ...cs, skins: demoSkins() } }
    }
    case 'loading':
      return { ...base('GameStart'), inGame: { loaded: false, gameTime: 0, startedAt: null } }
    case 'game':
      return {
        ...base('InProgress'),
        lobby: { ...lobby(false), queueId: liveAram ? 450 : 420, gameMode: liveAram ? 'ARAM' : 'CLASSIC' },
        inGame: { loaded: true, gameTime: liveStart, startedAt: new Date().toISOString() },
      }
    case 'postgame':
      return postGameStart()
    case 'invite':
      return { ...base('None'), invitations: receivedInvites() }
    case 'toasts':
      return { ...base('None'), invitations: receivedInvites().slice(0, 1) }
    case 'invite-lobby':
      return { ...base('Lobby'), lobby: invitingLobby() }
    default:
      return null
  }
}

/** The fake PC's answers: each feature module handles its own commands; the rest are short enough to live here. */
devCommands.handle = async (cmd): Promise<CommandResult | null> => {
  const ok = { requestId: cmd.requestId, success: true }
  if (cmd.type === 'RefreshState' && scenario === 'reconnect') return demoRefresh(cmd)
  if (/AutoPick/.test(cmd.type)) return demoAutoPick(cmd)
  if (cmd.type === 'BenchSwap' || cmd.type === 'Reroll') return demoBench(cmd)
  if (/FriendRequest$/.test(cmd.type)) return demoFriends(cmd)
  if (cmd.type === 'LoadPlayerProfile' || (cmd.type === 'LoadMatchDetails' && cmd.puuid)) return demoPlayer(cmd)
  if (cmd.type === 'SearchPlayer') return demoSearch(cmd)
  if (cmd.type === 'RefreshProfile' || cmd.type === 'LoadMatchDetails') return demoProfile(cmd)
  if (cmd.type === 'PlayAgain' || cmd.type === 'LeavePostGame') return demoPostGame(cmd)
  if (cmd.type === 'DodgeChampSelect' || cmd.type === 'SelectSkin') return demoDraftCommand(cmd)
  if (cmd.type === 'AcceptInvitation' || cmd.type === 'DeclineInvitation' || cmd.type === 'SendInvitation') {
    await demoInvite(cmd)
    return ok
  }
  if (cmd.type === 'SendChatMessage' || cmd.type === 'MarkConversationRead') {
    void demoChat(cmd)
    return ok
  }
  if (cmd.type === 'RemoveFriend') {
    await demoRemoveFriend(cmd.friendId)
    return ok
  }
  if (cmd.type === 'OpenDirectConversation') {
    const opened = await demoOpenDirect(cmd.puuid ?? '')
    return { requestId: cmd.requestId, success: opened, error: opened ? null : "That player isn't on your friends list." }
  }
  // Ready check: Accept waits for the others (the accepted loop), then everyone is ready and champ select opens (the
  // success outro); Decline drops back to the lobby.
  if ((cmd.type === 'AcceptReadyCheck' || cmd.type === 'DeclineReadyCheck') && store.state?.readyCheck) {
    const s = store.state
    const accepted = cmd.type === 'AcceptReadyCheck'
    await wait(300)
    push({ ...s, readyCheck: { ...s.readyCheck!, playerResponse: accepted ? 'Accepted' : 'Declined' } })
    void (async () => {
      await wait(accepted ? 3000 : 1200)
      push(
        accepted
          ? { ...store.state!, gameflowPhase: 'ChampSelect', readyCheck: null, lobby: lobby(false), champSelect: champSelect('planning') }
          : { ...store.state!, gameflowPhase: 'Lobby', readyCheck: null, lobby: lobby(false) },
      )
    })()
    return ok
  }
  // Lobby ✕: the client drops the lobby and the next push lands on Play.
  if (cmd.type === 'LeaveLobby') {
    await wait(300)
    push({ ...store.state!, gameflowPhase: 'None', lobby: null })
    return ok
  }
  // Champion Select: hovers stick; Lock In / Ban succeed after a short "client confirms" delay, and a ban walks on
  // through the rest of the draft (bans end → enemies pick → your turn) so the picker's collapse and rise show.
  const cs = store.state?.champSelect
  if (!cs || (cmd.type !== 'HoverChampion' && cmd.type !== 'LockChampion')) return null
  const id = cmd.championId ?? 0
  if (cmd.type === 'HoverChampion') {
    if (cs.myAction) cs.myAction.championId = id
    autoPickUserHovered(id)
    return ok
  }
  await wait(600)
  completeMyAction(id, autoPickArm)
  return ok
}

if (scenario && scenario !== 'pair') {
  held.state = state()
  held.chat = chatState()
  held.friends = friendsState()
  held.profile = profileState()
  pushAutoPick(autoPickState())
  if (scenario === 'chat') nav.tab = 'chat'
  if (scenario === 'autopick') openAutoPickDemo()
  armAutoPickOnOpen()
  if (scenario === 'profile') openProfileDemo()
  // ?demo=settings: the Settings tab with notifications on (the alert toggles and test button show); `&push=default`,
  // `denied`, `needs-install` or `unsupported` shows the other states.
  if (scenario === 'settings') nav.tab = 'settings'
  // Settings › About › Licenses answers from demo text in every scenario (`&licenses=fail` for the error state).
  demoLicenses()
  // `&push=default` on `play` shows Play's notifications tip (if it wasn't dismissed in this browser).
  const pushParam = params.get('push')
  if (scenario === 'settings' || pushParam) pushStatus.value = devPush.status = (pushParam ?? 'enabled') as PushStatus
  if (scenario === 'pick-final' || scenario === 'ban-final') loopFinalSeconds()
  if (scenario === 'friends') {
    nav.tab = 'friends'
    friendRequestLater()
  }
  // ?demo=toasts: an invite banner over two toasts (a one-liner, and a title with a message line), held for review
  // instead of timing out: the newest shows, the other folds into "1 More" (tap to unfold); swipe one up to dismiss it.
  // `&text=40`: the toasts drop their message line.
  if (scenario === 'toasts') {
    const HOLD_MS = 10 * 60 * 1000
    setTimeout(() => toast('Friend request sent to Demo Player #EUW', 'success', HOLD_MS), 300)
    setTimeout(() => toast("The PC didn't answer. Try again.", 'error', HOLD_MS), 600)
  }
  if (scenario === 'reconnect') demoReconnect()
  if (scenario === 'aram') aramAllySwaps()
  if (scenario === 'postgame') postGameToHistory()
  if (scenario === 'game' && !liveOff) startLiveGame()
  // Demos never open the hub connection (hub.ts): hold the fake connection state (?demo=reconnect drops it on purpose,
  // ?demo=offline keeps the phone offline).
  watchEffect(() => {
    if (reconnect.dropped) return
    if (!store.grant) store.grant = { pairingId: 'demo', token: 'demo' }
    if (scenario === 'offline') {
      if (store.connection !== 'disconnected') store.connection = 'disconnected'
      return
    }
    if (store.connection !== 'connected') store.connection = 'connected'
    if (!store.authenticated) store.authenticated = true
    // A slice that is on its way after a sign-in (?demo=reconnect) is left to the RefreshState answer.
    if (held.state && !store.state && !store.syncing.state) {
      store.state = held.state
      store.receivedAt.state = performance.now()
    }
    if (held.chat && !store.chat && !store.syncing.chat) store.chat = held.chat
    if (held.friends && !store.friends && !store.syncing.friends) store.friends = held.friends
    if (held.profile && !store.profile && !store.syncing.profile) store.profile = held.profile
    if (held.liveGame && !store.liveGame && !store.syncing.liveGame) store.liveGame = held.liveGame
    if (held.autoPick && !store.autoPick && !store.syncing.autoPick) {
      store.autoPick = held.autoPick
      store.receivedAt.autoPick = performance.now()
    }
  })
  // `?demo=skins&favourite=1`: this phone's favourite Ahri skin is Star Guardian (Rose Quartz), offered in Loadout
  // (stored under the demo pairing, set just above); plain `?demo=skins` starts without one.
  if (scenario === 'skins') setFavouriteSkin(103, params.get('favourite') === '1' ? 103040 : null)
}

// ?demo=pair: the PC is reachable (no "Offline" line under the code), nothing is paired. Demos never open the hub
// connection (hub.ts), so nothing else would set it.
if (scenario === 'pair') store.connection = 'connected'
