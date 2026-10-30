<script setup lang="ts">
import { computed, ref, type Component } from 'vue'

/**
 * Monogram avatar for a person (circles are people; champions are rounded squares). Decorative: the name is next to it.
 * `icon`: a symbol in the circle instead of initials, in the accent colour, for an action row that sits in a list of
 * people (Invite Friends in the party list), so its label lines up with the names.
 * `imageUrl`: a picture in the circle (the player's own profile icon), over the initials until it has loaded; the
 * initials stay if it fails. `size` `lg`: the Profile tab's header.
 */
const props = defineProps<{ name?: string; icon?: Component; imageUrl?: string | null; size?: 'lg' }>()
const initials = computed(() => (props.name || '?').trim().slice(0, 2).toUpperCase())
const failed = ref<string | null>(null)
</script>

<template>
  <span
    class="lr-avatar"
    :class="[size === 'lg' ? 'title-2' : 'subheadline', { 'lr-avatar--symbol': icon, 'lr-avatar--lg': size === 'lg' }]"
    aria-hidden="true"
  >
    <component :is="icon" v-if="icon" :stroke-width="2.25" />
    <template v-else>{{ initials }}</template>
    <img
      v-if="imageUrl && failed !== imageUrl"
      :src="imageUrl"
      alt=""
      decoding="async"
      draggable="false"
      @load="($event.target as HTMLImageElement).classList.add('is-loaded')"
      @error="failed = imageUrl"
    />
  </span>
</template>
