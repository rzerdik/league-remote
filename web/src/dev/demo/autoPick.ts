// Every scenario has an AutoPickState, switched off and outside champion select.
// ?demo=autopick: Settings › Auto Pick & Ban with a few presets, switched on; `&edit=draft.MIDDLE` (group.role) opens
// that preset's editor. `&auto=1` on a champion select scenario (`pick`, `ban`) switches it on for draft Mid (Ranked Flex):
// a moment after the demo opens the PC hovers your first choice and the banner counts down the grace period, then it
// locks in (or bans, and the draft walks on to your pick, which it locks in too). Cancel, Pause and Resume answer like
// the connector; hovering another champion meanwhile leaves the action to you. `&auto=hold` keeps the countdown going
// (it restarts instead of locking in) to review the banner. `&auto=paused` / `no-preset` show those status lines.
import { emptyPreset } from '../../autoPick'
import {
  AutoPickLockDelayMs,
  AutoPickMaxLogEntries,
  AutoPickQueueGroups,
  AutoPickRoles,
  isAutoPickQueueGroup,
  isAutoPickRoleAllowed,
  type AutoPickActionKind,
  type AutoPickLogEntry,
  type AutoPickPreset,
  type AutoPickQueueGroup,
  type AutoPickRole,
  type AutoPickRun,
  type AutoPickState,
  type CommandResult,
  type RemoteCommand,
} from '../../contracts'
import { nav } from '../../navigation'
import { store } from '../../store'
import { completeMyAction } from './champSelect'
import { held, params, push, scenario, wait } from './core'

const autoParam = params.get('auto')
const autoHold = autoParam === 'hold'
const autoOn = autoParam === '1' || autoHold || autoParam === 'paused' || autoParam === 'no-preset'
/** How long after the draft reaches your turn the fake connector hovers (the real one: on the next session update). */
const AUTO_ARM_MS = 1200
/** The connector's grace period (AutoPickState.AutoLockDelay) and how long its lock-in request then takes. */
const AUTO_LOCK_DELAY_MS = AutoPickLockDelayMs
const AUTO_LOCK_REQUEST_MS = 500

function preset(queueGroup: AutoPickQueueGroup, role: AutoPickRole, p: Partial<AutoPickPreset>): AutoPickPreset {
  return { ...emptyPreset(queueGroup, role), ...p }
}

/** Zed and Yasuo banned in every draft, Flash + Ignite by default; Mid, Bot and Support picks; Blind and ARAM defaults. */
function demoPresets(): AutoPickPreset[] {
  return [
    preset('draft', 'ANY', { banChampionIds: [238, 157], spell1Id: 4, spell2Id: 14 }),
    preset('draft', 'MIDDLE', { pickChampionIds: [103, 134, 61, 517], runePageId: 1, runePageName: 'Electrocute Mid' }),
    preset('draft', 'BOTTOM', { pickChampionIds: [222, 51], spell1Id: 4, spell2Id: 7 }),
    preset('draft', 'UTILITY', { banChampionIds: [555], pickChampionIds: [412, 89], spell1Id: 4, spell2Id: 3 }),
    preset('blind', 'ANY', { pickChampionIds: [103, 99], spell1Id: 4, spell2Id: 14 }),
    preset('aram', 'ANY', { spell1Id: 4, spell2Id: 32 }),
  ]
}

let autoLogAt = 0
function logLine(kind: AutoPickLogEntry['kind'], championId: number, text: string): AutoPickLogEntry {
  // Distinct times even within one millisecond (the phone keys log lines by them).
  autoLogAt = Math.max(autoLogAt + 1, Date.now())
  return { at: new Date(autoLogAt).toISOString(), kind, championId, text }
}

function demoRun(): AutoPickRun | null {
  const cs = held.state?.champSelect
  if (!autoOn || !cs) return null
  const status = autoParam === 'paused' ? 'paused' : autoParam === 'no-preset' ? 'no-preset' : 'active'
  const log = status === 'no-preset' ? [] : [logLine('spells', 0, 'Set Flash and Ignite')]
  if (status !== 'no-preset' && cs.myAction?.type !== 'ban') log.push(logLine('banned', 238, 'Auto-banned Zed'))
  return { queueGroup: 'draft', role: 'MIDDLE', status, pending: null, log, id: Date.now() }
}

