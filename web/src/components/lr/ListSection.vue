<script setup lang="ts">
/**
 * Inset-grouped list section: optional prominent header (title-3, optional trailing count), the rows (default slot,
 * ListRow elements inside the rounded list) and an optional footer (footnote). `bare` drops the list wrapper for a
 * single card (Tip, LiveGameCard).
 */
defineProps<{ header?: string; headerTrailing?: string; footer?: string; bare?: boolean; listClass?: string }>()
</script>

<template>
  <section class="lr-section">
    <h2 v-if="header || $slots.header" class="lr-section-header title-3">
      <span><slot name="header">{{ header }}</slot></span>
      <span v-if="headerTrailing" class="lr-secondary subheadline">{{ headerTrailing }}</span>
    </h2>
    <slot v-if="bare" />
    <ul v-else class="lr-list" :class="listClass"><slot /></ul>
    <p v-if="footer || $slots.footer" class="lr-section-footer footnote"><slot name="footer">{{ footer }}</slot></p>
  </section>
</template>
