import type { ClientState, CommandResult, RemoteCommand, SkinSelection } from '../../contracts'
import { store } from '../../store'
import { lobby } from './client'
import { params, push, wait } from './core'
import { KESTREL, LUMEN, ME, NIGHTOWL, riotIdOf } from './people'

const ALL_CHAMPS = [266, 103, 84, 166, 12, 32, 34, 1, 523, 22, 136, 268, 432, 53, 63, 201, 51, 164, 69, 31, 42, 122, 131, 119, 36, 245, 60, 28, 81, 9, 114, 105, 3, 41, 86, 150, 79, 104, 887, 120, 74, 420, 39, 427, 40, 59, 24, 126, 202, 222, 145, 429, 43, 30, 38, 55, 10, 141, 85, 121, 203, 240, 96, 7, 64, 89, 876, 127, 236, 117, 99, 54, 90, 57, 11, 21, 62, 82, 25, 267, 75, 111, 518, 76, 56, 20, 2, 61, 516, 80, 78, 555, 246, 133, 497, 33, 421, 526, 58, 107, 92, 68, 13, 360, 113, 235, 147, 875, 35, 98, 102, 27, 14, 15, 72, 37, 16, 50, 517, 134, 223, 163, 91, 44, 17, 412, 18, 48, 23, 4, 29, 77, 6, 110, 67, 45, 161, 711, 254, 234, 112, 8, 106, 19, 498, 101, 5, 157, 777, 83, 350, 154, 238, 221, 115, 26, 142, 143]

export type CsStage = 'planning' | 'ban' | 'enemypick' | 'pick' | 'final'

/**
 * Ranked draft, you are blue mid (cell 2). Turns: 10 simultaneous bans, reveal, then B1 (0) · R1 R2 (5, 6) ·
 * B2 B3 (1, 2) · R3 R4 (7, 8) · B4 B5 (3, 4) · R5 (9). 'enemypick' = R1 R2 picking; 'pick' = you and Jungle picking.
 */
export function champSelect(stage: CsStage): ClientState['champSelect'] {
  const order = [[0], [5, 6], [1, 2], [7, 8], [3, 4], [9]]
  // Index of the pick turn in progress (-1: none yet, 6: all done).
  const now = { planning: -1, ban: -1, enemypick: 1, pick: 2, final: 6 }[stage]
  const banning = stage === 'ban'
  const picksStarted = now >= 0
  const pickOrderOf = (cell: number) => order.flat().indexOf(cell) + 1
  const turnOf = (cell: number) => order.findIndex((t) => t.includes(cell))
  const locked = (cell: number) => turnOf(cell) < now
  const acting = (cell: number) => turnOf(cell) === now
  // Teammates League names carry a puuid (View Profile in their menu) and their Riot ID; support (cell 4) is hidden, as
  // in a ranked draft.
  const named = [NIGHTOWL, KESTREL, ME, LUMEN]
  const ally = (cellId: number, position: string, champ: number, spells: [number, number], ban: number) => ({
    cellId,
    isLocalPlayer: cellId === 2,
    puuid: named[cellId]?.puuid ?? '',
    riotId: cellId in named ? riotIdOf(named[cellId]!) : '',
    championId: locked(cellId) || (acting(cellId) && cellId !== 2) ? champ : 0,
    championPickIntent: champ,
    assignedPosition: position,
    spell1Id: spells[0],
    spell2Id: spells[1],
    isLocked: locked(cellId),
    isActing: banning ? cellId !== 2 && cellId !== 0 : acting(cellId),
    pickOrder: pickOrderOf(cellId),
    banChampionId: banning && cellId === 1 ? 0 : ban,
    hasBanned: !banning || cellId === 0,
  })
  const enemy = (cellId: number, position: string, champ: number) => ({
    cellId,
    championId: locked(cellId) || acting(cellId) ? champ : 0,
    championPickIntent: 0,
    assignedPosition: position,
    isLocked: locked(cellId),
    isActing: acting(cellId),
    pickOrder: pickOrderOf(cellId),
  })
  return {
    localPlayerCellId: 2,
    timerPhase: stage === 'final' ? 'FINALIZATION' : stage === 'planning' ? 'PLANNING' : 'BAN_PICK',
    timeLeftMs: stage === 'final' ? 28000 : 24000,
    myTeam: [
      ally(0, 'top', 86, [4, 12], 157),
      ally(1, 'jungle', 64, [4, 11], 777),
      ally(2, 'middle', 103, [4, 14], 11),
      ally(3, 'bottom', 51, [4, 7], 122),
      ally(4, 'utility', 412, [4, 3], 555),
    ],
    myAction:
      stage === 'final'
        ? null
        : banning
          ? { id: 3, type: 'ban', championId: 0, isInProgress: true, completed: false }
          : { id: 12, type: 'pick', championId: 103, isInProgress: stage === 'pick', completed: false },
    // 6000x: ids League lists that aren't champions (must not show up in the grid).
    pickableChampionIds: [60001, 60002, ...ALL_CHAMPS],
    bannableChampionIds: [60001, 60002, ...ALL_CHAMPS],
    theirTeam: [enemy(5, '', 238), enemy(6, '', 254), enemy(7, '', 99), enemy(8, '', 222), enemy(9, '', 89)],
    turns: [
      {
        type: 'ban',
        team: 'both',
        cellIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9],
        isInProgress: banning,
        completed: picksStarted,
      },
      { type: 'reveal', team: 'none', cellIds: [], isInProgress: false, completed: picksStarted },
      ...order.map((cellIds, i) => ({
        type: 'pick',
        team: cellIds[0] < 5 ? 'ally' : 'enemy',
        cellIds,
        isInProgress: i === now,
        completed: i < now,
      })),
    ],
    mySide: 'blue',
    isTimerInfinite: false,
    allowSubsetChampionPicks: false,
    benchEnabled: false,
    benchChampionIds: null,
    canBenchSwap: false,
    allowRerolling: false,
    rerollsRemaining: 0,
    canReroll: false,
    skins: null,
    bans: picksStarted ? { myTeamBans: [157, 777, 11, 122, 555], theirTeamBans: [236, 17, 245, 133, 84] } : { myTeamBans: [], theirTeamBans: [] },
    swaps: [
      { kind: 'PickOrder', id: 11, cellId: 0, state: 'AVAILABLE' },
      { kind: 'Position', id: 12, cellId: 0, state: 'AVAILABLE' },
      { kind: 'PickOrder', id: 13, cellId: 1, state: 'SENT' },
      // `&swap=1` on `pick`: Lumen asks to swap roles (the banner covers the top of the board).
      { kind: 'Position', id: 14, cellId: 3, state: stage === 'pick' && params.get('swap') === '1' ? 'RECEIVED' : 'AVAILABLE' },
    ],
    canDodge: true,
  }
}

