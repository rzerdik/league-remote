<script setup lang="ts">
/**
 * Value-over-label stat cells as one item of an inset-grouped list (a team's totals, your live KDA / CS / gold): as
 * many columns as fit, fewer at large text sizes. The label reads first for VoiceOver (a `dl`), the value is drawn on
 * top (headline, tabular). `spoken` replaces a value VoiceOver would misread ("7 / 2 / 9"); `accent` tints a value.
 */
defineProps<{ cells: { label: string; value: string; spoken?: string; accent?: boolean }[] }>()
</script>

<template>
  <li class="lr-stats">
    <dl class="lr-stats-grid">
      <div v-for="c in cells" :key="c.label" class="lr-stats-cell" :class="{ 'is-accent': c.accent }">
        <dt class="caption-1">{{ c.label }}</dt>
        <dd class="headline" :aria-label="c.spoken">{{ c.value }}</dd>
      </div>
    </dl>
  </li>
</template>
