import { ArrowLeftRightIcon, ArrowUpDownIcon, RepeatIcon } from '@lucide/vue'
import type { Component } from 'vue'
import type { SwapKind } from '../../contracts'

export type SwapCommand = 'RequestSwap' | 'AcceptSwap' | 'DeclineSwap' | 'CancelSwap'

/** Menu item labels for sending a request ("Swap Pick Order") and cancelling one we sent. */
export const SWAP_ACTION: Record<SwapKind, string> = {
  PickOrder: 'Swap Pick Order',
  Position: 'Swap Roles',
  Trade: 'Trade Champions',
}
export const SWAP_CANCEL: Record<SwapKind, string> = {
  PickOrder: 'Cancel Pick Order Swap',
  Position: 'Cancel Role Swap',
  Trade: 'Cancel Trade',
}
/** Incoming request banner title. */
export const SWAP_QUESTION: Record<SwapKind, string> = {
  PickOrder: 'Swap Pick Order?',
  Position: 'Swap Roles?',
  Trade: 'Trade Champions?',
}
export const SWAP_ICON: Record<SwapKind, Component> = {
  PickOrder: ArrowUpDownIcon,
  Position: ArrowLeftRightIcon,
  Trade: RepeatIcon,
}
/** The tag on a teammate row while our request waits for their answer (the symbol says which kind). */
export const SWAP_SENT: Record<SwapKind, string> = {
  PickOrder: 'Swap Sent',
  Position: 'Swap Sent',
  Trade: 'Trade Sent',
}
/** What VoiceOver hears for that tag. */
export const SWAP_SENT_SPOKEN: Record<SwapKind, string> = {
  PickOrder: 'Pick order swap sent',
  Position: 'Role swap sent',
  Trade: 'Trade sent',
}
/** Fixed menu order. */
export const SWAP_ORDER: readonly SwapKind[] = ['PickOrder', 'Position', 'Trade']
