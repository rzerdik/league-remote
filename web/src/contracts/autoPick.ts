// Mirror of src/LeagueRemote.Contracts/AutoPick.cs.

/** AutoPickSettings.MaxBans / MaxPicks */
export const AutoPickMaxBans = 5
export const AutoPickMaxPicks = 10
/** AutoPickState.MaxLogEntries */
export const AutoPickMaxLogEntries = 10
/** AutoPickState.AutoLockDelay: grace period between the auto-hover and the lock-in / ban. */
export const AutoPickLockDelayMs = 3_000

/**
 * AutoPickQueueGroups: queues sharing presets, by queue id (the Summoner's Rift Blind / Draft / Tournament Draft custom
 * queues included). Anything else (Arena, Practice Tool and the other customs, TFT, rotating modes) is not automated.
 */
export type AutoPickQueueGroup = 'draft' | 'blind' | 'aram'
export const AutoPickQueueGroups: readonly AutoPickQueueGroup[] = ['draft', 'blind', 'aram']
export const AutoPickQueueIds: Readonly<Record<AutoPickQueueGroup, readonly number[]>> = {
  /** Draft Pick, Ranked Solo/Duo, Ranked Flex, Clash; custom SR Draft Pick and Tournament Draft. */
  draft: [400, 420, 440, 700, 3110, 3130],
  /** Blind Pick, Swiftplay, Quickplay, Co-op vs. AI, custom SR Blind Pick: picks and spells, no bans. */
  blind: [430, 480, 490, 870, 880, 890, 3100],
  /** ARAM, ARAM Clash, ARAM: Mayhem: spells and rune page only. */
  aram: [450, 720, 2400],
}
export function isAutoPickQueueGroup(v: string | null | undefined): v is AutoPickQueueGroup {
  return !!v && (AutoPickQueueGroups as readonly string[]).includes(v)
}
/** AutoPickQueueGroups.ForQueue: null when the queue isn't automated. */
export function autoPickGroupForQueue(queueId: number): AutoPickQueueGroup | null {
  return AutoPickQueueGroups.find((g) => AutoPickQueueIds[g].includes(queueId)) ?? null
}

/** AutoPickRoles: 'ANY' (no assigned role, and the defaults a role preset falls back to) + the five roles. */
export type AutoPickRole = 'ANY' | 'TOP' | 'JUNGLE' | 'MIDDLE' | 'BOTTOM' | 'UTILITY'
export const AutoPickRoles: readonly AutoPickRole[] = ['ANY', 'TOP', 'JUNGLE', 'MIDDLE', 'BOTTOM', 'UTILITY']
/** AutoPickRoles.IsAllowed: ARAM presets are 'ANY' only. */
export function isAutoPickRoleAllowed(group: AutoPickQueueGroup, role: string | null | undefined): role is AutoPickRole {
  return !!role && (AutoPickRoles as readonly string[]).includes(role) && (group !== 'aram' || role === 'ANY')
}

/**
 * Automatic ban, pick, spells and rune page, run by the connector (AutoPickUpdated); replayed, never pushed. No player
 * names or ids: champions, spells and rune page ids only. The rules are documented on the C# record and in
 * docs/auto-pick.md.
 */
export interface AutoPickState {
  settings: AutoPickSettings
  /** PauseAutoPick: no writes in the current (or next) champ select; cleared when it ends, on None or disconnect. */
  pausedForThisGame: boolean
  /** Null outside champ select. */
  run: AutoPickRun | null
  /** Connector clock (ISO). */
  updatedAt: string
}

/** Saved on the PC (auto-pick.json in the connector's data folder). */
export interface AutoPickSettings {
  /** Master switch; off by default. */
  enabled: boolean
  /** At most one per (queueGroup, role), never empty, ordered by group then role; at most 13 (AutoPickSettings.MaxPresets). */
  presets: AutoPickPreset[]
}

