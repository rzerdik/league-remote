import { computed } from 'vue'
import { anchoredGameClock, now } from '../clock'
import type { LiveGameState, LivePlayer, LiveTeam } from '../contracts'
import { championName, itemGold, spellKeyById } from '../ddragon'
import { formatCompact, perMinute } from '../format'
import { positionLabel } from '../labels'
import { liveGameState, store } from '../store'
import { hasTrinkets, mapSide } from '../teams'

/**
 * The In Game screen's live analysis: the running game as the connector reads it from the game's own Live Client Data
 * API (LiveGameUpdated). The connector enforces privacy: enemies come without names (only their champion), so
 * everything here names players by champion except your own team. The event feed, objective and buff timers live
 * next to this module (`feed.ts`, `objectives.ts`, `buffs.ts`).
 */

/** The running game; null while there is none (loading screen: use ClientState.inGame then). */
export const liveGame = computed<LiveGameState | null>(() => {
  const g = liveGameState.value
  return g?.available ? g : null
})

export const me = computed(() => liveGame.value?.me ?? null)

/** The running game's map has trinkets (not ARAM or Arena): loadouts draw the empty trinket well only then. */
export const liveHasTrinkets = computed(() => !liveGame.value || hasTrinkets(liveGame.value.mapNumber))

/** Your team first (the connector orders them), then the others. */
export const teams = computed<LiveTeam[]>(() => liveGame.value?.teams ?? [])

/**
 * Whether you play in this game (not spectating, `me` matched): only then is any team "yours" or "the enemy". Without
 * it every team has isMyTeam false, so naming one "the enemy" would be wrong for both: the UI says Blue / Red Side.
 */
export const knowsMyTeam = computed(() => teams.value.some((t) => t.isMyTeam))

// --- Sides ----------------------------------------------------------------------------------------------------

/**
 * Whose a side is, as the UI words it: `mine` / `enemy` only when your team is known and the side is; `blue` / `red`
 * (the side itself) when spectating; `unknown` when the side is '' (the connector couldn't tell who scored).
 */
export type SideRelation = 'mine' | 'enemy' | 'blue' | 'red' | 'unknown'

export function sideRelation(side: string, isMyTeam: boolean): SideRelation {
  if (!side) return 'unknown'
  if (knowsMyTeam.value) return isMyTeam ? 'mine' : 'enemy'
  return mapSide(side) ?? 'unknown'
}

/** A team's own side relation (scoreboard, inhibitors, score). */
export function teamRelation(side: string): SideRelation {
  return sideRelation(side, teams.value.find((t) => t.side === side)?.isMyTeam ?? false)
}

/**
 * The relation as the subject of a sentence: "Your team", "The enemy", "Blue Side", "Red Side", "A team". The map
 * sides are names (the scoreboard's headers), so they keep their capitals mid-sentence too.
 */
export const SIDE_SUBJECT: Record<SideRelation, string> = {
  mine: 'Your team',
  enemy: 'The enemy',
  blue: 'Blue Side',
  red: 'Red Side',
  unknown: 'A team',
}

// --- Clock ----------------------------------------------------------------------------------------------------

const clock = anchoredGameClock()

/** Interpolated game clock (seconds); null without a running game, stopped once it has a result. Ticks with `now`. */
export const liveClock = computed<number | null>(() => {
  void now.value
  const g = liveGame.value
  if (!g) return null
  const t = clock(g.gameTime, store.receivedAt.liveGame)
  return g.result ? g.gameTime : t
})

/** How far the clock has run since the snapshot (respawn timers are "seconds left at the snapshot"). */
function sinceSnapshot(): number {
  const g = liveGame.value
  const t = liveClock.value
  return g && t != null ? Math.max(0, t - g.gameTime) : 0
}

// --- Players --------------------------------------------------------------------------------------------------

/** The game's champion name ("Wukong"), or Data Dragon's for an id the game didn't name. */
export function champName(p: Pick<LivePlayer, 'championId' | 'championName'>): string {
  return p.championName || championName(p.championId) || 'Unknown'
}

/** Your own team by game name; everyone else by champion (the connector never sends enemy names). */
export function playerLabel(p: LivePlayer): string {
  return p.gameName || champName(p)
}

/** Seconds until a dead player respawns, counting down on the interpolated clock; 0 while alive. */
export function respawnLeft(p: LivePlayer): number {
  if (!p.isDead) return 0
  return Math.max(0, p.respawnSeconds - sinceSnapshot())
}

/** "8.1" on the snapshot's clock (so the rate doesn't drift between updates); '' in the first minute. */
export function csPerMinute(p: LivePlayer): string {
  return perMinute(p.creepScore, liveGame.value?.gameTime ?? 0)
}

/** "Level 11 · Mid" — the position only when the game says it. */
export function levelLine(p: LivePlayer): string {
  return [`Level ${p.level}`, positionLabel(p.position)].filter(Boolean).join(' · ')
}

const SLOTS = 7

/** A live scoreboard line in the shape LoadoutStrip draws (items by slot, spells by Data Dragon key). */
export function loadoutOf(p: LivePlayer): {
  items: number[]
  spell1Id: number
  spell2Id: number
  keystone: number
  spellNames: [string, string]
} {
  const items = new Array<number>(SLOTS).fill(0)
  for (const i of p.items) if (i.slot >= 0 && i.slot < SLOTS) items[i.slot] = i.itemId
  return {
    items,
    spell1Id: spellKeyById(p.spell1.id),
    spell2Id: spellKeyById(p.spell2.id),
    keystone: p.keystone,
    spellNames: [p.spell1.name, p.spell2.name],
  }
}

/**
 * What a team's items are worth at Data Dragon prices (stacks counted), as an estimate of gold spent: the API reports
 * gold only for you. Null until the prices are loaded (loadDdragonExtras).
 */
export function teamItemValue(team: LiveTeam): number | null {
  let total = 0
  let known = false
  for (const p of team.players) {
    for (const i of p.items) {
      const price = itemGold(i.itemId)
      if (price == null) continue
      known = true
      total += price * Math.max(1, i.count)
    }
  }
  return known ? total : null
}

/** Below this the teams' items count as even (a potion or two). */
const ITEM_LEAD_EVEN = 150

/**
 * Under your team on the scoreboard: how far ahead or behind its items are, against a single enemy team; '' when
 * spectating, with several enemy teams or no prices yet.
 */
export function itemLead(): string {
  const mine = teams.value.find((t) => t.isMyTeam)
  const theirs = teams.value.filter((t) => !t.isMyTeam)
  if (!mine || theirs.length !== 1) return ''
  const a = teamItemValue(mine)
  const b = teamItemValue(theirs[0]!)
  if (a == null || b == null) return ''
  const d = a - b
  if (Math.abs(d) < ITEM_LEAD_EVEN) return 'Both teams’ items are worth about the same (estimated from item prices).'
  return `Your team’s items are worth about ${formatCompact(Math.abs(d))} gold ${d > 0 ? 'more' : 'less'} (estimated from item prices).`
}
