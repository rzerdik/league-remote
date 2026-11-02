<script setup lang="ts">
import { CircleCheckIcon } from '@lucide/vue'
import { computed } from 'vue'
import { autoAcceptEnabled, setAutoAcceptEnabled } from '../../autoPick'
import { useBusy } from '../../composables/useBusy'
import ListRow from '../lr/ListRow.vue'
import LrSwitch from '../lr/LrSwitch.vue'

/**
 * The Auto Accept switch (Settings › Automation). The PC accepts the ready check by itself, independent of the auto pick master switch; `autoAcceptEnabled` shows the optimistic value until the echo.
 */
const toggle = useBusy()
const enabled = computed({
  get: () => autoAcceptEnabled.value,
  set: (on: boolean) => void toggle.run(() => setAutoAcceptEnabled(on)),
})
</script>

<template>
  <ListRow title="Auto Accept" :icon="CircleCheckIcon" tile-color="green" :busy="toggle.busy.value">
    <template #trailing>
      <LrSwitch v-if="!toggle.busy.value" v-model="enabled" aria-label="Auto Accept" />
    </template>
  </ListRow>
</template>
