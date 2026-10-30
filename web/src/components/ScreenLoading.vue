<script setup lang="ts">
import EmptyState from './lr/EmptyState.vue'

/** What a lazily loaded screen shows while its code is still on its way (lazy.ts; only on a slow first load). */
</script>

<template>
  <div class="lr-content">
    <EmptyState busy title="Loading" />
  </div>
</template>
