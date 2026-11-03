<script lang="ts">
/** The bar that measured last: a screen change mounts the next bar before the old one leaves, which must not clear it. */
let measuredBy: object | null = null
</script>

<script setup lang="ts">
import { Comment, Fragment, onBeforeUnmount, onMounted, ref, useSlots, type VNode } from 'vue'
import { useRefraction } from '../../composables/useRefraction'
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
 * accessory above it. Hidden while a modal sheet is up (never glass on glass). `#accessory-action`: one labelled small
 * button (`LrButton variant="gray" size="sm"`) at the end of the accessory's capsule, for a screen whose only action
 * belongs to the status (Cancel next to Finding Match), so the bar takes one row. It moves under the status at large
 * text. The bar publishes its height as `--lr-actionbar-h`, so `.lr-content` lets the last row scroll clear of it.
 */
defineProps<{ accessory?: ActionBarAccessory | null; overSheet?: boolean }>()

const slots = useSlots()
/** True when the default slot renders something (a `v-if`ed button leaves only comments behind). */
function hasContent(nodes: VNode[] | undefined): boolean {
  return !!nodes?.some((n) => n.type !== Comment && (n.type !== Fragment || hasContent(n.children as VNode[])))
}

const HEIGHT_VAR = '--lr-actionbar-h'
const bar = ref<HTMLElement | null>(null)
const capsule = ref<HTMLElement | null>(null)
useRefraction(capsule)
const self = {}
let observer: ResizeObserver | null = null
onMounted(() => {
  observer = new ResizeObserver(() => {
    const height = bar.value?.offsetHeight ?? 0
    // 0: hidden under a modal sheet. The content keeps its padding, so it doesn't jump behind the sheet.
    if (!height) return
    measuredBy = self
    document.documentElement.style.setProperty(HEIGHT_VAR, `${height}px`)
  })
  if (bar.value) observer.observe(bar.value)
})
onBeforeUnmount(() => {
  observer?.disconnect()
  if (measuredBy !== self) return
  measuredBy = null
  document.documentElement.style.removeProperty(HEIGHT_VAR)
})
</script>

<template>
  <!-- In <body>, beside the teleported sheets: inside #app it would be stacked under them on iOS. -->
  <Teleport to="body">
  <div class="lr-edge-bottom" :class="{ 'lr-edge-bottom--over-sheet': overSheet }" aria-hidden="true" />
  <div ref="bar" v-show="!sheetStack.modal" class="lr-actionbar" :class="{ 'lr-actionbar--over-sheet': overSheet }">
    <Transition name="lr-pop">
      <div v-if="accessory" ref="capsule" class="lr-accessory lr-glass">
        <!-- The live region is the status alone: VoiceOver mustn't hear the button again with every change. -->
        <div class="lr-accessory-status" role="status">
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
        <div v-if="hasContent(slots['accessory-action']?.())" class="lr-accessory-action">
          <slot name="accessory-action" />
        </div>
      </div>
    </Transition>
    <div v-if="hasContent(slots.default?.())" class="lr-actionbar-row">
      <slot />
    </div>
  </div>
  </Teleport>
</template>