// --- ARAM ---------------------------------------------------------------------------------------------------

/**
 * `aram`: ARAM (queue 450) in FINALIZATION, as the connector maps a bench session: champions dealt to everyone (no pick
 * actions, no turns), a bench of eight, one reroll left, a 60 s timer. ARAM spells (Mark) on everyone. A champion you
 * can't play (not in pickableChampionIds) sits on the bench grayed. `&rerolls=0` has no rerolls left, `&bench=closed`
 * shows the bench before it opens. A teammate takes a bench champion every few seconds (aramAllySwaps).
 */
const ARAM_LOCKED_OUT = 147 // Seraphine: on the bench, but not yours to play
export function aramSelect(): ClientState['champSelect'] {
  // ARAM names everyone on your team (no anonymous draft there).
  const NAMES = [riotIdOf(NIGHTOWL), riotIdOf(KESTREL), riotIdOf(ME), riotIdOf(LUMEN), 'Quillon#EUW']
  const ally = (cellId: number, champ: number, spell2: number) => ({
    cellId,
    isLocalPlayer: cellId === 2,
    riotId: NAMES[cellId] ?? '',
    puuid: `demo-${(NAMES[cellId] ?? '').split('#')[0]!.toLowerCase()}`,
    championId: champ,
    championPickIntent: 0,
    assignedPosition: '',
    spell1Id: 4,
    spell2Id: spell2,
    isLocked: true,
    isActing: false,
    pickOrder: 0,
    banChampionId: 0,
    hasBanned: false,
  })
  const bench = [22, 51, 99, ARAM_LOCKED_OUT, 13, 117, 69, 498]
  const closed = params.get('bench') === 'closed'
  const rerolls = Number(params.get('rerolls') ?? 1)
  return {
    localPlayerCellId: 2,
    timerPhase: closed ? 'BAN_PICK' : 'FINALIZATION',
    timeLeftMs: 60000,
    myTeam: [ally(0, 86, 32), ally(1, 64, 32), ally(2, 103, 32), ally(3, 157, 7), ally(4, 412, 3)],
    myAction: null,
    pickableChampionIds: ALL_CHAMPS.filter((id) => id !== ARAM_LOCKED_OUT),
    bannableChampionIds: [],
    theirTeam: [5, 6, 7, 8, 9].map((cellId) => ({
      cellId,
      championId: 0,
      championPickIntent: 0,
      assignedPosition: '',
      isLocked: false,
      isActing: false,
      pickOrder: 0,
    })),
    bans: { myTeamBans: [], theirTeamBans: [] },
    swaps: [{ kind: 'Trade', id: 41, cellId: 0, state: 'AVAILABLE' }],
    turns: [],
    mySide: 'blue',
    isTimerInfinite: false,
    allowSubsetChampionPicks: false,
    benchEnabled: true,
    benchChampionIds: bench,
    canBenchSwap: !closed,
    allowRerolling: true,
    rerollsRemaining: rerolls,
    canReroll: !closed && rerolls > 0,
    skins: null,
    canDodge: true,
  }
}

