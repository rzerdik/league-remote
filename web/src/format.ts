import { computed } from 'vue'
import { now } from './clock'
import type { ChatMessage, MatchParticipant } from './contracts'

// --- Numbers ------------------------------------------------------------------------------------------------

const integer = new Intl.NumberFormat('en-US')

/** 12345 → "12,345". */
export function formatCount(n: number): string {
  return integer.format(n)
}

/** Unspent gold: "1,250". */
export function formatGold(n: number): string {
  return integer.format(Math.floor(n))
}

/** 12345 → "12.3k", 980 → "980" (gold, damage). */
export function formatCompact(n: number): string {
  if (Math.abs(n) < 1000) return String(n)
  const k = n / 1000
  return `${k >= 100 ? Math.round(k) : k.toFixed(1).replace(/\.0$/, '')}k`
}

/** 1 → "1 game", 1240 → "1,240 games"; `many` for irregular plurals. */
export function plural(n: number, one: string, many = `${one}s`): string {
  return `${formatCount(n)} ${n === 1 ? one : many}`
}

/** "55%" of games won; '' without games. */
export function winRate(wins: number, losses: number): string {
  const games = wins + losses
  return games > 0 ? `${Math.round((wins / games) * 100)}%` : ''
}

/** "34W 28L". */
export function record(wins: number, losses: number): string {
  return `${wins}W ${losses}L`
}

/** "5.8" per minute over `seconds` of game time; '' in the first minute (the rate means nothing yet). */
export function perMinute(n: number, seconds: number): string {
  const minutes = seconds / 60
  return minutes >= 1 ? (n / minutes).toFixed(1) : ''
}

/** 1 → "1st", 3 → "3rd" (Arena placements). */
export function ordinal(n: number): string {
  const suffix = n % 100 >= 11 && n % 100 <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th'
  return `${n}${suffix}`
}

// --- K / D / A ----------------------------------------------------------------------------------------------

/** Kills, deaths, assists: a finished game's participant or a live scoreboard line. */
export type KdaLine = Pick<MatchParticipant, 'kills' | 'deaths' | 'assists'>

/** "7 / 2 / 9". */
export function kda(p: KdaLine): string {
  return `${p.kills} / ${p.deaths} / ${p.assists}`
}

/** VoiceOver for kda(): "7 kills, 2 deaths, 9 assists". */
export function kdaSpoken(p: KdaLine): string {
  return `${plural(p.kills, 'kill')}, ${plural(p.deaths, 'death')}, ${plural(p.assists, 'assist')}`
}

/**
 * "(kills + assists) / deaths" as League shows it: "4.50 KDA", "Perfect KDA" without deaths. Without the word ("4.50")
 * for a stat cell already labelled KDA.
 */
export function kdaRatio(p: KdaLine, { withWord = true } = {}): string {
  const ratio = p.deaths > 0 ? ((p.kills + p.assists) / p.deaths).toFixed(2) : p.kills + p.assists > 0 ? 'Perfect' : '0.00'
  return withWord ? `${ratio} KDA` : ratio
}

// --- Time ---------------------------------------------------------------------------------------------------

/** 754 → "12:34". */
export function formatSeconds(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds))
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

/** Ticks once a minute is plenty for "12 min ago"; `now` ticks every second, so round. */
const nowMinutes = computed(() => {
  void now.value
  return Math.floor(Date.now() / 60000)
})

const shortDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' })
const longDate = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })

/** "Just now", "12 min ago", "3 h ago", "Yesterday", "4 days ago", then the date ("Oct 3"). Reactive (ticks). */
export function relativeTime(iso: string): string {
  const at = Date.parse(iso)
  if (Number.isNaN(at)) return ''
  const minutes = Math.max(0, nowMinutes.value - Math.floor(at / 60000))
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes} min ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h ago`
  const days = Math.floor(hours / 24)
  if (days === 1) return 'Yesterday'
  if (days < 7) return `${days} days ago`
  return shortDate.format(at)
}

/** "Oct 21, 8:14 PM" in the phone's locale: the detail view's date line. */
export function matchDate(iso: string): string {
  const at = Date.parse(iso)
  return Number.isNaN(at) ? '' : longDate.format(at)
}

/** A chat message's time of day ("14:02"). */
export function messageTime(m: ChatMessage): string {
  if (!m.timestamp) return ''
  const d = new Date(m.timestamp)
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
}

/** "14:02", "Yesterday", "Mon" or a date, like Messages' conversation list. */
export function listTime(m: ChatMessage | null | undefined): string {
  if (!m?.timestamp) return ''
  const d = new Date(m.timestamp)
  if (Number.isNaN(d.getTime())) return ''
  const today = new Date()
  const days = Math.round((startOfDay(today) - startOfDay(d)) / 86400000)
  if (days <= 0) return d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })
  if (days === 1) return 'Yesterday'
  if (days < 7) return d.toLocaleDateString([], { weekday: 'short' })
  return d.toLocaleDateString([], { day: 'numeric', month: 'numeric' })
}

function startOfDay(d: Date) {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}
