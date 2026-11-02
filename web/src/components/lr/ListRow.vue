<script setup lang="ts">
import { CheckIcon, ChevronRightIcon, ChevronsUpDownIcon } from '@lucide/vue'
import { computed, type Component, type FunctionalComponent } from 'vue'
import type { StatusTone } from '../../labels'
import ActivityIndicator from './ActivityIndicator.vue'
import IconTile, { type TileColor } from './IconTile.vue'
import LrMenu, { type MenuItem } from './LrMenu.vue'
import StatusLine from './StatusLine.vue'

/**
 * One row of an inset-grouped list. Renders an <li>; when `tappable` the row is a <button> inside it (pressed state on
 * pointer-down). Lead: `icon` (+ `tileColor`) for a Settings-style tile, or the `lead` slot (Avatar, ChampionIcon).
 * Trailing: `value` (label-secondary, with a status dot via `valueTone`), the `trailing` slot, then a chevron
 * (`chevron`), pop-up marker (`popUp`), checkmark (`checked`) or activity indicator (`busy`).
 * `menu`: the row is a pop-up button that opens that Menu (rune page): it shows the pop-up marker by itself.
 * The default slot replaces the title block with a control that fills the row (the volume slider).
 */
const props = defineProps<{
  title?: string
  subtitle?: string
  value?: string
  valueTone?: StatusTone
  valuePulse?: boolean
  icon?: Component
  tileColor?: TileColor
  variant?: 'default' | 'danger' | 'you'
  tappable?: boolean
  chevron?: boolean
  popUp?: boolean
  checked?: boolean
  busy?: boolean
  disabled?: boolean
  /** Text-only row (shorter minimum height). */
  text?: boolean
  /** Let the title/subtitle wrap instead of truncating. */
  wrap?: boolean
  ariaLabel?: string
  menu?: (MenuItem | 'separator')[]
  menuTitle?: string
}>()
defineEmits<{ click: [e: MouseEvent] }>()

const pressable = computed(() => props.tappable || !!props.menu)
const classes = computed(() => [
  'lr-row',
  props.variant && props.variant !== 'default' && `lr-row--${props.variant}`,
  { 'lr-row--action': pressable.value, 'lr-row--text': props.text },
])

/**
 * A value with a marker after it (Rune Page's pop-up marker, a chevron, a checkmark, a spinner) wraps as one: a long
 * value that moves under the title wraps its words beside the marker instead of pushing the marker onto a line of its
 * own.
 */
const endGrouped = computed(() => !!props.value && (props.busy || props.checked || props.popUp || !!props.menu || props.chevron))

/** Renders its slot as is: the row without a menu around it (or the trailing end without a group). */
const Bare: FunctionalComponent = (_, { slots }) => slots.default?.()
</script>

<template>
  <li :class="pressable ? 'lr-row-li' : classes">
    <component :is="menu ? LrMenu : Bare" v-bind="menu ? { items: menu, title: menuTitle } : {}">
      <component
        :is="pressable ? 'button' : 'div'"
        :type="pressable ? 'button' : undefined"
        :class="pressable ? classes : 'lr-row-inner'"
        :disabled="pressable ? disabled || busy : undefined"
        :aria-haspopup="tappable && popUp ? 'dialog' : undefined"
        :aria-label="ariaLabel"
        @click="tappable && $emit('click', $event)"
      >
        <span v-if="icon || $slots.lead" class="lr-row-lead">
          <slot name="lead"><IconTile v-if="icon" :icon="icon" :color="tileColor" /></slot>
        </span>
        <span class="lr-row-main">
          <slot>
            <span class="lr-row-text" :class="{ 'lr-row-text--wrap': wrap }">
              <span class="body"><slot name="title">{{ title }}</slot></span>
              <span v-if="subtitle || $slots.subtitle" class="subheadline"><slot name="subtitle">{{ subtitle }}</slot></span>
            </span>
          </slot>
          <component :is="endGrouped ? 'span' : Bare" v-bind="endGrouped ? { class: 'lr-row-end' } : {}">
            <StatusLine v-if="value" class="lr-row-value body" :tone="valueTone" :pulse="valuePulse" :text="value" />
            <slot name="trailing" />
            <ActivityIndicator v-if="busy" decorative />
            <CheckIcon v-else-if="checked" class="lr-row-check" :stroke-width="2.5" aria-label="Selected" role="img" />
            <ChevronsUpDownIcon
              v-else-if="popUp || menu"
              class="lr-row-chevron lr-row-chevron--popup"
              :stroke-width="2.25"
              aria-hidden="true"
            />
            <ChevronRightIcon v-else-if="chevron" class="lr-row-chevron" :stroke-width="2.5" aria-hidden="true" />
          </component>
        </span>
      </component>
    </component>
  </li>
</template>

<style>
/* Zero specificity so list items with their own layout (spell slots) win. */
:where(.lr-list > li) {
  display: block;
}
.lr-row-inner {
  display: contents;
}
</style>
