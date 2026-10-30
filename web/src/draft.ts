import type { DotTone } from './components/lr/StatusDot.vue'
import type { ChampSelectState, DraftTurn, EnemyMember, SwapOffer, TeamMember } from './contracts'
import { championName } from './ddragon'
import { positionLabel } from './labels'

/**
 * Where the draft stands, read from League's own turn list (ChampSelectState.turns).
 *
 * Ranked / Draft Pick run: Declare Intent (PLANNING) → one ban turn where all ten ban at once → the ban reveal →
 * six pick turns (blue 1, red 2, blue 2, red 2, blue 2, red 1) → Finalization → Game Starting. Blind pick is a single
 * pick turn for everyone; tournament draft alternates ban and pick turns. All of these are just turns here.
 */
export type DraftStage = 'planning' | 'banning' | 'reveal' | 'picking' | 'finalization' | 'starting' | 'waiting'

export type DraftTone = 'you-pick' | 'you-ban' | 'ally' | 'enemy' | 'neutral'

/** The status dot of a draft tone: the acting side's colour (blue = your team, red = the enemy), yours in the accent. */
export const DRAFT_DOT: Record<DraftTone, DotTone> = {
  'you-pick': 'accent',
  'you-ban': 'red',
  ally: 'blue',
  enemy: 'red',
  neutral: 'gray',
}

export interface DraftView {
  stage: DraftStage
  /** The turn being played now (null during PLANNING and FINALIZATION). */
  current: DraftTurn | null
  /** The local player's next uncompleted pick/ban turn. */
  myTurn: DraftTurn | null
  /** Pick/ban turns before mine starts: 0 = it's mine now, 1 = next, ...; null when none is left. */
  turnsUntilMine: number | null
  /** Short status for the header, e.g. "Your Pick", "Enemy Picks". */
  title: string
  /** When your turn comes, e.g. "You're next"; empty when the strip says it all. */
  detail: string
  tone: DraftTone
}

const isPlayerTurn = (t: DraftTurn) => t.type === 'pick' || t.type === 'ban'

/** Your own pick or ban is running now (not just coming up, not done). */
export function myTurnNow(cs: ChampSelectState): boolean {
  const a = cs.myAction
  return !!a && a.isInProgress && !a.completed
}

/** Swap and trade requests from teammates waiting for your answer. */
export function receivedSwaps(cs: ChampSelectState): SwapOffer[] {
  return (cs.swaps ?? []).filter((s) => s.id > 0 && s.state === 'RECEIVED')
}

/** A team in pick order (a pick-order swap moves the actions, not the cells). */
export function byPickOrder<T extends { pickOrder: number; cellId: number }>(team: readonly T[]): T[] {
  return [...team].sort((a, b) => (a.pickOrder || 99) - (b.pickOrder || 99) || a.cellId - b.cellId)
}

/**
 * A teammate row's actions button (⋯): swaps with them (`offers`: AVAILABLE or SENT ones), or View Profile while League
 * shows who they are (`puuid`). Never on your own row.
 */
export function hasMemberActions(m: TeamMember, offers: readonly SwapOffer[]): boolean {
  return !m.isLocalPlayer && (offers.length > 0 || !!m.puuid)
}

/**
 * The team lists' line for a champion hovered (or declared) but not locked in, the same for both teams: the portrait
 * stays at full opacity and this word says it isn't locked yet.
 */
export function hoveringLine(championId: number): string | undefined {
  const name = championName(championId)
  return name ? `Hovering ${name}` : undefined
}

