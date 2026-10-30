<script setup lang="ts">
import { watch } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import LrAlert from '../../components/lr/LrAlert.vue'
import { useBusy } from '../../composables/useBusy'
import { tapFeedback } from '../../haptics'
import { sendCommand } from '../../hub'

/**
 * Dodge, as a destructive row behind an alert: it ends the draft for all ten players and League penalises it. Only
 * where League takes it (`canDodge`: not in custom games, not once the game is starting); the screen changes when the
 * next state arrives, never on the command's success alone.
 */
const props = defineProps<{ canDodge: boolean }>()
/** The confirmation is up (the picker steps aside meanwhile). */
const alertOpen = defineModel<boolean>('alert', { default: false })

const dodge = useBusy()
watch(
  () => props.canDodge,
  (can) => {
    if (!can) alertOpen.value = false
  },
)

function ask() {
  tapFeedback()
  alertOpen.value = true
}
function confirm() {
  tapFeedback()
  void dodge.run(() => sendCommand({ type: 'DodgeChampSelect' }))
}
</script>

<template>
  <ListSection v-if="canDodge" footer="Ends champion select for everyone.">
    <ListRow title="Dodge Game" variant="danger" tappable text :busy="dodge.busy.value" @click="ask" />
  </ListSection>

  <LrAlert
    v-model:open="alertOpen"
    title="Dodge This Game?"
    message="Everyone goes back to their lobby. League locks you out of the queue for a while, and in ranked you lose LP."
    action-label="Dodge"
    destructive
    @action="confirm"
  />
</template>