/** Champions a reroll deals in the demo (not on the bench, not on your team). */
const ARAM_REROLLS = [136, 268, 432, 53, 63, 201]

/** A teammate trades with the bench now and then (the bench chimes), so the bench is seen changing under you. */
export function aramAllySwaps() {
  setInterval(() => {
    const cs = store.state?.champSelect
    if (!cs?.benchChampionIds?.length) return
    const ally = cs.myTeam.find((m) => m.cellId === 4)!
    const i = cs.benchChampionIds.findIndex((id) => id !== ARAM_LOCKED_OUT)
    const taken = cs.benchChampionIds[i]!
    const bench = [...cs.benchChampionIds]
    bench.splice(i, 1)
    bench.push(ally.championId)
    push({
      ...store.state!,
      champSelect: { ...cs, benchChampionIds: bench, myTeam: cs.myTeam.map((m) => (m === ally ? { ...m, championId: taken } : m)) },
    })
  }, 9000)
}

/** BenchSwap / Reroll the way League answers: your champion and the bench trade places after a short delay. */
export async function demoBench(cmd: RemoteCommand): Promise<CommandResult> {
  const fail = (error: string) => ({ requestId: cmd.requestId, success: false, error })
  await wait(450)
  const cs = store.state?.champSelect
  const me = cs?.myTeam.find((m) => m.isLocalPlayer)
  if (!cs || !me) return fail('Not in champ select.')
  const bench = [...(cs.benchChampionIds ?? [])]
  let next: number
  let rerollsRemaining = cs.rerollsRemaining
  if (cmd.type === 'BenchSwap') {
    const i = bench.indexOf(cmd.championId ?? 0)
    if (!cs.canBenchSwap) return fail("The bench isn't open yet.")
    if (i < 0) return fail('That champion is no longer on the bench.')
    if (!cs.pickableChampionIds.includes(bench[i]!)) return fail("You can't play that champion.")
    next = bench[i]!
    bench[i] = me.championId
  } else {
    if (!cs.canReroll) return fail('No rerolls left.')
    next = ARAM_REROLLS.find((id) => !bench.includes(id) && !cs.myTeam.some((m) => m.championId === id)) ?? 1
    bench.push(me.championId)
    if (bench.length > 10) bench.shift()
    rerollsRemaining--
  }
  push({
    ...store.state!,
    champSelect: {
      ...cs,
      benchChampionIds: bench,
      rerollsRemaining,
      canReroll: rerollsRemaining > 0 && cs.canBenchSwap,
      myTeam: cs.myTeam.map((m) => (m.isLocalPlayer ? { ...m, championId: next } : m)),
    },
  })
  return { requestId: cmd.requestId, success: true }
}

// --- Final seconds ------------------------------------------------------------------------------------------

/**
 * `pick-final` / `ban-final`: your turn with ~12.5 s left, so the final-seconds edge glow and the client's timer tick
 * (from its "10" to "0": 11 ticks over the last 11 s) can be reviewed; the turn restarts every FINAL_LOOP_MS until you
 * lock in. `n` keeps each restart a new timer reading.
 */
const FINAL_DEMO_MS = 12500
const FINAL_LOOP_MS = 14000
export function finalSeconds(cs: ClientState['champSelect'], n = 0): ClientState['champSelect'] {
  return cs && { ...cs, timeLeftMs: FINAL_DEMO_MS + n }
}

