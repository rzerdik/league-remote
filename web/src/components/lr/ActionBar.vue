<script setup lang="ts">
import { Comment, Fragment, useSlots, type VNode } from 'vue'
import { sheetStack } from '../../composables/useSheet'
import ActivityIndicator from './ActivityIndicator.vue'
import StatusDot, { type DotTone } from './StatusDot.vue'
import TimerText from './TimerText.vue'

export interface ActionBarAccessory {
  /** The status dot (Champion Select: the acting side's colour, `DRAFT_DOT`). */
  tone?: DotTone
  pulsing?: boolean
  /** Activity indicator instead of the status dot. */
  busy?: boolean
  title: string
  detail?: string
  /** Trailing value that counts up (time in queue), tabular; `valueLabel` is what VoiceOver reads. */
  value?: string
  valueLabel?: string
  /** Trailing countdown (seconds left in a turn): a TimerText, orange / red near the end. */
  countdown?: number
  /** VoiceOver hears the countdown at 10 and 5 seconds (the screen's one timer that is always on screen). */
  announce?: boolean
}

/**
 * The floating bottom bar: the screen's ONE primary action (default slot: a row of buttons) plus an optional status
 * accessory above it. Hidden while a modal sheet is up (never glass on glass). `compact`: the buttons keep their own
 * width and sit centred instead of spanning the bar (a pill over the picker sheet).
 */
defineProps<{ accessory?: ActionBarAccessory | null; overSheet?: boolean; compact?: boolean }>()

const slots = useSlots()
/** True when the default slot renders something (a `v-if`ed button leaves only comments behind). */
function hasContent(nodes: VNode[] | undefined): boolean {
  return !!nodes?.some((n) => n.type !== Comment && (n.type !== Fragment || hasContent(n.children as VNode[])))
}
</script>

<template>
  <!-- In <body>, beside the teleported sheets: inside #app it would be stacked under them on iOS. -->
  <Teleport to="body">
  <div class="lr-edge-bottom" aria-hidden="true" />
  <div v-show="!sheetStack.modal" class="lr-actionbar" :class="{ 'lr-actionbar--over-sheet': overSheet }">
    <Transition name="lr-pop">
      <div v-if="accessory" class="lr-accessory lr-glass" role="status">
        <ActivityIndicator v-if="accessory.busy" decorative />
        <StatusDot v-else :tone="accessory.tone" :pulse="accessory.pulsing" />
        <span class="lr-accessory-main">
          <span class="headline">{{ accessory.title }}</span>
          <span v-if="accessory.detail" class="footnote">{{ accessory.detail }}</span>
        </span>
        <TimerText v-if="accessory.countdown != null" class="title-3" :seconds="accessory.countdown" :announce="accessory.announce" />
        <span v-else-if="accessory.value" class="lr-timer-value title-3" role="timer" aria-live="off" :aria-label="accessory.valueLabel">{{
          accessory.value
        }}</span>
      </div>
    </Transition>
    <div v-if="hasContent(slots.default?.())" class="lr-actionbar-row" :class="{ 'lr-actionbar-row--compact': compact }">
      <slot />
    </div>
  </div>
  </Teleport>
</template>
