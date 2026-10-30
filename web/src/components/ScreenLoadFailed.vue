<script setup lang="ts">
import { CloudOffIcon } from '@lucide/vue'
import { computed } from 'vue'
import { reloadWouldInterrupt } from '../lib/reload'
import EmptyState from './lr/EmptyState.vue'

/**
 * What a lazily loaded screen shows when its code couldn't be fetched (lazy.ts, after its retries). The usual cause is
 * an update that replaced the app's files (or no network): Try Again reloads the app, which fetches the current
 * version. Not during a ready check, the draft or a game (reload.ts): then it says the screen comes after the game,
 * and Try Again only loads the screen again in place.
 */
const emit = defineEmits<{ retry: [] }>()

const busy = computed(() => reloadWouldInterrupt())

function tryAgain() {
  if (reloadWouldInterrupt()) emit('retry')
  else location.reload()
}
</script>

<template>
  <div class="lr-content">
    <EmptyState
      :icon="CloudOffIcon"
      title="Couldn’t Load"
      :message="busy ? 'It will load after the game.' : 'Try again to load the latest version.'"
      action-label="Try Again"
      @action="tryAgain"
    />
  </div>
</template>
