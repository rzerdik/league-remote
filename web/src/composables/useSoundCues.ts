import { onScopeDispose, watch } from 'vue'
import type { ChatState, ClientState, FriendsState } from '../contracts'
import { draftView, myTurnNow } from '../draft'
import { vibrate } from '../haptics'
import { play, stop, type SoundKey } from '../sound'
import { liveChat, liveFriends, liveState } from '../store'

/**
 * Plays the League client's sound cues for state transitions, at the moments the client plays them (ClientSoundCatalog.cs
 * says which file and where in the client that was read). Only transitions between two consecutive live snapshots
 * produce sounds: the first snapshot after (re)connecting is a baseline, so reconnects and state replays stay silent.
 * The one exception is a ready check that is still waiting for us (with a cooldown so a replay does not re-chime).
 * At most one cue (the most important) plays per state update; the ready check's countdown, portal and accepted loop
 * run alongside like in the client. Timer ticks live with the timer (champselect/useDraftTimer).
 */

interface Snapshot {
  phase: string
  rcActive: boolean
  rcWaiting: boolean
  rcState: string
  rcResponse: string
  searching: boolean
  inLobby: boolean
  isLeader: boolean
  /** Other lobby members (display names). */
  members: Set<string>
  /** Pending invites sent from this lobby (puuids). */
  invitesSent: Set<string>
  /** Invites to other parties waiting for an answer (ids). */
  invitesReceived: Set<string>
  csStage: 'planning' | 'ban' | 'pick' | 'final' | null
  /** Sequential picks (draft): the client's pick lock / first pick events exist only there. */
  draft: boolean
  simultaneousBans: boolean
  actionId: number | null
  actionHot: boolean
  actionType: string | null
  /** Your pick is locked in / your ban is in (from your team row: myAction has already moved on by then). */
  myLocked: boolean
  myBanned: boolean
  /** The champion you declared or hover for your pick (0 when none or locked). */
  myIntent: number
  allyLocks: number
  enemyLocks: number
  allyBans: number
  enemyBans: number
  /** Every visible ban, both teams. */
  bannedIds: Set<number>
  swaps: Map<string, { kind: string; state: string }>
  /** Bench modes: the bench (ids in order, '' when none) and your dealt champion. */
  bench: string | null
  myChampion: number
  gameLoaded: boolean
}

function snapshot(s: ClientState): Snapshot {
  const rc = s.readyCheck
  const cs = s.gameflowPhase === 'ChampSelect' ? s.champSelect : null
  const a = cs?.myAction ?? null
  const me = cs?.myTeam.find((m) => m.isLocalPlayer) ?? null
  let csStage: Snapshot['csStage'] = null
  if (cs) {
    // The draft's own stage, so the pick-phase cue waits for the bans of all ten players, not just yours.
    const stage = draftView(cs).stage
    if (stage === 'planning') csStage = 'planning'
    else if (stage === 'banning') csStage = 'ban'
    else if (stage === 'picking' || stage === 'reveal') csStage = 'pick'
    else if (stage === 'finalization' || stage === 'starting') csStage = 'final'
  }
  const turns = cs?.turns ?? []
  const swaps = new Map<string, { kind: string; state: string }>()
  for (const sw of cs?.swaps ?? []) if (sw.id > 0) swaps.set(`${sw.kind}:${sw.id}`, { kind: sw.kind, state: sw.state })
  const lobby = s.gameflowPhase === 'Lobby' ? s.lobby : null
  const myLocked = !!me && me.isLocked && me.championId > 0
  return {
    phase: s.gameflowPhase,
    rcActive: !!rc && rc.state === 'InProgress',
    rcWaiting: !!rc && rc.state === 'InProgress' && rc.playerResponse === 'None',
    rcState: rc?.state ?? '',
    rcResponse: rc?.playerResponse ?? 'None',
    searching: !!s.lobby?.isSearching,
    inLobby: !!lobby,
    isLeader: !!lobby?.isLeader,
    members: new Set((lobby?.members ?? []).filter((m) => !m.isLocalPlayer).map((m) => m.riotId)),
    invitesSent: new Set((lobby?.invitations ?? []).filter((i) => i.state === 'Pending').map((i) => i.puuid)),
    invitesReceived: new Set((s.invitations ?? []).map((i) => i.id)),
    csStage,
    draft: turns.some((t) => t.type === 'pick' && (t.team === 'ally' || t.team === 'enemy')),
    simultaneousBans: turns.some((t) => t.type === 'ban' && t.team === 'both'),
    actionId: a?.id ?? null,
    actionHot: !!cs && myTurnNow(cs),
    actionType: a?.type ?? null,
    myLocked,
    myBanned: !!me?.hasBanned && me.banChampionId > 0,
    myIntent: me && !myLocked ? me.championPickIntent || me.championId : 0,
    allyLocks: (cs?.myTeam ?? []).filter((m) => !m.isLocalPlayer && m.isLocked && m.championId > 0).length,
    enemyLocks: (cs?.theirTeam ?? []).filter((m) => m.isLocked).length,
    allyBans: (cs?.myTeam ?? []).filter((m) => !m.isLocalPlayer && m.hasBanned && m.banChampionId > 0).length,
    enemyBans: cs?.bans.theirTeamBans.filter((id) => id > 0).length ?? 0,
    bannedIds: new Set([...(cs?.bans.myTeamBans ?? []), ...(cs?.bans.theirTeamBans ?? [])].filter((id) => id > 0)),
    swaps,
    bench: cs?.benchEnabled ? (cs.benchChampionIds ?? []).join(',') : null,
    myChampion: me?.championId ?? 0,
    gameLoaded: !!s.inGame?.loaded,
  }
}

