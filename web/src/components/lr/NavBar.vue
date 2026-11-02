<script setup lang="ts">
import { navState } from '../../composables/useCollapsingNav'

/**
 * Transparent navigation bar with floating glass buttons. The screen's LargeTitle scrolls with the content; once it
 * passes under the bar the inline title fades in and the scroll-edge band appears. Scrolled to the top: no background.
 */
/** `inline`: always show the inline title (screens without a large title, e.g. a conversation). */
defineProps<{ title: string; inline?: boolean }>()
</script>

<template>
  <header class="lr-nav" :data-collapsed="inline || navState.collapsed">
    <div class="lr-nav-bar">
      <div class="lr-nav-lead"><slot name="leading" /></div>
      <div class="lr-nav-title headline" aria-hidden="true">{{ title }}</div>
      <!-- LargeTitle #trailing buttons dock here (#lr-nav-trailing) once the title collapses (Add Friend, chat). -->
      <div id="lr-nav-trailing" class="lr-nav-trail"><slot name="trailing" /></div>
    </div>
  </header>
</template>
