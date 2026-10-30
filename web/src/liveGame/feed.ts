import { computed } from 'vue'
import type { LiveEvent } from '../contracts'
import { championName } from '../ddragon'
import { liveGame, me, SIDE_SUBJECT, sideRelation, type SideRelation } from './index'
import { dragonTitle } from './objectives'

/** The In Game event feed: the game's events in plain words, players by champion, related events folded together. */

/** One line of the feed: a game event, maybe with the events that belong to it folded in (first blood on its kill). */
export interface FeedItem {
  /** The first event's id (list key). */
  id: number
  time: number
  event: LiveEvent
  /** "Ahri killed Zed". */
  text: string
  /** Folded-in events: "First Blood", "Triple Kill", "First Tower". */
  tags: string[]
  /** Voidgrubs folded into one line. */
  count: number
}

const STREAKS: Record<number, string> = { 2: 'Double Kill', 3: 'Triple Kill', 4: 'Quadra Kill', 5: 'Penta Kill' }

/** "Triple Kill"; '' outside 2–5. */
function streakName(n: number): string {
  return STREAKS[n] ?? ''
}

/** Is this side of an event you (your own champion, named by game name only on your team)? */
function isYou(championId: number, gameName: string): boolean {
  const p = me.value?.player
  return !!p && !!gameName && championId === p.championId && gameName === p.gameName
}

/** The actor of an event by champion ("You" for yourself); '' when not a champion (turret, minions). */
function actor(e: LiveEvent, start: boolean): string {
  if (!e.actorChampionId && !e.actorChampionName) return ''
  if (isYou(e.actorChampionId, e.actorGameName)) return start ? 'You' : 'you'
  return e.actorChampionName || championName(e.actorChampionId)
}

function victim(e: LiveEvent): string {
  if (isYou(e.victimChampionId, e.victimGameName)) return 'you'
  return e.victimChampionName || championName(e.victimChampionId) || 'someone'
}

/** "Your team" / "The enemy" (spectating: "Blue Side"; side unknown: "A team") for events without a champion behind them. */
function team(e: LiveEvent): string {
  return SIDE_SUBJECT[sideRelation(e.side, e.isMyTeam)]
}

function objectiveName(e: LiveEvent): string {
  switch (e.kind) {
    case 'dragon':
      return `the ${dragonTitle(e.dragonType)}`
    case 'baron':
      return 'Baron Nashor'
    case 'herald':
      return 'the Rift Herald'
    default:
      return 'a Voidgrub'
  }
}

/** A lane as part of a sentence ("a mid turret"); '' when unknown. */
function lane(e: LiveEvent): string {
  return e.lane ? `${e.lane} ` : ''
}

/** Whose inhibitor respawned (`side` is its owner); "A" when the side is unknown ("A mid inhibitor"). */
const RESPAWNED_OWNER: Record<SideRelation, string> = {
  mine: 'Your',
  enemy: 'The enemy’s',
  blue: 'Blue Side’s',
  red: 'Red Side’s',
  unknown: 'A',
}

function capitalise(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1)
}

/** One feed line in plain words, players by champion. `count`: grubs folded into this line. */
function eventText(e: LiveEvent, count = 1): string {
  const who = actor(e, true) || team(e)
  switch (e.kind) {
    case 'kill': {
      const by = actor(e, true)
      return by ? `${by} killed ${victim(e)}` : `${capitalise(victim(e))} ${victim(e) === 'you' ? 'were' : 'was'} executed`
    }
    case 'firstBlood':
      return `${who} drew first blood`
    case 'multikill':
      return `${streakName(e.killStreak) || 'Multikill'} for ${actor(e, false) || team(e).toLowerCase()}`
    case 'ace': {
      const r = sideRelation(e.side, e.isMyTeam)
      if (r === 'mine') return 'Your team aced the enemy'
      if (r === 'enemy') return 'The enemy aced your team'
      return `${SIDE_SUBJECT[r]} scored an ace`
    }
    case 'dragon':
    case 'baron':
    case 'herald':
      return `${who} ${e.stolen ? 'stole' : 'slew'} ${objectiveName(e)}`
    case 'grub':
      return count > 1 ? `${who} slew ${count} Voidgrubs` : `${who} ${e.stolen ? 'stole' : 'slew'} a Voidgrub`
    case 'turret':
      return `${who} destroyed a ${lane(e)}turret`
    case 'firstTower':
      return `${who} took the first tower`
    case 'inhibitor':
      return `${who} destroyed the ${lane(e)}inhibitor`
    case 'inhibitorRespawned':
      return `${RESPAWNED_OWNER[sideRelation(e.side, e.isMyTeam)]} ${lane(e)}inhibitor respawned`
    case 'gameEnd':
      return e.result === 'win' ? 'Victory' : e.result === 'loss' ? 'Defeat' : 'Game over'
    default:
      return ''
  }
}

/** Events that follow the one they belong to (same second or so) and fold into it as a tag. */
const FOLD_WITHIN_S = 3
/** Grubs of one camp come seconds apart: fold them into one line. */
const GRUB_FOLD_WITHIN_S = 45

function foldTarget(items: FeedItem[], e: LiveEvent): FeedItem | undefined {
  for (let i = items.length - 1; i >= 0; i--) {
    const it = items[i]!
    if (e.time - it.time > FOLD_WITHIN_S) return undefined
    const k = it.event
    if (e.kind === 'firstTower' && k.kind === 'turret' && k.side === e.side) return it
    if ((e.kind === 'firstBlood' || e.kind === 'multikill') && k.kind === 'kill' && k.actorChampionId === e.actorChampionId)
      return it
  }
  return undefined
}

/**
 * The feed, newest first: kills with their first blood / multikill folded in, the first tower on its turret, a camp's
 * Voidgrubs as one line. Events the phone doesn't know are left out.
 */
export const feed = computed<FeedItem[]>(() => {
  const items: FeedItem[] = []
  for (const e of liveGame.value?.events ?? []) {
    if (e.kind === 'firstBlood' || e.kind === 'multikill' || e.kind === 'firstTower') {
      const target = foldTarget(items, e)
      if (target) {
        target.tags.push(e.kind === 'firstBlood' ? 'First Blood' : e.kind === 'firstTower' ? 'First Tower' : streakName(e.killStreak))
        continue
      }
    }
    const last = items[items.length - 1]
    if (e.kind === 'grub' && last?.event.kind === 'grub' && last.event.side === e.side && e.time - last.time <= GRUB_FOLD_WITHIN_S) {
      last.count += 1
      // Different champions took the grubs: the line names the team instead.
      if (last.event.actorChampionId !== e.actorChampionId) last.event = { ...last.event, actorChampionId: 0, actorChampionName: '', actorGameName: '' }
      if (e.stolen) last.event = { ...last.event, stolen: true }
      last.time = e.time
      last.text = eventText(last.event, last.count)
      continue
    }
    const text = eventText(e)
    if (text) items.push({ id: e.id, time: e.time, event: e, text, tags: [], count: 1 })
  }
  return items.reverse()
})
