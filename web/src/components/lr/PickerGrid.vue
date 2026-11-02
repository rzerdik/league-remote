<script setup lang="ts" generic="T extends { id: number; name: string; unavailable?: boolean }">
/**
 * A grid of art tiles to pick from (champion picker, summoner spell picker, the ARAM bench): art (`art` slot, per item)
 * with the name under it. The selected tile gets the selection ring (`accent`; `red` while banning) and `aria-pressed`.
 * Presses scale on touch-down. Four columns, fewer at large text sizes (`.lr-grid`). Empty: `emptyText`.
 * `disabled` stops every tile (a swap on its way); an item's `unavailable` grays and disables just that tile (a bench
 * champion you can't play).
 */
withDefaults(
  defineProps<{
    items: T[]
    selectedId?: number
    tone?: 'accent' | 'red'
    emptyText?: string
    disabled?: boolean
  }>(),
  { tone: 'accent', selectedId: 0, emptyText: '', disabled: false },
)
defineEmits<{ select: [item: T] }>()
defineSlots<{ art(props: { item: T }): unknown }>()
</script>

<template>
  <div v-if="items.length" class="lr-grid">
    <button
      v-for="item in items"
      :key="item.id"
      type="button"
      class="lr-grid-tile"
      :class="{ 'is-ban': tone === 'red', 'is-unavailable': item.unavailable }"
      :aria-pressed="item.id === selectedId"
      :disabled="disabled || item.unavailable"
      @click="$emit('select', item)"
    >
      <span class="lr-grid-art"><slot name="art" :item="item" /></span>
      <span class="caption-1">{{ item.name }}</span>
    </button>
  </div>
  <p v-else-if="emptyText" class="lr-grid-empty subheadline">{{ emptyText }}</p>
</template>
