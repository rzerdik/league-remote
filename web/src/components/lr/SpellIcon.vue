<script setup lang="ts">
import { computed } from 'vue'
import { spellIconUrl } from '../../ddragon'
import { vImgLoaded } from '../../lib/vImgLoaded'

/**
 * Summoner spell art (Data Dragon, shimmer until loaded): `sm` 20 in a match-history strip, `row` 24 in a team row,
 * `slot` 36 in a Loadout slot, `grid` 48 in the spell picker. Decorative: the spell's name is always printed or in the parent's label.
 */
const props = withDefaults(defineProps<{ spellId: number; size?: 'sm' | 'row' | 'slot' | 'grid' }>(), { size: 'row' })
const url = computed(() => spellIconUrl(props.spellId))
</script>

<template>
  <span class="lr-spell" :class="size !== 'row' && `lr-spell--${size}`">
    <img v-if="url" v-img-loaded :src="url" alt="" loading="lazy" decoding="async" draggable="false" />
  </span>
</template>