/** The scenario's auto pick state (call once the client state is held: the run follows champion select). */
export function autoPickState(): AutoPickState {
  return {
    settings: { enabled: scenario === 'autopick' || autoOn, presets: demoPresets() },
    pausedForThisGame: autoParam === 'paused',
    run: demoRun(),
    updatedAt: new Date().toISOString(),
  }
}

/** A connector publish: stamped with the connector's clock, received now. */
export function pushAutoPick(next: AutoPickState) {
  held.autoPick = { ...next, updatedAt: new Date().toISOString() }
  store.autoPick = held.autoPick
  store.receivedAt.autoPick = performance.now()
}

function updateRun(change: (run: AutoPickRun) => Partial<AutoPickRun>) {
  const s = held.autoPick
  if (!s?.run) return
  const run = { ...s.run, ...change(s.run) }
  pushAutoPick({ ...s, run: { ...run, log: run.log.slice(-AutoPickMaxLogEntries) } })
}

let autoTimer: ReturnType<typeof setTimeout> | undefined

/**
 * The connector's step on your turn: hover the first valid choice of the Mid preset (Zed to ban, Ahri to pick), then
 * lock it in AUTO_LOCK_DELAY_MS later unless it was cancelled, paused or you chose yourself.
 */
export function autoPickArm(after = AUTO_ARM_MS) {
  clearTimeout(autoTimer)
  autoTimer = setTimeout(() => {
    const cs = store.state?.champSelect
    const a = cs?.myAction
    const s = held.autoPick
    if (!cs || !a || !a.isInProgress || a.completed || s?.run?.status !== 'active' || s.run.pending) return
    const kind: AutoPickActionKind = a.type === 'ban' ? 'ban' : 'pick'
    const championId = kind === 'ban' ? 238 : 103
    a.championId = championId
    push({ ...store.state!, champSelect: { ...cs } })
    const completesAt = new Date(Date.now() + AUTO_LOCK_DELAY_MS).toISOString()
    updateRun(() => ({ pending: { kind, actionId: a.id, championId, completesAt, cancellable: true } }))
    if (autoHold) {
      // Review mode: a fresh grace period every time this one runs out (the banner never goes away by itself).
      const again = () => {
        autoTimer = setTimeout(() => {
          const at = new Date(Date.now() + AUTO_LOCK_DELAY_MS).toISOString()
          updateRun((r) => ({ pending: r.pending && { ...r.pending, completesAt: at } }))
          again()
        }, AUTO_LOCK_DELAY_MS)
      }
      again()
      return
    }
    autoTimer = setTimeout(() => {
      updateRun((r) => ({ pending: r.pending && { ...r.pending, cancellable: false } }))
      autoTimer = setTimeout(() => {
        const text = kind === 'ban' ? 'Auto-banned Zed' : 'Auto-locked Ahri'
        updateRun((r) => ({ pending: null, log: [...r.log, logLine(kind === 'ban' ? 'banned' : 'locked', championId, text)] }))
        completeMyAction(championId, autoPickArm)
      }, AUTO_LOCK_REQUEST_MS)
    }, AUTO_LOCK_DELAY_MS)
  }, after)
}

/** You hovered another champion during the grace period: your choice wins, the action is left to you. */
export function autoPickUserHovered(championId: number) {
  const p = held.autoPick?.run?.pending
  if (!p || !p.cancellable || p.championId === championId) return
  clearTimeout(autoTimer)
  updateRun((r) => ({ pending: null, log: [...r.log, logLine('manual', championId, 'You chose yourself, so it was left to you')] }))
}

const byKey = (a: AutoPickPreset, b: AutoPickPreset) =>
  AutoPickQueueGroups.indexOf(a.queueGroup) - AutoPickQueueGroups.indexOf(b.queueGroup) ||
  AutoPickRoles.indexOf(a.role) - AutoPickRoles.indexOf(b.role)

