import { ref } from 'vue'
import { isRecord, KEYS, keysWithPrefix, readJson, writeJson, writeText } from './lib/storage'
import { store } from './store'

/**
 * Favourite skins: one skin or chroma per champion, chosen in the Skins sheet and kept on this phone only
 * (localStorage), per paired PC (the pairing id: a new pairing starts empty, and its first favourite drops the earlier
 * pairing's). After lock-in the Loadout offers it as a one-tap suggestion ("Equip Favourite: Star Guardian Ahri"); it
 * is only ever sent when the user taps that, never by itself.
 */

/** Bumped on every write, so readers recompute (localStorage itself isn't reactive). */
const version = ref(0)

function key(): string | null {
  const pairingId = store.grant?.pairingId
  return pairingId ? KEYS.favouriteSkinsPrefix + pairingId : null
}

function parse(raw: unknown): Record<string, number> | null {
  if (!isRecord(raw)) return null
  const out: Record<string, number> = {}
  for (const [champion, id] of Object.entries(raw))
    if (typeof id === 'number' && Number.isInteger(id) && id > 0) out[champion] = id
  return out
}

/** championId → skin or chroma id; unreadable or malformed storage reads as none. */
function read(): Record<string, number> {
  void version.value
  const k = key()
  return (k && readJson(k, parse)) || {}
}

/** The favourite skin or chroma id for a champion, or null. Reactive (re-read after every change). */
export function favouriteSkinId(championId: number): number | null {
  return championId > 0 ? (read()[String(championId)] ?? null) : null
}

/** Sets (or, with null, clears) a champion's favourite. Without a pairing or storage it does nothing. */
export function setFavouriteSkin(championId: number, skinId: number | null): void {
  const k = key()
  if (!k || championId <= 0) return
  const all = read()
  if (skinId && skinId > 0) all[String(championId)] = skinId
  else delete all[String(championId)]
  // Storage full or blocked: nothing remembered.
  if (writeJson(k, all)) {
    // Only the current pairing's favourites can ever be offered: drop those of earlier pairings (unpaired, re-paired).
    for (const other of keysWithPrefix(KEYS.favouriteSkinsPrefix)) if (other !== k) writeText(other, null)
  }
  version.value++
}
