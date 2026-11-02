<script setup lang="ts">
import { CloudOffIcon, InfoIcon, ScrollTextIcon } from '@lucide/vue'
import { ref, watch } from 'vue'
import { appVersion, loadLicenses } from '../../licenses'
import EmptyState from '../lr/EmptyState.vue'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import Sheet from '../lr/Sheet.vue'

/**
 * Settings › About: this build's version and the licenses (the PC app's LICENSE and third-party notices, fetched from the
 * PC when the sheet opens, kept for the rest of the session).
 */
const open = ref(false)
const text = ref<string | null>(null)
const loading = ref(false)
const failed = ref(false)

async function load() {
  loading.value = true
  failed.value = false
  text.value = await loadLicenses()
  failed.value = text.value === null
  loading.value = false
}

watch(open, (isOpen) => {
  if (isOpen && text.value === null && !loading.value) void load()
})
</script>

<template>
  <ListSection>
    <ListRow title="Version" :icon="InfoIcon" tile-color="gray" :value="appVersion" />
    <ListRow title="Licenses" :icon="ScrollTextIcon" tile-color="gray" chevron tappable @click="open = true" />
  </ListSection>

  <Sheet v-model:open="open" modal :detents="['large']" title="Licenses">
    <EmptyState v-if="loading" busy title="Loading" />
    <EmptyState
      v-else-if="failed"
      :icon="CloudOffIcon"
      title="Couldn’t Load"
      message="Connect to your PC and try again."
      action-label="Try Again"
      @action="load"
    />
    <p v-else-if="text" class="footnote licenses">{{ text }}</p>
  </Sheet>
</template>

<style scoped>
.licenses {
  margin: 0;
  color: var(--label-secondary);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}
</style>
