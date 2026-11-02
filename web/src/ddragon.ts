import { reactive } from 'vue'
import { isRecord, KEYS, readJson, writeJson } from './lib/storage'

const BASE = 'https://ddragon.leagueoflegends.com'
const MAX_AGE_MS = 12 * 60 * 60 * 1000

export interface ChampionInfo {
  key: number
  id: string
  name: string
}

export interface SpellInfo {
  key: number
  id: string
  name: string
  /** Game modes that offer it ("CLASSIC", "ARAM", …). */
  modes: string[]
}

interface Cached {
  version: string
  fetchedAt: number
  champions: ChampionInfo[]
  spells: SpellInfo[]
}

export const ddragon = reactive({
  version: '',
  loading: false,
  error: '',
  champions: new Map<number, ChampionInfo>(),
  /** Summoner's Rift (CLASSIC) spells, sorted by name. */
  spells: [] as SpellInfo[],
  /** Every summoner spell Data Dragon lists (also ARAM's Mark and Clarity), sorted by name. */
  allSpells: [] as SpellInfo[],
})

function apply(c: Cached) {
  ddragon.version = c.version
  ddragon.champions = new Map(c.champions.map((x) => [x.key, x]))
  ddragon.allSpells = c.spells
  ddragon.spells = spellsForMode('CLASSIC')
}

/** The spells a game mode offers ("CLASSIC" = Summoner's Rift, "ARAM"), sorted by name. */
export function spellsForMode(mode: string): SpellInfo[] {
  return ddragon.allSpells.filter((s) => s.modes.includes(mode))
}

/** Our own cache, written by this file: only its shape is checked. */
function readCache(): Cached | null {
  return readJson(KEYS.ddragon, (raw) =>
    isRecord(raw) && typeof raw.version === 'string' && Array.isArray(raw.champions) && Array.isArray(raw.spells)
      ? (raw as unknown as Cached)
      : null,
  )
}

function writeCache(c: Cached) {
  writeJson(KEYS.ddragon, c) // quota / private mode: memory only
}

/** A whole request (download included); a stalled one would leave "Loading champions…" spinning for good. */
const FETCH_TIMEOUT_MS = 20_000

async function getJson<T>(url: string): Promise<T> {
  const r = await fetch(url, { signal: AbortSignal.timeout(FETCH_TIMEOUT_MS) })
  if (!r.ok) throw new Error(`${r.status} ${url}`)
  return (await r.json()) as T
}

interface DdChampionFile {
  data: Record<string, { id: string; key: string; name: string; tags?: string[] }>
}
interface DdSummonerFile {
  data: Record<string, { id: string; key: string; name: string; modes: string[] }>
}

async function fetchAll(version: string): Promise<Cached> {
  const [champs, summ] = await Promise.all([
    getJson<DdChampionFile>(`${BASE}/cdn/${version}/data/en_US/champion.json`),
    getJson<DdSummonerFile>(`${BASE}/cdn/${version}/data/en_US/summoner.json`),
  ])
  const champions = Object.values(champs.data).map((c) => ({
    key: Number(c.key),
    id: c.id,
    name: c.name,
  }))
  const spells = Object.values(summ.data)
    .map((s) => ({ key: Number(s.key), id: s.id, name: s.name, modes: s.modes }))
    .sort((a, b) => a.name.localeCompare(b.name))
  return { version, fetchedAt: Date.now(), champions, spells }
}

let loadPromise: Promise<void> | null = null
let retryTimer: ReturnType<typeof setTimeout> | undefined

function scheduleRetry() {
  clearTimeout(retryTimer)
  const retry = () => {
    clearTimeout(retryTimer)
    window.removeEventListener('online', retry)
    void loadDdragon()
  }
  retryTimer = setTimeout(retry, 30_000)
  window.addEventListener('online', retry, { once: true })
}

/** Load champion + summoner spell data (cached in localStorage, revalidated every 12 h). */
export function loadDdragon(): Promise<void> {
  loadPromise ??= (async () => {
    let cached = readCache()
    if (cached) {
      try {
        apply(cached)
      } catch {
        cached = null // corrupt or old-shaped cache: fetch fresh
      }
    }
    if (cached && Date.now() - cached.fetchedAt < MAX_AGE_MS) return
    ddragon.loading = true
    try {
      const versions = await getJson<string[]>(`${BASE}/api/versions.json`)
      const latest = versions[0]
      if (!latest) throw new Error('No Data Dragon version')
      if (cached && cached.version === latest) {
        writeCache({ ...cached, fetchedAt: Date.now() })
      } else {
        const fresh = await fetchAll(latest)
        writeCache(fresh)
        apply(fresh)
      }
      ddragon.error = ''
    } catch (e) {
      if (!cached) ddragon.error = e instanceof Error ? e.message : String(e)
      loadPromise = null
      // Nothing to show yet (first start offline): try again when the network is back, or in 30 s.
      if (!cached) scheduleRetry()
    } finally {
      ddragon.loading = false
    }
  })()
  return loadPromise
}

export function championIconUrl(championId: number): string | null {
  const c = ddragon.champions.get(championId)
  return c && ddragon.version ? `${BASE}/cdn/${ddragon.version}/img/champion/${c.id}.png` : null
}

/**
 * A skin's art (`loading`: the portrait card art, 308×560; `splash`: the wide splash). Data Dragon keys it by the
 * champion's id and the skin's number (skin id % 1000); the art isn't versioned. Chromas have no art of their own: pass
 * the parent skin's id. Null until the champion is known.
 */
