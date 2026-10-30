<script setup lang="ts">
import { ref } from 'vue'
import { useCollapsingNav } from '../../composables/useCollapsingNav'

/**
 * The screen's large title (one per screen) and its one-line subtitle (default slot). The screen's own buttons (Add
 * Friend, the chat button in Lobby / Champion Select) go in `#trailing`: they sit at the end of the title row, as on
 * iOS, and dock into the nav bar once the title collapses under it, so they stay reachable while the content scrolls.
 */
defineProps<{ title: string }>()
const root = ref<HTMLElement | null>(null)
const { docked } = useCollapsingNav(root)
</script>

<template>
  <div ref="root" class="lr-largetitle" :class="{ 'lr-largetitle--trailing': $slots.trailing }">
    <div>
      <h1 class="large-title">{{ title }}</h1>
      <p v-if="$slots.default" class="subheadline"><slot /></p>
    </div>
    <div v-if="$slots.trailing" class="lr-largetitle-trail">
      <Teleport defer to="#lr-nav-trailing" :disabled="!docked">
        <span class="lr-largetitle-dock"><slot name="trailing" /></span>
      </Teleport>
    </div>
  </div>
</template>
