<script setup lang="ts">
import { computed } from 'vue'
import { championIconUrl, championName } from '../../ddragon'
import { vImgLoaded } from '../../lib/vImgLoaded'

/**
 * Champion portrait (Data Dragon, shimmer until loaded). Rounded squares, never circles. `alt` is the champion name
 * unless `decorative` (the name is printed next to it). `intent`: hovered but not locked in, translucent — on the draft
 * board, where the opacity says it; ARAM's team lists keep portraits opaque and say "Hovering …".
 */
const props = withDefaults(
  defineProps<{
    championId: number
    /** `tile`: icon-tile size, the lead of a compact row (In Game event feed). */
    size?: 'grid' | 'row' | 'ban' | 'tile'
    /**
     * `acting` / `enemy-acting`: breathing ally / enemy ring during that player's turn. `dead`: dimmed gray (In Game;
     * the row says when they respawn).
     */
    state?: 'locked' | 'empty' | 'you' | 'acting' | 'enemy-acting' | 'banned' | 'dead' | null
    /** Hovered, not locked in; on top of the ring states. */
    intent?: boolean
    decorative?: boolean
    popping?: boolean
  }>(),
  { size: 'row', state: null },
)
const url = computed(() => (props.championId > 0 ? championIconUrl(props.championId) : null))
</script>

<template>
  <span
    class="lr-champ"
    :class="[
      size !== 'row' && `lr-champ--${size}`,
      state && state !== 'locked' && `is-${state}`,
      intent && 'is-intent',
      !url && 'is-empty',
      popping && 'is-pop',
    ]"
  >
    <img
      v-if="url"
      v-img-loaded
      :src="url"
      :alt="decorative ? '' : championName(championId)"
      loading="lazy"
      decoding="async"
      draggable="false"
    />
  </span>
</template>
