import { computed, ref, watch } from 'vue'
import { now } from './clock'
import {
  AutoPickRoles,
  isAutoPickRoleAllowed,
  resolveAutoPickPreset,
  type AutoPickLogEntry,
  type AutoPickPending,
  type AutoPickPreset,
  type AutoPickQueueGroup,
  type AutoPickRole,
  type AutoPickRun,
  type AutoPickSettings,
  type AutoPickState,
  type RunePage,
} from './contracts'
import { championName, spellById } from './ddragon'
import { positionLabel } from './labels'
import { sendCommand } from './hub'
import { liveAutoPick, liveState, store, toast } from './store'

/**
 * Automatic pick, ban and default summoner spells. The connector runs it, so it keeps working while the phone sleeps;
 * the phone edits the presets (saved on the PC), switches it on and off, pauses it for one game and cancels a pending
 * auto lock-in. Everything reads `liveAutoPick`.
 */

/** The slice has arrived (not offline, signed in): the settings can be shown. */
export const autoPickAvailable = computed(() => !!liveAutoPick.value)

export const autoPickSettings = computed<AutoPickSettings | null>(() => liveAutoPick.value?.settings ?? null)

/** The automation in the current champion select; null outside one. */
export const autoPickRun = computed<AutoPickRun | null>(() => liveAutoPick.value?.run ?? null)

/** Champion Select has something to say about it: on, paused, or no preset for your role. */
export const autoPickShown = computed(() => ['active', 'paused', 'no-preset'].includes(autoPickRun.value?.status ?? ''))

/** The user's rune pages, known only while the client runs (ClientState.runePages). */
export const runePages = computed<RunePage[]>(() => liveState.value?.runePages ?? [])

// --- Labels -------------------------------------------------------------------------------------------------

/** Section titles and what each group covers (the queues of AutoPickQueueIds, by their client names). */
export const GROUP_INFO: Record<AutoPickQueueGroup, { title: string; queues: string }> = {
  draft: { title: 'Draft Pick', queues: 'Draft Pick, Ranked Solo/Duo, Ranked Flex, Clash and Draft custom games.' },
  blind: { title: 'Blind Pick', queues: 'Blind Pick, Swiftplay, Quickplay, Co-op vs. AI and Blind custom games. No bans.' },
  aram: { title: 'ARAM', queues: 'ARAM, ARAM Clash and ARAM: Mayhem. Spells and rune page only.' },
}

/** Ban lists only where the queue has bans (draft); ARAM has neither list. */
export const hasBans = (g: AutoPickQueueGroup) => g === 'draft'
export const hasPicks = (g: AutoPickQueueGroup) => g !== 'aram'

/** The roles a group has presets for, Any first. */
export function rolesFor(group: AutoPickQueueGroup): AutoPickRole[] {
  return AutoPickRoles.filter((r) => isAutoPickRoleAllowed(group, r))
}

/** "Any Role", "Top", "Mid", … — ARAM's single preset reads "All Games". */
export function roleTitle(role: AutoPickRole | '', group?: AutoPickQueueGroup | ''): string {
  if (role === 'ANY') return group === 'aram' ? 'All Games' : 'Any Role'
  return positionLabel(role) || 'Any Role'
}

/** "Draft Pick · Mid", "ARAM". */
export function presetTitle(group: AutoPickQueueGroup, role: AutoPickRole): string {
  return group === 'aram' ? GROUP_INFO.aram.title : `${GROUP_INFO[group].title} · ${roleTitle(role, group)}`
}

/** At most this many champion names in a summary line; the rest is "+2". */
const SUMMARY_NAMES = 3

/** "Ahri, Syndra, Orianna +2". */
export function championList(ids: number[]): string {
  const names = ids.slice(0, SUMMARY_NAMES).map(championName)
  const more = ids.length - SUMMARY_NAMES
  return more > 0 ? `${names.join(', ')} +${more}` : names.join(', ')
}

/** "Flash + Ignite"; '' when unset. */
export function spellPair(spell1Id: number, spell2Id: number): string {
  if (!(spell1Id > 0 && spell2Id > 0)) return ''
  const name = (id: number) => spellById(id)?.name ?? `Spell ${id}`
  return `${name(spell1Id)} + ${name(spell2Id)}`
}

/**
 * A rune page by id: its name while the client runs, else the name the PC saved with the preset (`stored`), else
 * "Saved Page" (saved while League was closed).
 */
export function runePageName(id: number, stored?: string): string {
  if (!(id > 0)) return ''
  return runePages.value.find((p) => p.id === id)?.name || stored || 'Saved Page'
}

export function findPreset(
  settings: AutoPickSettings | null,
  group: AutoPickQueueGroup,
  role: AutoPickRole,
): AutoPickPreset | null {
  return settings?.presets.find((p) => p.queueGroup === group && p.role === role) ?? null
}

