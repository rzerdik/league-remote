import { reactive, watch } from 'vue'
import { isRecord, KEYS, readJson, writeJson } from '../lib/storage'

/** What a sound belongs to, for the per-category switches in Settings. */
export type SoundCategory = 'readyCheck' | 'champSelect' | 'countdown' | 'party' | 'social' | 'taps'

export const SOUND_CATEGORIES: readonly { id: SoundCategory; title: string }[] = [
  { id: 'readyCheck', title: 'Ready Check' },
  { id: 'champSelect', title: 'Champion Select' },
  { id: 'countdown', title: 'Timer Ticks' },
  { id: 'party', title: 'Party and Queue' },
  { id: 'social', title: 'Chat and Friends' },
  { id: 'taps', title: 'Taps' },
]

export interface SoundSettings {
  /** Sound effects off (music has its own switch). */
  muted: boolean
  volume: number
  /** The client's music and ambience loops. */
  music: boolean
  musicVolume: number
  /** Per-category switches; a missing entry is on. */
  categories: Record<SoundCategory, boolean>
  /** Keep sounds and music playing while the app is in the background or the phone is locked. */
  background: boolean
}

function allCategories(on: boolean): Record<SoundCategory, boolean> {
  return Object.fromEntries(SOUND_CATEGORIES.map((c) => [c.id, on])) as Record<SoundCategory, boolean>
}

function unit(v: unknown, fallback: number): number {
  return typeof v === 'number' && v >= 0 && v <= 1 ? v : fallback
}

const DEFAULTS: SoundSettings = {
  muted: false,
  volume: 0.7,
  music: true,
  musicVolume: 0.5,
  categories: allCategories(true),
  background: true,
}

/** Stored settings over the defaults; malformed fields keep their default. */
function parse(s: unknown): SoundSettings | null {
  if (!isRecord(s)) return null
  const categories = allCategories(true)
  if (isRecord(s.categories)) for (const c of SOUND_CATEGORIES) categories[c.id] = s.categories[c.id] !== false
  return {
    muted: s.muted === true,
    volume: unit(s.volume, DEFAULTS.volume),
    music: s.music !== false,
    musicVolume: unit(s.musicVolume, DEFAULTS.musicVolume),
    categories,
    background: s.background !== false,
  }
}

/** Settings › Sounds & Haptics, kept on this phone. */
export const soundSettings = reactive(readJson(KEYS.sound, parse) ?? { ...DEFAULTS, categories: allCategories(true) })

watch(
  () => JSON.stringify(soundSettings),
  () => writeJson(KEYS.sound, soundSettings), // storage unavailable: in memory only
)

/**
 * Paused from the lock screen or Control Center (`session.ts`, Media Session): sounds and music stay quiet until they
 * are played there again or the app is touched. Not persisted.
 */
export const playback = reactive({ userPaused: false })
