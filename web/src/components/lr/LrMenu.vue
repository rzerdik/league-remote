<script setup lang="ts">
import { CheckIcon } from '@lucide/vue'
import {
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuPortal,
  DropdownMenuRoot,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from 'reka-ui'
import { computed, type Component } from 'vue'
import { haptic } from '../../haptics'
import { SPACE } from '../../styles/tokens'

export interface MenuItem {
  label: string
  icon?: Component
  /** Choice menus (rune page): the current choice shows a leading checkmark. */
  checked?: boolean
  destructive?: boolean
  disabled?: boolean
  onSelect: () => void
}

/**
 * Glass pop-up menu anchored to its trigger (default slot, rendered as-child). Grows from the trigger corner and
 * shrinks back into it; tap outside or Escape closes. Unavailable actions stay visible but disabled.
 */
const props = withDefaults(
  defineProps<{ items: (MenuItem | 'separator')[]; title?: string; align?: 'start' | 'end'; side?: 'top' | 'bottom' }>(),
  { align: 'end', side: 'bottom' },
)
const open = defineModel<boolean>('open', { default: false })
const choice = computed(() => props.items.some((i) => i !== 'separator' && i.checked !== undefined))
/** Destructive items go last in their group, whatever order they were built in. */
const ordered = computed(() => {
  const out: (MenuItem | 'separator')[] = []
  let destructive: MenuItem[] = []
  for (const item of props.items) {
    if (item !== 'separator' && item.destructive) destructive.push(item)
    else if (item === 'separator') {
      out.push(...destructive, item)
      destructive = []
    } else out.push(item)
  }
  return [...out, ...destructive]
})

function select(item: MenuItem) {
  haptic('selection')
  item.onSelect()
}
</script>

<template>
  <DropdownMenuRoot v-model:open="open" :modal="false">
    <DropdownMenuTrigger as-child><slot /></DropdownMenuTrigger>
    <DropdownMenuPortal>
      <DropdownMenuContent
        class="lr-menu lr-glass-thick"
        :side="side"
        :align="align"
        :side-offset="SPACE.space2"
        :collision-padding="SPACE.space4"
      >
        <DropdownMenuLabel v-if="title" class="lr-menu-title footnote">{{ title }}</DropdownMenuLabel>
        <template v-for="(item, i) in ordered" :key="i">
          <DropdownMenuSeparator v-if="item === 'separator'" class="lr-menu-sep" />
          <DropdownMenuItem
            v-else
            class="lr-menu-item body"
            :class="{ 'lr-menu-item--danger': item.destructive }"
            :disabled="item.disabled"
            :role="choice ? 'menuitemradio' : 'menuitem'"
            :aria-checked="choice ? !!item.checked : undefined"
            @select="select(item)"
          >
            <span v-if="choice" class="lr-menu-check">
              <CheckIcon v-if="item.checked" class="lr-icon" :stroke-width="2.5" aria-hidden="true" />
            </span>
            <span>{{ item.label }}</span>
            <component :is="item.icon" v-if="item.icon" class="lr-icon" :stroke-width="1.75" aria-hidden="true" />
          </DropdownMenuItem>
        </template>
      </DropdownMenuContent>
    </DropdownMenuPortal>
  </DropdownMenuRoot>
</template>
