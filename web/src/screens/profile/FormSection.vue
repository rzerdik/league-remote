<script setup lang="ts">
import { computed } from 'vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import type { MatchSummary } from '../../contracts'
import { plural, record, winRate } from '../../format'
import { recentForm, streakLabel, streakSpoken } from '../../profileStats'

/**
 * Recent form over the last games that counted (profileStats.ts): one W / L chip per game, newest first — a win filled
 * green, a loss ringed red, the letter on each so it never rests on colour — then win rate, record and the current
 * streak. Hidden without a counted game.
 */
const props = defineProps<{ matches: MatchSummary[] }>()

const form = computed(() => recentForm(props.matches))
const last = computed(() => `Last ${plural(form.value.results.length, 'game')}`)
const spoken = computed(() => `${last.value}, newest first: ${form.value.results.join(', ')}`)
const cells = computed(() => [
  { label: 'Win Rate', value: winRate(form.value.wins, form.value.losses) },
  {
    label: 'Record',
    value: record(form.value.wins, form.value.losses),
    spoken: `${plural(form.value.wins, 'win')}, ${plural(form.value.losses, 'loss', 'losses')}`,
  },
  { label: 'Streak', value: streakLabel(form.value.streak), spoken: streakSpoken(form.value.streak) },
])
</script>

<template>
  <ListSection
    v-if="form.results.length"
    header="Recent Form"
    :header-trailing="last"
    footer="Matchmade games; remakes and custom games are left out."
  >
    <li class="form-strip">
      <span class="form-strip-chips" role="img" :aria-label="spoken">
        <span
          v-for="(r, i) in form.results"
          :key="i"
          class="caption-2 form-chip"
          :class="r === 'win' ? 'is-win' : 'is-loss'"
          aria-hidden="true"
        >{{ r === 'win' ? 'W' : 'L' }}</span>
      </span>
    </li>
    <StatGrid :cells="cells" />
  </ListSection>
</template>

<style scoped>
/* Like a StatGrid item: inset like a row, with the row separator under it. */
.form-strip {
  padding-left: var(--space-5);
}
.form-strip-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-1);
  padding: var(--space-4) var(--space-5) var(--space-4) 0;
  box-shadow: inset 0 calc(-1 * var(--lr-hairline)) 0 var(--separator);
}
.form-chip {
  /* min-: the letter grows with the text size beyond the token's cap. */
  box-sizing: border-box;
  min-width: var(--form-chip);
  min-height: var(--form-chip);
  padding: var(--space-0-5);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-full);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
.form-chip.is-win {
  background: var(--green);
  color: var(--on-green);
}
.form-chip.is-loss {
  box-shadow: inset 0 0 0 var(--lr-stroke) var(--red);
  color: var(--red-text);
}
</style>