/** ["Picks: Ahri, Syndra", "Bans: Zed, Yasuo"]: what is set of the two lists, picks first (also Champion Select's status). */
export function presetChoiceLines(p: AutoPickPreset): string[] {
  const lines: string[] = []
  if (hasPicks(p.queueGroup) && p.pickChampionIds.length) lines.push(`Picks: ${championList(p.pickChampionIds)}`)
  if (hasBans(p.queueGroup) && p.banChampionIds.length) lines.push(`Bans: ${championList(p.banChampionIds)}`)
  return lines
}

/**
 * The settings row's subtitle for one preset, a line per fact: picks, bans, spells, rune page; or "Not Set". A role
 * gets its row once it has a preset of its own. What a role leaves empty falls back to Any Role field by field; the editor's footers say so per field.
 */
export function roleSummaryLines(settings: AutoPickSettings, group: AutoPickQueueGroup, role: AutoPickRole): string[] {
  const own = findPreset(settings, group, role)
  if (!own) return ['Not Set']
  const lines = presetChoiceLines(own)
  const spells = spellPair(own.spell1Id, own.spell2Id)
  if (spells) lines.push(`Spells: ${spells}`)
  if (own.runePageId > 0) lines.push(`Runes: ${runePageName(own.runePageId, own.runePageName)}`)
  return lines.length ? lines : ['Not Set']
}

/** The preset that applies to the current champion select (role fallback included); null when none. */
export const runPreset = computed<AutoPickPreset | null>(() => {
  const run = autoPickRun.value
  const settings = autoPickSettings.value
  if (!run || !settings || !run.queueGroup || !run.role) return null
  return resolveAutoPickPreset(settings, run.queueGroup, run.role)
})

// --- Pending auto action ------------------------------------------------------------------------------------

/**
 * Where the pending action's countdown started: the receive time of the update that carried it. `completesAt` and
 * `updatedAt` are both the connector's clock, so their difference is the time left when the update was sent; the
 * phone's own clock never enters. A republished copy of the same update (the replay on sign-in) keeps the first anchor,
 * so the countdown doesn't jump back up.
 */
let pendingAnchor: { key: string; leftMs: number; at: number } | null = null

/** Seconds until the pending auto ban / lock-in completes (0 at the end); null without one. Reactive through `now`. */
export function pendingSecondsLeft(): number | null {
  const s: AutoPickState | null = liveAutoPick.value
  const p = s?.run?.pending
  if (!s || !p) return null
  const key = `${p.actionId}:${p.completesAt}:${s.updatedAt}`
  if (pendingAnchor?.key !== key) {
    const leftMs = Date.parse(p.completesAt) - Date.parse(s.updatedAt)
    pendingAnchor = { key, leftMs: Number.isFinite(leftMs) ? leftMs : 0, at: store.receivedAt.autoPick }
  }
  return Math.max(0, (pendingAnchor.leftMs - (now.value - pendingAnchor.at)) / 1000)
}

export const pendingAction = computed<AutoPickPending | null>(() => autoPickRun.value?.pending ?? null)

// --- Log ----------------------------------------------------------------------------------------------------

/** Log kinds worth a banner when they happen (the rest only shows in the log rows). */
const TOASTED: Partial<Record<AutoPickLogEntry['kind'], 'success' | 'warning' | 'error'>> = {
  banned: 'success',
  locked: 'success',
  spells: 'success',
  'rune-page': 'success',
  skipped: 'warning',
  failed: 'error',
}

export function logKey(e: AutoPickLogEntry): string {
  return `${e.at}|${e.kind}|${e.championId}|${e.text}`
}

/**
 * Toasts each new log line while `active()` (Champion Select is on screen), once per run. Entries already there when it
 * starts (or when the run first shows up, e.g. opening the app mid-draft) are only marked seen. A new run (`run.id`: the
 * next champ select, also after a dodge while the screen stayed up) starts a fresh seen set, so the earlier run's
 * entries can't collide with or hide the new ones. The connector writes the text: short English, no player names.
 */
export function useAutoPickToasts(active: () => boolean) {
  const seen = new Set<string>()
  let primed = false
  let runId: number | null | undefined = null
  watch(
    () => (active() ? autoPickRun.value : null),
    (run) => {
      if (!run) {
        primed = false
        seen.clear()
        runId = null
        return
      }
      if (run.id !== runId) {
        seen.clear()
        runId = run.id
      }
      for (const e of run.log) {
        const key = logKey(e)
        if (seen.has(key)) continue
        seen.add(key)
        const kind = TOASTED[e.kind]
        if (primed && kind && e.text) toast(e.text, kind)
      }
      primed = true
    },
    { immediate: true },
  )
}

// --- Commands -----------------------------------------------------------------------------------------------

