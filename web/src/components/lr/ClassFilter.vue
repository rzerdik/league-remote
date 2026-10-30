<script setup lang="ts">
import { CrosshairIcon, HeartHandshakeIcon, ShieldIcon, SwordIcon, SwordsIcon, WandSparklesIcon } from '@lucide/vue'
import type { Component } from 'vue'
import { CHAMPION_CLASSES, type ChampionClass } from '../../ddragon'
import { tapFeedback } from '../../haptics'

/**
 * The champion class filter of the League client's picker (Assassin, Fighter, Mage, Marksman, Support, Tank) as a row
 * of icon toggles under the search field. One class at a time: tapping the active one clears it (v-model is null then).
 * Named for VoiceOver ("Mage, selected"); the row is a group, each toggle a 44-pt button.
 */
const model = defineModel<ChampionClass | null>({ required: true })

const ICONS: Record<ChampionClass, Component> = {
  Assassin: SwordIcon,
  Fighter: SwordsIcon,
  Mage: WandSparklesIcon,
  Marksman: CrosshairIcon,
  Support: HeartHandshakeIcon,
  Tank: ShieldIcon,
}

function toggle(c: ChampionClass) {
  tapFeedback()
  model.value = model.value === c ? null : c
}
</script>

<template>
  <div class="lr-class-filter" role="group" aria-label="Filter by Class">
    <button
      v-for="c in CHAMPION_CLASSES"
      :key="c"
      type="button"
      class="lr-class-filter-btn"
      :class="{ 'is-selected': model === c }"
      :aria-pressed="model === c"
      :aria-label="c"
      @click="toggle(c)"
    >
      <component :is="ICONS[c]" :stroke-width="2" aria-hidden="true" />
    </button>
  </div>
</template>
