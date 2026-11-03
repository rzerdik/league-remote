/**
 * Everything this phone keeps in localStorage, in one place. Storage can be missing, full or blocked (private mode,
 * quota): reads then give the fallback and writes keep the value in memory only, never an exception.
 */
export const KEYS = {
  /** The pairing grant (store.ts), shared by every tab of the app. */
  grant: 'league-remote.grant',
  /** Which events push to this phone (push.ts). */
  pushPreferences: 'league-remote.pushPreferences',
  /** The Play tab's "Get Notified" tip was dismissed (notifications.ts). */
  notifyHintDismissed: 'league-remote.notifyHintDismissed',
  /** Sound and music settings (sound/settings.ts). */
  sound: 'league-remote.sound',
  /** Haptics on or off (haptics.ts). */
  haptics: 'league-remote.haptics',
  /** This phone's glass amount, when it isn't the default (glassAmount.ts). */
  glassAmount: 'league-remote.glassAmount',
  /** Prefix of the favourite skins, one key per pairing id (favouriteSkins.ts). */
  favouriteSkinsPrefix: 'league-remote.favouriteSkins.',
  /** Data Dragon champions and spells (ddragon.ts). */
  ddragon: 'league-remote.ddragon.v3',
  /** Data Dragon items and runes (ddragon.ts). */
  ddragonExtras: 'league-remote.ddragon.extras.v2',
  /** sessionStorage: when the app last reloaded for missing code (lib/reload.ts). */
  preloadReloadAt: 'league-remote.preloadReloadAt',
} as const

/** The raw string stored under `key`, or null (missing or storage unavailable). */
export function readText(key: string, storage: 'local' | 'session' = 'local'): string | null {
  try {
    return (storage === 'local' ? localStorage : sessionStorage).getItem(key)
  } catch {
    return null
  }
}

/** Store `value` under `key` (null removes it). Returns false when storage refused it. */
export function writeText(key: string, value: string | null, storage: 'local' | 'session' = 'local'): boolean {
  try {
    const s = storage === 'local' ? localStorage : sessionStorage
    if (value === null) s.removeItem(key)
    else s.setItem(key, value)
    return true
  } catch {
    return false
  }
}

/** The JSON stored under `key`, checked by `parse` (which returns null for anything malformed). */
export function readJson<T>(key: string, parse: (raw: unknown) => T | null): T | null {
  const text = readText(key)
  if (text === null) return null
  try {
    return parse(JSON.parse(text))
  } catch {
    return null
  }
}

/** Store `value` as JSON under `key` (null removes it). Returns false when storage refused it. */
export function writeJson(key: string, value: unknown): boolean {
  return writeText(key, value === null ? null : JSON.stringify(value))
}

/** Every localStorage key starting with `prefix` (empty when storage is unavailable). */
export function keysWithPrefix(prefix: string): string[] {
  try {
    return Object.keys(localStorage).filter((k) => k.startsWith(prefix))
  } catch {
    return []
  }
}

/** A plain object (not null, not an array), for `parse` callbacks. */
export function isRecord(raw: unknown): raw is Record<string, unknown> {
  return !!raw && typeof raw === 'object' && !Array.isArray(raw)
}
