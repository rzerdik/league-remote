<script setup lang="ts">
import { computed } from 'vue'
import { runeIconUrl } from '../../ddragon'
import { vImgLoaded } from '../../lib/vImgLoaded'

/**
 * A keystone or rune tree (Data Dragon runesReforged art) in a circle, next to the summoner spells of a match-history
 * strip. Nothing at all while the rune is unknown (no art yet, or id 0). Decorative: LoadoutStrip names it.
 */
const props = defineProps<{ runeId: number }>()
const url = computed(() => runeIconUrl(props.runeId))
</script>

<template>
  <span v-if="url" class="lr-rune">
    <img v-img-loaded :src="url" alt="" loading="lazy" decoding="async" draggable="false" />
  </span>
</template>
