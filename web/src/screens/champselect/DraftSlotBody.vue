<script setup lang="ts">
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import SpellIcon from '../../components/lr/SpellIcon.vue'

/**
 * What a draft board row shows: the portrait on the screen's outer edge, spells stacked beside it (your team only: League
 * doesn't show the enemy's), the name towards the middle. A hover is the translucent portrait, no word. An empty slot is
 * an empty tile.
 */
defineProps<{
  championId: number
  state?: 'you' | 'acting' | 'enemy-acting' | null
  intent?: boolean
  popping?: boolean
  title: string
  line?: string
  lineAccent?: boolean
  spellIds?: number[]
  spellsLabel?: string
}>()
</script>

<template>
  <ChampionIcon v-if="championId > 0" :champion-id="championId" :state="state" :intent="intent" :popping="popping" decorative />
  <span v-else class="lr-champ is-empty" :class="state && `is-${state}`" aria-hidden="true" />
  <span v-if="spellIds" class="lr-slot-spells" role="img" :aria-label="spellsLabel">
    <SpellIcon v-for="(id, i) in spellIds" :key="i" :spell-id="id" />
  </span>
  <span class="lr-slot-text">
    <span class="headline">{{ title }}</span>
    <span v-if="line" class="caption-1" :class="lineAccent ? 'lr-accent-text' : 'lr-secondary'"><slot name="line">{{ line }}</slot></span>
  </span>
</template>
