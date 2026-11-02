<script setup lang="ts">
import { EllipsisIcon } from '@lucide/vue'
import { computed, watch } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import LrAlert from '../../components/lr/LrAlert.vue'
import LrButton from '../../components/lr/LrButton.vue'
import LrMenu, { type MenuItem } from '../../components/lr/LrMenu.vue'
import { useBusy } from '../../composables/useBusy'
import { tapFeedback } from '../../haptics'
import { sendCommand } from '../../hub'

/**
 * Dodge, as a destructive row behind an alert: it ends the draft for all ten players and League penalises it. Only
 * where League takes it (`canDodge`: not in custom games, not once the game is starting); the screen changes when the
 * next state arrives, never on the command's success alone. `inNav` (the draft board, which keeps the screen for the
 * draft): a More Options menu in the nav bar instead of a row; `navHidden` takes it away while a view is pushed over it.
 */
const props = defineProps<{ canDodge: boolean; inNav?: boolean; navHidden?: boolean }>()
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
const menu = computed<MenuItem[]>(() => [{ label: 'Dodge Game', destructive: true, disabled: dodge.busy.value, onSelect: ask }])
</script>

<template>
  <Teleport v-if="inNav" defer to="#lr-nav-leading">
    <LrMenu v-if="canDodge && !navHidden" :items="menu" align="start">
      <LrButton variant="glass" :icon="EllipsisIcon" aria-label="More Options" />
    </LrMenu>
  </Teleport>
  <ListSection v-else-if="canDodge" footer="Ends champion select for everyone.">
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
