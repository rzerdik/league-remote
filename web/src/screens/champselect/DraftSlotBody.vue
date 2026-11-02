<script setup lang="ts">
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import SpellIcon from '../../components/lr/SpellIcon.vue'

/**
 * What a draft board row shows: the portrait on the screen's outer edge, spells stacked beside it (your team only: League
 * doesn't show the enemy's), the name towards the middle. A hover is the translucent portrait, no word. An empty slot
 * shows its pick number, which is how the board tells the draft order.
 */
defineProps<{
  championId: number
  state?: 'you' | 'acting' | 'enemy-acting' | null
  intent?: boolean
  popping?: boolean
  /** The player's pick number (1–10). */
  order: number
  title: string
  line?: string
  lineAccent?: boolean
  spellIds?: number[]
  spellsLabel?: string
}>()
</script>

<template>
  <ChampionIcon v-if="championId > 0" :champion-id="championId" :state="state" :intent="intent" :popping="popping" decorative />
  <span
    v-else
    class="lr-champ is-empty lr-slot-order caption-1"
    :class="state && `is-${state}`"
    aria-hidden="true"
  >{{ order || '' }}</span>
  <span v-if="spellIds" class="lr-slot-spells" role="img" :aria-label="spellsLabel">
    <SpellIcon v-for="(id, i) in spellIds" :key="i" :spell-id="id" />
  </span>
  <span class="lr-slot-text">
    <span class="headline">{{ title }}</span>
    <span v-if="line" class="caption-1" :class="lineAccent ? 'lr-accent-text' : 'lr-secondary'"><slot name="line">{{ line }}</slot></span>
  </span>
</template>