export function loopFinalSeconds() {
  let n = 0
  setInterval(() => {
    const cs = store.state?.champSelect
    if (cs?.myAction?.isInProgress) push({ ...store.state!, champSelect: finalSeconds(cs, ++n) })
  }, FINAL_LOOP_MS)
}

// --- Skins --------------------------------------------------------------------------------------------------

/**
 * `?demo=skins`: finalization with Ahri locked in, wearing Dynasty Ahri. Star Guardian and Elderwood have chromas (one
 * with two colours, a tiered form without any); Data Dragon has art for every skin number used here.
 */
export function demoSkins(): SkinSelection {
  const chroma = (id: number, name: string, ...colors: string[]) => ({ id, name, colors })
  return {
    championId: 103,
    selectedSkinId: 103001,
    skins: [
      { id: 103000, name: 'Ahri', isBase: true, chromas: [] },
      { id: 103001, name: 'Dynasty Ahri', isBase: false, chromas: [] },
      { id: 103007, name: 'Arcade Ahri', isBase: false, chromas: [] },
      {
        id: 103014,
        name: 'Star Guardian Ahri',
        isBase: false,
        chromas: [
          chroma(103040, 'Star Guardian Ahri (Rose Quartz)', '#F0A6DE', '#5BB9E8'),
          chroma(103041, 'Star Guardian Ahri (Obsidian)', '#2B2633'),
          chroma(103042, 'Star Guardian Ahri (Citrine)', '#F2C14E'),
          chroma(103043, 'Star Guardian Ahri (Prestige Form)'),
        ],
      },
      { id: 103015, name: 'K/DA Ahri', isBase: false, chromas: [] },
      {
        id: 103017,
        name: 'Elderwood Ahri',
        isBase: false,
        chromas: [chroma(103050, 'Elderwood Ahri (Ruby)', '#C8303C'), chroma(103051, 'Elderwood Ahri (Emerald)', '#2E9E6A', '#E7D27C')],
      },
      { id: 103027, name: 'Spirit Blossom Ahri', isBase: false, chromas: [] },
    ],
  }
}

// --- Commands -----------------------------------------------------------------------------------------------

/**
 * Dodge: League ends the draft and puts you back in your lobby (not searching), with its queue lockout. Skins: League
 * takes a moment, then the next push wears it (the connector refuses ids the carousel doesn't list).
 */
export async function demoDraftCommand(cmd: RemoteCommand): Promise<CommandResult | null> {
  if (cmd.type === 'DodgeChampSelect') {
    await wait(500)
    if (!store.state?.champSelect?.canDodge) return { requestId: cmd.requestId, success: false, error: "You can't dodge right now." }
    push({ ...store.state!, gameflowPhase: 'Lobby', champSelect: null, lobby: lobby(false) })
    return { requestId: cmd.requestId, success: true }
  }
  if (cmd.type === 'SelectSkin') {
    await wait(700)
    const s = store.state?.champSelect?.skins
    const listed = s?.skins.some((k) => k.id === cmd.skinId || k.chromas.some((c) => c.id === cmd.skinId))
    if (!s || !listed) return { requestId: cmd.requestId, success: false, error: "That skin isn't available." }
    push({ ...store.state!, champSelect: { ...store.state!.champSelect!, skins: { ...s, selectedSkinId: cmd.skinId! } } })
    return { requestId: cmd.requestId, success: true }
  }
  return null
}

/**
 * Your ban or pick lands (Lock In, or the fake automation). A ban walks on through the rest of the draft (bans end →
 * enemies pick → your turn), then `onPickTurn` runs (the auto pick demo hovers and locks your pick next).
 */
export function completeMyAction(id: number, onPickTurn: () => void) {
  const cs = store.state?.champSelect
  if (!cs) return
  const me = cs.myTeam.find((m) => m.isLocalPlayer)
  if (cs.myAction?.type === 'ban') {
    if (me) Object.assign(me, { banChampionId: id, hasBanned: true })
    cs.myAction = { id: 12, type: 'pick', championId: 103, isInProgress: false, completed: false }
    void (async () => {
      await wait(2500)
      push({ ...store.state!, champSelect: champSelect('enemypick') })
      await wait(4000)
      push({ ...store.state!, champSelect: champSelect('pick') })
      onPickTurn()
    })()
  } else if (me) {
    Object.assign(me, { championId: id, isLocked: true, isActing: false })
    cs.myAction = null
  }
  // A new snapshot, like a connector push (in-place edits alone don't reach the sound cues' watcher).
  push({ ...store.state!, champSelect: { ...cs } })
}