/** Higher wins when several cues happen in one update. */
const PRIORITY: Partial<Record<SoundKey, number>> = {
  matchFound: 100,
  yourTurn: 90,
  yourBan: 90,
  gameStart: 85,
  readySuccess: 84,
  readyFailed: 84,
  swapRequest: 80,
  positionSwapRequest: 80,
  inviteReceived: 78,
  pickSniped: 75,
  lockIn: 70,
  ban: 70,
  readyAccept: 60,
  readyDecline: 60,
  allyFirstPick: 55,
  enemyFirstPick: 55,
  swapAccepted: 50,
  swapDeclined: 50,
  benchUpdate: 44,
  allyPick: 48,
  enemyPick: 47,
  serialAllyBan: 46,
  serialEnemyBan: 46,
  allyBan: 45,
  leaderGranted: 42,
  banPhase: 40,
  pickPhase: 35,
  memberJoined: 32,
  memberLeft: 31,
  queueStart: 30,
  queueCancel: 30,
  inviteSent: 25,
}

function added<T>(before: Set<T>, now: Set<T>): T[] {
  return [...now].filter((x) => !before.has(x))
}

/** Incoming chat lines per conversation (ids), to tell new messages from history. */
function chatSnapshot(chat: ChatState): Map<string, Set<string>> {
  const seen = new Map<string, Set<string>>()
  for (const c of chat.conversations) seen.set(c.id, new Set(c.messages.map((m) => m.id)))
  return seen
}

/** Incoming friend requests (puuids). */
function requestSnapshot(friends: FriendsState): Set<string> {
  return new Set(friends.requests.filter((r) => r.direction === 'incoming').map((r) => r.puuid))
}

/** A conversation that appears with history (joining a room) only chimes for a message this recent. */
const NEW_ROOM_FRESH_MS = 30000

/** The client's CHAMPION_BENCH_SOUND_COOLDOWN_MS: the bench chimes at most this often. */
const BENCH_SOUND_COOLDOWN_MS = 3000

const MATCH_FOUND_COOLDOWN_MS = 15000
/** The phone keeps buzzing (Android) while the ready check waits: sound alone is easy to miss in a pocket. */
const MATCH_FOUND_BUZZ_EVERY_MS = 3000
const MATCH_FOUND_MAX_BUZZES = 4