/** The five auto pick commands, answered like the connector (they work without a League client). */
export async function demoAutoPick(cmd: RemoteCommand): Promise<CommandResult | null> {
  await wait(350)
  const ok = { requestId: cmd.requestId, success: true }
  const fail = (error: string) => ({ requestId: cmd.requestId, success: false, error })
  const s = held.autoPick
  if (!s) return fail('Unknown command.')
  switch (cmd.type) {
    case 'SetAutoPickEnabled': {
      const enabled = !!cmd.enabled
      if (!enabled) clearTimeout(autoTimer)
      const status = enabled ? (s.pausedForThisGame ? 'paused' : 'active') : 'disabled'
      const run: AutoPickRun | null = s.run && { ...s.run, pending: enabled ? s.run.pending : null, status }
      pushAutoPick({ ...s, settings: { ...s.settings, enabled }, run })
      if (run?.status === 'active') autoPickArm()
      return ok
    }
    case 'SetAutoPickPreset': {
      const next = preset(cmd.queueGroup!, cmd.role!, {
        banChampionIds: cmd.banChampionIds ?? [],
        pickChampionIds: cmd.pickChampionIds ?? [],
        spell1Id: cmd.spell1Id ?? 0,
        spell2Id: cmd.spell2Id ?? 0,
        runePageId: cmd.runePageId ?? 0,
        // Like the connector: the client's name for the page, else one already saved for it.
        runePageName:
          store.state?.runePages.find((p) => p.id === cmd.runePageId)?.name ??
          s.settings.presets.find((p) => p.runePageId === cmd.runePageId && p.runePageName)?.runePageName ??
          '',
      })
      const empty = !next.banChampionIds.length && !next.pickChampionIds.length && !next.spell1Id && !next.runePageId
      const rest = s.settings.presets.filter((p) => p.queueGroup !== next.queueGroup || p.role !== next.role)
      pushAutoPick({ ...s, settings: { ...s.settings, presets: (empty ? rest : [...rest, next]).sort(byKey) } })
      return ok
    }
    case 'PauseAutoPick': {
      clearTimeout(autoTimer)
      const run = s.run
      const p = run?.pending
      pushAutoPick({
        ...s,
        pausedForThisGame: true,
        run: run && {
          ...run,
          status: run.status === 'active' ? 'paused' : run.status,
          pending: null,
          log: p ? [...run.log, logLine('cancelled', p.championId, 'Paused; your hover stays')] : run.log,
        },
      })
      return ok
    }
    case 'ResumeAutoPick': {
      const run = s.run && { ...s.run, status: s.run.status === 'paused' ? ('active' as const) : s.run.status }
      pushAutoPick({ ...s, pausedForThisGame: false, run })
      autoPickArm()
      return ok
    }
    case 'CancelAutoPickAction': {
      const p = s.run?.pending
      if (!p) return fail('Nothing to cancel.')
      if (!p.cancellable) return fail('That auto pick is already done.')
      clearTimeout(autoTimer)
      updateRun((r) => ({ pending: null, log: [...r.log, logLine('cancelled', p.championId, 'Cancelled; your hover stays')] }))
      return ok
    }
    default:
      return null
  }
}

/** ?demo=autopick: Settings › Auto Pick & Ban; `&edit=draft.MIDDLE` opens that preset. */
export function openAutoPickDemo() {
  nav.tab = 'settings'
  nav.settingsView = 'autopick'
  const [group, role] = (params.get('edit') ?? '').split('.')
  if (isAutoPickQueueGroup(group) && isAutoPickRoleAllowed(group, role)) nav.autoPickEdit = { group, role }
}

/** `&auto=1` (or `hold`) in champion select: the PC hovers your first choice a moment after the demo opens. */
export function armAutoPickOnOpen() {
  if ((autoParam === '1' || autoHold) && held.state?.champSelect) autoPickArm()
}