/** One preset. */
export interface AutoPickPreset {
  queueGroup: AutoPickQueueGroup
  role: AutoPickRole
  /** Priority order, distinct, at most AutoPickMaxBans; empty for aram. */
  banChampionIds: number[]
  /** Priority order, distinct, at most AutoPickMaxPicks; empty for aram. */
  pickChampionIds: number[]
  /** D; 0 = leave the spells alone (spell2Id is 0 too). */
  spell1Id: number
  /** F; 0 = none. */
  spell2Id: number
  /** RunePage.id; 0 = leave the page alone. */
  runePageId: number
  /**
   * runePageId's name as the client showed it when the preset was saved (filled in by the connector, not part of
   * 'SetAutoPickPreset'), so the phone can name it while League is closed; '' when unknown.
   */
  runePageName: string
}

/** AutoPickSettings.Resolve: the role's preset field by field, falling back to the group's 'ANY' preset. */
export function resolveAutoPickPreset(
  settings: AutoPickSettings,
  group: AutoPickQueueGroup,
  role: AutoPickRole,
): AutoPickPreset | null {
  const any = settings.presets.find((p) => p.queueGroup === group && p.role === 'ANY') ?? null
  const own = role === 'ANY' ? null : (settings.presets.find((p) => p.queueGroup === group && p.role === role) ?? null)
  if (!own || !any) return own ?? any
  const ownSpells = own.spell1Id > 0 && own.spell2Id > 0
  return {
    ...own,
    banChampionIds: own.banChampionIds.length > 0 ? own.banChampionIds : any.banChampionIds,
    pickChampionIds: own.pickChampionIds.length > 0 ? own.pickChampionIds : any.pickChampionIds,
    spell1Id: ownSpells ? own.spell1Id : any.spell1Id,
    spell2Id: ownSpells ? own.spell2Id : any.spell2Id,
    runePageId: own.runePageId > 0 ? own.runePageId : any.runePageId,
    runePageName: own.runePageId > 0 ? own.runePageName : any.runePageName,
  }
}

/** AutoPickRunStatus */
export type AutoPickRunStatus = 'active' | 'disabled' | 'paused' | 'unsupported' | 'no-preset'

/** The automation in the current champ select. */
export interface AutoPickRun {
  /** '' when this queue isn't automated. */
  queueGroup: AutoPickQueueGroup | ''
  /** The role whose preset applies; '' when this queue isn't automated. */
  role: AutoPickRole | ''
  status: AutoPickRunStatus
  pending: AutoPickPending | null
  /** Oldest first, at most AutoPickMaxLogEntries. */
  log: AutoPickLogEntry[]
  /** This champ select's run (a new champ select gets a new, larger id). */
  id: number
}

/** AutoPickActionKinds (the LCU's action type). */
export type AutoPickActionKind = 'ban' | 'pick'

/** An auto-hovered action that completes unless cancelled ('CancelAutoPickAction') or changed by the user. */
export interface AutoPickPending {
  kind: AutoPickActionKind
  actionId: number
  championId: number
  /**
   * Connector clock (ISO). Count down from `completesAt − updatedAt` starting when the update arrived; don't compare it
   * with the phone's clock.
   */
  completesAt: string
  /** False once the connector has started the lock-in / ban request. */
  cancellable: boolean
}

/** AutoPickLogKinds */
export type AutoPickLogKind =
  | 'banned'
  | 'locked'
  | 'hovered'
  | 'cancelled'
  | 'manual'
  | 'skipped'
  | 'spells'
  | 'rune-page'
  | 'failed'

/** One thing the automation did. */
export interface AutoPickLogEntry {
  /** Connector clock (ISO). */
  at: string
  kind: AutoPickLogKind
  /** 0 for spells / rune page / nothing found. */
  championId: number
  /** The connector's short English sentence ("Auto-banned Zed"), at most 80 chars, no player names. */
  text: string
}
