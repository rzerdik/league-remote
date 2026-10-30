import { WifiOffIcon } from '@lucide/vue'
import { computed, type Component } from 'vue'
import { isInGame } from './gameflow'
import type { StatusTone } from './labels'
import { liveState, store } from './store'

/** Where this phone's link to the PC is. Every status surface below words this one value. */
export type ConnectionPhase = 'offline' | 'connecting' | 'reconnecting' | 'unpaired' | 'signing-in' | 'ready'

const connectionPhase = computed<ConnectionPhase>(() => {
  switch (store.connection) {
    case 'disconnected':
      return 'offline'
    case 'connecting':
      return 'connecting'
    case 'reconnecting':
      return 'reconnecting'
  }
  if (!store.grant) return 'unpaired'
  return store.authenticated ? 'ready' : 'signing-in'
})

/** One word and tone per phase, shared by the status line and the Settings row. */
const PHASE: Record<ConnectionPhase, { tone: StatusTone; text: string; busy: boolean }> = {
  offline: { tone: 'red', text: 'Offline', busy: false },
  connecting: { tone: 'orange', text: 'Connecting…', busy: true },
  reconnecting: { tone: 'orange', text: 'Reconnecting…', busy: true },
  'signing-in': { tone: 'orange', text: 'Signing In…', busy: true },
  unpaired: { tone: 'gray', text: 'Not Paired', busy: false },
  ready: { tone: 'green', text: 'Connected', busy: false },
}

/** What a screen shows instead of its content while the phone can't reach the PC (ConnectionEmptyState). */
interface ConnectionEmpty {
  icon?: Component
  busy?: boolean
  title: string
  message?: string
  /** "Try Again": restart the connection (hub.ts `start`). */
  action?: string
}

/**
 * Why nothing from the PC can be shown right now, or null once it can. Every tab that shows the PC's data (Play,
 * Chat, Friends, Profile) starts with this, so a dropped connection never reads as "No Conversations" or an endless
 * spinner.
 */
export const connectionEmpty = computed<ConnectionEmpty | null>(() => {
  const phase = connectionPhase.value
  if (phase === 'ready') return null
  if (phase === 'offline')
    return { icon: WifiOffIcon, title: 'Offline', message: 'Check your connection.', action: 'Try Again' }
  if (phase === 'unpaired') return { title: 'Not Paired', message: 'Pair this phone with your PC.' }
  return { busy: true, title: PHASE[phase].text }
})

/** One status line for the nav subtitle: the link first, then League. Never shows plumbing words. */
export const connectionLine = computed<{ tone: StatusTone; pulse: boolean; busy: boolean; text: string }>(() => {
  if (store.pairing) return { tone: 'orange', pulse: false, busy: true, text: 'Pairing…' }
  const phase = connectionPhase.value
  if (phase !== 'ready') {
    const p = PHASE[phase]
    return { tone: p.tone, pulse: p.busy, busy: false, text: p.text }
  }
  if (!liveState.value && store.syncing.state) return { tone: 'gray', pulse: true, busy: false, text: 'Checking League…' }
  if (!liveState.value?.lcuConnected) return { tone: 'orange', pulse: false, busy: false, text: 'Waiting for League' }
  return { tone: 'green', pulse: false, busy: false, text: 'Ready' }
})

/** Settings › Connection › PC. */
export const pcStatus = computed<{ tone: StatusTone; text: string }>(() => PHASE[connectionPhase.value])

/** Settings › Connection › League. */
export const clientStatus = computed<{ tone: StatusTone; text: string }>(() => {
  const s = liveState.value
  if (!s) return { tone: 'gray', text: '—' }
  if (!s.lcuConnected) return { tone: 'red', text: 'Not Running' }
  if (isInGame(s.gameflowPhase)) return { tone: 'green', text: 'In Game' }
  return { tone: 'green', text: 'Connected' }
})

/** This phone's link alone (paired or not), for the pairing form's status line: red offline, orange on its way. */
export const linkStatus = computed<{ tone: StatusTone; text: string }>(() => {
  const phase = connectionPhase.value
  return phase === 'unpaired' || phase === 'ready' ? { tone: 'green', text: 'Connected' } : PHASE[phase]
})
