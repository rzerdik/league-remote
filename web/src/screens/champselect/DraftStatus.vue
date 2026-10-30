<script setup lang="ts">
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import type { DraftTurn } from '../../contracts'
import { championName } from '../../ddragon'
import type { DraftSlot } from '../../draft'

/**
 * The pick order (draft modes): every turn, its side (blue = your team, red = the enemy), the turn being played
 * highlighted and your own slot ringed in the accent. The stage and its timer aren't repeated here: they live in the
 * floating chrome that stays on screen (the action bar's status, or the picker's turn header while it is up).
 */
defineProps<{ order: { turn: DraftTurn; slots: DraftSlot[] }[] }>()

function slotLabel(s: DraftSlot) {
  const champ = championName(s.championId)
  const state = s.locked ? champ || 'Locked' : s.acting ? `picking${champ ? ` ${champ}` : ''}` : 'waiting'
  return `${s.ally ? '' : 'Enemy '}${s.label}: ${state}`
}
</script>

<template>
  <section class="lr-draft">
    <ol class="lr-draft-order" aria-label="Pick order">
      <li
        v-for="(g, i) in order"
        :key="i"
        class="lr-draft-turn"
        :style="{ '--n': g.slots.length }"
        :class="[g.slots[0]?.ally ? 'is-ally' : 'is-enemy', { 'is-now': g.turn.isInProgress, 'is-done': g.turn.completed }]"
      >
        <span
          v-for="s in g.slots"
          :key="s.cellId"
          class="lr-draft-slot"
          :class="{ 'is-you': s.you, 'is-acting': s.acting }"
          role="img"
          :aria-label="slotLabel(s)"
        >
          <ChampionIcon :champion-id="s.championId" size="ban" :state="s.locked ? null : 'intent'" decorative />
        </span>
      </li>
    </ol>
  </section>
</template>