export function useSoundCues() {
  let prev: Snapshot | null = null
  let matchFoundAt = 0
  let benchSoundAt = 0
  let buzzTimer: ReturnType<typeof setInterval> | undefined
  /** The ready check's background layers: countdown + portal while waiting, then the accepted loop. */
  let readyLayers: (() => void)[] = []

  function stopReadyLayers() {
    for (const s of readyLayers) s()
    readyLayers = []
    stop('readyCountdown')
    stop('readyAcceptedLoop')
  }

  /** The client's ready check opening: low-spec intro, the ring countdown (joined where it is) and the map's portal. */
  function startMatchFound(s: ClientState) {
    if (Date.now() - matchFoundAt < MATCH_FOUND_COOLDOWN_MS) return false
    matchFoundAt = Date.now()
    stopMatchFound()
    play('matchFound')
    const elapsed = Math.max(0, s.readyCheck?.timer ?? 0)
    readyLayers.push(play('readyCountdown', { offset: elapsed }))
    readyLayers.push(play(s.lobby?.gameMode === 'ARAM' ? 'readyPortalAram' : 'readyPortal'))
    vibrate([300, 150, 300, 150, 300])
    let buzzes = 1
    buzzTimer = setInterval(() => {
      if (++buzzes > MATCH_FOUND_MAX_BUZZES) return clearInterval(buzzTimer)
      vibrate([200, 120, 200])
    }, MATCH_FOUND_BUZZ_EVERY_MS)
    return true
  }
  function stopMatchFound() {
    clearInterval(buzzTimer)
    buzzTimer = undefined
    stop('matchFound')
    stopReadyLayers()
  }

  watch(
    liveState,
    (state) => {
      if (!state) {
        prev = null
        stopMatchFound()
        return
      }
      const cur = snapshot(state)
      const p = prev
      prev = cur

      if (!cur.rcWaiting) clearInterval(buzzTimer)
      if (cur.rcWaiting && (!p || !p.rcWaiting)) {
        // Fresh ready check (or the app opened while one is waiting for us): chime once, de-duplicated.
        if (startMatchFound(state)) return
      }
      if (!p) return // baseline after (re)connecting: no transition sounds

      const cues: SoundKey[] = []
      // Ready check answered: the countdown gives way to the click and (accepted) the waiting loop.
      if (p.rcResponse === 'None' && cur.rcResponse === 'Accepted') {
        stop('readyCountdown')
        cues.push('readyAccept')
        readyLayers.push(play('readyAcceptedLoop', { loop: true }))
      }
      if (p.rcResponse === 'None' && cur.rcResponse === 'Declined') {
        stopMatchFound()
        cues.push('readyDecline')
      }
      // Ready check over: everyone accepted (champ select next), or someone declined / didn't answer.
      if (p.rcActive && !cur.rcActive) {
        stopMatchFound()
        if (cur.rcState === 'EveryoneReady' || cur.phase === 'ChampSelect') cues.push('readySuccess')
        else if (p.rcResponse === 'Accepted') cues.push('readyFailed')
      }
      // Queue.
      if (!p.searching && cur.searching && (cur.phase === 'Lobby' || cur.phase === 'Matchmaking')) cues.push('queueStart')
      if (p.searching && !cur.searching && cur.inLobby) cues.push('queueCancel')
      // Party: members coming and going, invites sent from here, leadership handed to you, invites to other parties.
      if (p.inLobby && cur.inLobby) {
        if (added(p.members, cur.members).length) cues.push('memberJoined')
        if (added(cur.members, p.members).length) cues.push('memberLeft')
        if (added(p.invitesSent, cur.invitesSent).length) cues.push('inviteSent')
        if (!p.isLeader && cur.isLeader && p.members.size > 0) cues.push('leaderGranted')
      }
      if (added(p.invitesReceived, cur.invitesReceived).length) {
        cues.push('inviteReceived')
        vibrate(120)
      }
      // Champ select stages (entering champ select counts as a stage change).
      if (cur.csStage && cur.csStage !== p.csStage) {
        if (cur.csStage === 'ban') cues.push('banPhase')
        else if (cur.csStage === 'pick' || (cur.csStage === 'planning' && !p.csStage)) cues.push('pickPhase')
      }
      // Our action: "my-turn-to-ban" / "my-turn-to-pick".
      if (cur.actionHot && (!p.actionHot || p.actionId !== cur.actionId)) {
        cues.push(cur.actionType === 'ban' ? 'yourBan' : 'yourTurn')
        vibrate([180, 90, 180])
      }
      if (cur.csStage && p.csStage) {
        // Locked in / banned (from here or the PC). Not on a timed-out turn: that leaves no lock and no ban.
        if (cur.myLocked && !p.myLocked) cues.push('lockIn')
        if (cur.myBanned && !p.myBanned) cues.push(cur.simultaneousBans ? 'ban' : 'serialAllyBan')
        // Teammates' and enemies' bans (enemy bans only show one by one in serial bans; ten bans reveal them at once).
        if (cur.allyBans > p.allyBans) cues.push(cur.simultaneousBans ? 'allyBan' : 'serialAllyBan')
        if (!cur.simultaneousBans && cur.enemyBans > p.enemyBans) cues.push('serialEnemyBan')
        // The champion you declared got banned.
        if (p.myIntent > 0 && !p.bannedIds.has(p.myIntent) && cur.bannedIds.has(p.myIntent)) cues.push('pickSniped')
        // Picks locking in (draft): the very first pick of the draft has its own sting.
        if (cur.draft) {
          const first = p.allyLocks + p.enemyLocks === 0 && !p.myLocked
          if (cur.allyLocks > p.allyLocks) cues.push(first ? 'allyFirstPick' : 'allyPick')
          if (cur.enemyLocks > p.enemyLocks) cues.push(first ? 'enemyFirstPick' : 'enemyPick')
        }
        // Swaps / trades.
        for (const [k, sw] of cur.swaps) {
          const before = p.swaps.get(k)?.state
          if (sw.state === before) continue
          if (sw.state === 'RECEIVED') {
            cues.push(sw.kind === 'Position' ? 'positionSwapRequest' : 'swapRequest')
            vibrate(120)
          } else if (sw.state === 'ACCEPTED') cues.push('swapAccepted')
          else if (sw.state === 'DECLINED' && before === 'SENT') cues.push('swapDeclined')
        }
        // Bench modes: a teammate took from or put back on the bench (the client's playSoundOnAllySwap: the bench
        // changed while your own champion didn't).
        if (cur.bench !== null && p.bench !== null && cur.bench !== p.bench && cur.myChampion === p.myChampion
            && Date.now() - benchSoundAt >= BENCH_SOUND_COOLDOWN_MS) {
          benchSoundAt = Date.now()
          cues.push('benchUpdate')
        }
      }
      // Game loaded (loading screen → game clock running). The client has no sound here: synthesized.
      if (!p.gameLoaded && cur.gameLoaded) cues.push('gameStart')

      if (!cues.length) return
      cues.sort((a, b) => (PRIORITY[b] ?? 0) - (PRIORITY[a] ?? 0))
      play(cues[0]!)
    },
    // immediate: state already present at start-up is the silent baseline, so the next change already sounds.
    { flush: 'post', immediate: true },
  )

  // Chat: the client's chat sound once per update with new incoming messages in a conversation that isn't muted. Same
  // baseline rule: the first chat after (re)connecting is history. Only while the app is open (never pushed).
  let prevChat: Map<string, Set<string>> | null = null
  watch(
    liveChat,
    (chat) => {
      if (!chat) {
        prevChat = null
        return
      }
      const cur = chatSnapshot(chat)
      const p = prevChat
      prevChat = cur
      if (!p) return
      const incoming = chat.conversations.some((c) => {
        if (c.isMuted) return false
        const before = p.get(c.id)
        return c.messages.some(
          (m) =>
            m.kind === 'chat' &&
            !m.fromMe &&
            (before
              ? !before.has(m.id)
              : !!m.timestamp && Date.now() - Date.parse(m.timestamp) < NEW_ROOM_FRESH_MS),
        )
      })
      if (!incoming) return
      play('chatMessage')
      vibrate(40)
    },
    { flush: 'post', immediate: true },
  )

  // Friend requests: the client's sound for a new incoming request (not for the ones already there on connect).
  let prevRequests: Set<string> | null = null
  watch(
    liveFriends,
    (friends) => {
      if (!friends) {
        prevRequests = null
        return
      }
      const cur = requestSnapshot(friends)
      const p = prevRequests
      prevRequests = cur
      if (p && added(p, cur).length) play('friendRequest')
    },
    { flush: 'post', immediate: true },
  )

  onScopeDispose(stopMatchFound)
}
