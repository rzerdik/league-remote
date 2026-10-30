<script setup lang="ts">
import { computed } from 'vue'
import { itemIconUrl } from '../../ddragon'
import { vImgLoaded } from '../../lib/vImgLoaded'

/**
 * Item art (Data Dragon, shimmer until loaded) in a match-history strip; an empty slot (id 0) is an empty well, as in
 * the client. Decorative: the strip around it (LoadoutStrip) names the items for VoiceOver.
 */
const props = defineProps<{ itemId: number }>()
const url = computed(() => itemIconUrl(props.itemId))
</script>

<template>
  <span class="lr-item" :class="{ 'is-empty': !itemId }">
    <img v-if="url" v-img-loaded :src="url" alt="" loading="lazy" decoding="async" draggable="false" />
  </span>
</template>
