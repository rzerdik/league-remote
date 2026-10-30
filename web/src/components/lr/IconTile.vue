<script setup lang="ts">
import type { Component } from 'vue'

export type TileColor =
  | 'red'
  | 'orange'
  | 'green'
  | 'blue'
  | 'cyan'
  | 'mint'
  | 'indigo'
  | 'purple'
  | 'pink'
  | 'gray'
  | 'dark'
  | 'accent'

/** Settings-style coloured icon tile: white symbol on a system colour. Decorative — the row label carries meaning. */
defineProps<{ icon: Component; color?: TileColor }>()
</script>

<template>
  <span class="lr-tile" :class="`lr-tile--${color ?? 'gray'}`" aria-hidden="true">
    <component :is="icon" :stroke-width="2" />
  </span>
</template>