/** One auto pick command; true when the connector confirmed it (sendCommand toasts a refusal or no answer). */
async function send(cmd: Parameters<typeof sendCommand>[0]): Promise<boolean> {
  return !!(await sendCommand(cmd))?.success
}

/**
 * Optimistic master switch while 'SetAutoPickEnabled' is on its way. While the command is in flight only its echo
 * clears it (an update sent before the PC applied it would flip the switch back for a moment); once the command has
 * resolved, the next AutoPickState wins whatever it says (another phone, a resync), and a failure clears it at once.
 */
const pendingEnabled = ref<boolean | null>(null)
/** Counts toggles, so an older command's result can't clear a newer one's value. */
let enableSeq = 0
let enableInFlight = false
watch(autoPickSettings, (settings) => {
  if (pendingEnabled.value === null) return
  if (!settings || !enableInFlight || settings.enabled === pendingEnabled.value) pendingEnabled.value = null
})

/** The master switch as shown: the optimistic value while a change is in flight. */
export const autoPickEnabled = computed(() => pendingEnabled.value ?? autoPickSettings.value?.enabled ?? false)

export async function setAutoPickEnabled(enabled: boolean): Promise<boolean> {
  const seq = ++enableSeq
  pendingEnabled.value = enabled
  enableInFlight = true
  const ok = await send({ type: 'SetAutoPickEnabled', enabled })
  if (seq !== enableSeq) return ok
  enableInFlight = false
  // Failed, or the echo already came: show the state. Otherwise keep the value until the next update arrives.
  if (!ok || autoPickSettings.value?.enabled === enabled) pendingEnabled.value = null
  return ok
}

/** Optimistic Auto Accept switch while 'SetAutoAcceptEnabled' is on its way; same rules as `pendingEnabled`. */
const pendingAutoAccept = ref<boolean | null>(null)
/** Counts toggles, so an older command's result can't clear a newer one's value. */
let autoAcceptSeq = 0
let autoAcceptInFlight = false
watch(autoPickSettings, (settings) => {
  if (pendingAutoAccept.value === null) return
  if (!settings || !autoAcceptInFlight || settings.autoAccept === pendingAutoAccept.value) pendingAutoAccept.value = null
})

/** Auto Accept as shown: the optimistic value while a change is in flight. Independent of the auto pick switch. */
export const autoAcceptEnabled = computed(() => pendingAutoAccept.value ?? autoPickSettings.value?.autoAccept ?? false)

export async function setAutoAcceptEnabled(enabled: boolean): Promise<boolean> {
  const seq = ++autoAcceptSeq
  pendingAutoAccept.value = enabled
  autoAcceptInFlight = true
  const ok = await send({ type: 'SetAutoAcceptEnabled', enabled })
  if (seq !== autoAcceptSeq) return ok
  autoAcceptInFlight = false
  // Failed, or the echo already came: show the state. Otherwise keep the value until the next update arrives.
  if (!ok || autoPickSettings.value?.autoAccept === enabled) pendingAutoAccept.value = null
  return ok
}

/** Saves one preset ('SetAutoPickPreset'); a preset with nothing set removes it on the PC. */
export function saveAutoPickPreset(p: AutoPickPreset): Promise<boolean> {
  const spells = p.spell1Id > 0 && p.spell2Id > 0
  return send({
    type: 'SetAutoPickPreset',
    queueGroup: p.queueGroup,
    role: p.role,
    banChampionIds: p.banChampionIds,
    pickChampionIds: p.pickChampionIds,
    spell1Id: spells ? p.spell1Id : null,
    spell2Id: spells ? p.spell2Id : null,
    runePageId: p.runePageId > 0 ? p.runePageId : null,
  })
}

export function removeAutoPickPreset(group: AutoPickQueueGroup, role: AutoPickRole): Promise<boolean> {
  return saveAutoPickPreset(emptyPreset(group, role))
}

export const pauseAutoPick = () => send({ type: 'PauseAutoPick' })
export const resumeAutoPick = () => send({ type: 'ResumeAutoPick' })
export const cancelAutoPickAction = (actionId: number) => send({ type: 'CancelAutoPickAction', actionId })

export function emptyPreset(group: AutoPickQueueGroup, role: AutoPickRole): AutoPickPreset {
  return {
    queueGroup: group,
    role,
    banChampionIds: [],
    pickChampionIds: [],
    spell1Id: 0,
    spell2Id: 0,
    runePageId: 0,
    runePageName: '',
  }
}

export function samePreset(a: AutoPickPreset, b: AutoPickPreset): boolean {
  const same = (x: number[], y: number[]) => x.length === y.length && x.every((v, i) => v === y[i])
  return (
    same(a.banChampionIds, b.banChampionIds) &&
    same(a.pickChampionIds, b.pickChampionIds) &&
    a.spell1Id === b.spell1Id &&
    a.spell2Id === b.spell2Id &&
    a.runePageId === b.runePageId
  )
}