export function skinArtUrl(championId: number, skinId: number, kind: 'loading' | 'splash' = 'loading'): string | null {
  const c = ddragon.champions.get(championId)
  return c && skinId > 0 ? `${BASE}/cdn/img/champion/${kind}/${c.id}_${skinId % 1000}.jpg` : null
}

export function championName(championId: number): string {
  if (!championId) return ''
  return ddragon.champions.get(championId)?.name ?? `#${championId}`
}

export function spellById(key: number): SpellInfo | undefined {
  return ddragon.allSpells.find((s) => s.key === key)
}

/** A summoner spell by its Data Dragon id ("SummonerFlash"), as the in-game API names it; 0 when unknown. */
export function spellKeyById(id: string): number {
  return (id && ddragon.allSpells.find((s) => s.id === id)?.key) || 0
}

export function spellIconUrl(key: number): string | null {
  const s = spellById(key)
  return s && ddragon.version ? `${BASE}/cdn/${ddragon.version}/img/spell/${s.id}.png` : null
}

/** The player's profile icon (Data Dragon has every icon the client can show); null until the version is known. */
export function profileIconUrl(iconId: number): string | null {
  return iconId > 0 && ddragon.version ? `${BASE}/cdn/${ddragon.version}/img/profileicon/${iconId}.png` : null
}

/** Item art; null for an empty slot (0) or until the version is known. */
export function itemIconUrl(itemId: number): string | null {
  return itemId > 0 && ddragon.version ? `${BASE}/cdn/${ddragon.version}/img/item/${itemId}.png` : null
}

// --- Extras: item names, prices and runes (Profile tab, In Game scoreboard), loaded on first use -------------

export interface RuneInfo {
  id: number
  name: string
  /** Path under /cdn/img/ ("perk-images/Styles/…png"). */
  icon: string
}

interface CachedExtras {
  version: string
  /** Item id, name, total price in gold. */
  items: [number, string, number][]
  runes: RuneInfo[]
}

/** Item names and prices, runes (keystones and rune trees) by id; empty until loadDdragonExtras() finished. */
export const ddragonExtras = reactive({
  version: '',
  items: new Map<number, string>(),
  /** Total price (gold.total) by item id. */
  itemGold: new Map<number, number>(),
  runes: new Map<number, RuneInfo>(),
})

function applyExtras(c: CachedExtras) {
  ddragonExtras.version = c.version
  ddragonExtras.items = new Map(c.items.map(([id, name]) => [id, name]))
  ddragonExtras.itemGold = new Map(c.items.map(([id, , gold]) => [id, gold]))
  ddragonExtras.runes = new Map(c.runes.map((r) => [r.id, r]))
}

interface DdItemFile {
  data: Record<string, { name: string; gold?: { total?: number } }>
}
interface DdRuneStyle {
  id: number
  name: string
  icon: string
  slots: { runes: { id: number; name: string; icon: string }[] }[]
}

let extrasPromise: Promise<void> | null = null

/**
 * Item names (for VoiceOver), item prices (the In Game item-value estimate) and rune art: item.json and runesReforged.json of the current Data
 * Dragon version, cached in localStorage until the version changes. Failures leave the maps empty (icons without
 * names, no runes) and are retried on the next call.
 */
export function loadDdragonExtras(): Promise<void> {
  extrasPromise ??= (async () => {
    await loadDdragon()
    const version = ddragon.version
    if (!version) throw new Error('No Data Dragon version')
    if (ddragonExtras.version === version) return
    const cached = readJson(KEYS.ddragonExtras, (raw) =>
      isRecord(raw) && raw.version === version && Array.isArray(raw.items) && Array.isArray(raw.runes)
        ? (raw as unknown as CachedExtras)
        : null,
    )
    if (cached) return applyExtras(cached)
    const [items, styles] = await Promise.all([
      getJson<DdItemFile>(`${BASE}/cdn/${version}/data/en_US/item.json`),
      getJson<DdRuneStyle[]>(`${BASE}/cdn/${version}/data/en_US/runesReforged.json`),
    ])
    const runes: RuneInfo[] = []
    for (const s of styles) {
      runes.push({ id: s.id, name: s.name, icon: s.icon })
      for (const slot of s.slots) for (const r of slot.runes) runes.push({ id: r.id, name: r.name, icon: r.icon })
    }
    const fresh: CachedExtras = {
      version,
      items: Object.entries(items.data).map(([id, i]) => [Number(id), i.name, i.gold?.total ?? 0]),
      runes,
    }
    applyExtras(fresh)
    writeJson(KEYS.ddragonExtras, fresh) // quota / private mode: memory only
  })().catch(() => {
    extrasPromise = null
  })
  return extrasPromise
}

/** The item's name; '' when unknown (extras not loaded). */
export function itemName(itemId: number): string {
  return ddragonExtras.items.get(itemId) ?? ''
}

/** What the item costs in total (its recipe included); null when unknown (extras not loaded, or an unlisted item). */
export function itemGold(itemId: number): number | null {
  return ddragonExtras.itemGold.get(itemId) ?? null
}

export function runeById(id: number): RuneInfo | undefined {
  return id > 0 ? ddragonExtras.runes.get(id) : undefined
}

/** Keystone or rune tree art; null when unknown. */
export function runeIconUrl(id: number): string | null {
  const r = runeById(id)
  return r ? `${BASE}/cdn/img/${r.icon}` : null
}
