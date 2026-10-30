<script setup lang="ts">
import { resultLabel } from '../../profile'

/**
 * A game's result as a word in its colour: Victory green, Defeat red, Remake / Aborted secondary (they don't count).
 * The word carries it; the colour only repeats it. Inherits its type style from where it sits.
 */
defineProps<{ result: string }>()
</script>

<template>
  <span class="match-result" :class="`is-${result}`">{{ resultLabel(result) }}</span>
</template>

<style scoped>
.match-result {
  color: var(--label-secondary);
}
.match-result.is-win {
  color: var(--green-text);
}
.match-result.is-loss {
  color: var(--red-text);
}
</style>