export function draftView(cs: ChampSelectState): DraftView {
  const turns = cs.turns
  const myCell = cs.localPlayerCellId
  const currentIndex = turns.findIndex((t) => t.isInProgress)
  const current = currentIndex >= 0 ? turns[currentIndex] : null
  const myIndex = turns.findIndex((t) => isPlayerTurn(t) && !t.completed && t.cellIds.includes(myCell))
  const myTurn = myIndex >= 0 ? turns[myIndex] : null
  const turnsUntilMine =
    myIndex < 0
      ? null
      : turns.slice(Math.max(0, currentIndex), myIndex).filter((t) => isPlayerTurn(t) && !t.completed).length

  const stage = stageOf(cs, current)
  const view: DraftView = { stage, current, myTurn, turnsUntilMine, title: '', detail: '', tone: 'neutral' }
  const upNext = myUpNext(view)
  const mineNow = myTurnNow(cs)

  switch (stage) {
    case 'planning':
      view.title = 'Declare Intent'
      break
    case 'banning': {
      const banningNow = mineNow && cs.myAction?.type === 'ban'
      view.title = banningNow ? 'Your Ban' : current?.team === 'enemy' ? 'Enemy Bans' : 'Bans'
      view.tone = banningNow ? 'you-ban' : current?.team === 'enemy' ? 'enemy' : 'neutral'
      break
    }
    case 'reveal':
      view.title = 'Bans'
      view.detail = upNext
      break
    case 'picking': {
      if (mineNow && cs.myAction?.type === 'pick') {
        view.title = 'Your Pick'
        view.tone = 'you-pick'
      } else if (current?.team === 'enemy') {
        view.title = 'Enemy Picks'
        view.tone = 'enemy'
        view.detail = upNext
      } else {
        view.title = 'Ally Picks'
        view.tone = 'ally'
        view.detail = upNext
      }
      break
    }
    case 'finalization':
      view.title = 'Finalization'
      break
    case 'starting':
      view.title = 'Game Starting'
      break
    default:
      view.title = 'Champion Select'
      view.detail = upNext
  }
  return view
}

function stageOf(cs: ChampSelectState, current: DraftTurn | null): DraftStage {
  switch (cs.timerPhase) {
    case 'PLANNING':
      return 'planning'
    case 'FINALIZATION':
      return 'finalization'
    case 'GAME_STARTING':
      return 'starting'
  }
  if (current?.type === 'ban') return 'banning'
  if (current?.type === 'pick') return 'picking'
  if (current?.type === 'reveal') return 'reveal'
  // No turns at all (bench modes deal champions): nobody picks.
  return cs.turns.length && cs.timerPhase === 'BAN_PICK' ? 'reveal' : 'waiting'
}

/** The same as a tag in the picker's turn header (Title Case): "You're Next", "In 3 Turns", or "". */
export function upNextTag(view: DraftView): string {
  return myUpNext(view)
    .split(' ')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ')
}

/** "You're next", "In 3 turns", or "" (not yours, or nothing left). */
function myUpNext(view: DraftView): string {
  const n = view.turnsUntilMine
  if (n === null || n === 0 || !view.myTurn) return ''
  return n === 1 ? "You're next" : `In ${n} turns`
}

/** One pick slot of the draft order strip. */
export interface DraftSlot {
  cellId: number
  ally: boolean
  you: boolean
  championId: number
  locked: boolean
  acting: boolean
  label: string
}

/** Pick turns with their slots, for the draft order strip; empty for blind pick (a single simultaneous turn). */
export function pickTurns(cs: ChampSelectState, myHoverId = 0): { turn: DraftTurn; slots: DraftSlot[] }[] {
  const picks = cs.turns.filter((t) => t.type === 'pick')
  if (picks.length < 2) return []
  const allies = new Map(cs.myTeam.map((m) => [m.cellId, m]))
  const enemies = new Map<number, EnemyMember>((cs.theirTeam ?? []).map((e) => [e.cellId, e]))
  return picks.map((turn) => ({
    turn,
    slots: turn.cellIds.map((cellId) => {
      const a = allies.get(cellId)
      if (a) {
        const you = a.isLocalPlayer
        const hover = you && myHoverId > 0 && !a.isLocked ? myHoverId : 0
        return {
          cellId,
          ally: true,
          you,
          championId: hover || a.championId || a.championPickIntent,
          locked: a.isLocked,
          acting: a.isActing,
          label: you ? 'You' : positionLabel(a.assignedPosition) || 'Teammate',
        }
      }
      const e = enemies.get(cellId)
      return {
        cellId,
        ally: false,
        you: false,
        championId: e?.championId || 0,
        locked: !!e?.isLocked,
        acting: !!e?.isActing,
        label: positionLabel(e?.assignedPosition) || 'Enemy',
      }
    }),
  }))
}

