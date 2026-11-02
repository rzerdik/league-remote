<script setup lang="ts">
import { TrophyIcon } from '@lucide/vue'
import { computed, ref } from 'vue'
import { rankEmblemUrl } from '../../labels'
import IconTile from './IconTile.vue'

/**
 * A rank's emblem, the League client's own art from the PC app. Decorative: the rank is named next to it. Unranked,
 * placements, or a PC app that can't find the art (League not installed there) show a gray trophy tile instead, centred
 * in the emblem's slot so a row's text starts at the same place either way.
 */
const props = defineProps<{ tier: string }>()
const url = computed(() => rankEmblemUrl(props.tier))
const failed = ref<string | null>(null)
</script>

<template>
  <span v-if="url && failed !== url" class="lr-rank-icon" aria-hidden="true">
    <img :src="url" alt="" decoding="async" draggable="false" @error="failed = url" />
  </span>
  <span v-else class="lr-rank-icon lr-rank-icon--tile"><IconTile :icon="TrophyIcon" color="gray" /></span>
</template>
