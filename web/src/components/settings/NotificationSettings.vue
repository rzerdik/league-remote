<script setup lang="ts">
import { BellIcon } from '@lucide/vue'
import { computed } from 'vue'
import type { PushPreferences } from '../../contracts'
import { tapFeedback } from '../../haptics'
import { INSTALL_HINT, notifications } from '../../notifications'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrSwitch from '../lr/LrSwitch.vue'

/**
 * Settings › Notifications: push on/off for this phone, then (while on) which moments push and a test notification.
 * The choice is per phone and enforced by the PC (push.ts › setPushPreference).
 */
const status = notifications.pushStatus

const enabled = computed(() => status.value === 'enabled')
const notificationsOn = computed({
  get: () => enabled.value,
  set: (on: boolean) => void (on ? notifications.enable() : notifications.disable()),
})
const canToggle = computed(() => status.value === 'default' || status.value === 'enabled')
const footer = computed(() => {
  switch (status.value) {
    case 'needs-install':
      return INSTALL_HINT
    case 'denied':
      return 'Allow notifications for League Remote in your browser settings.'
    case 'unsupported':
      return 'This browser can’t show notifications.'
    case 'enabled':
      return 'Alerts reach this phone while League Remote is closed or in the background.'
    default:
      return 'Get alerted when a match is found, even with your phone locked.'
  }
})

/** The moments that can push, in the order they happen. Defaults: PushPreferences (Push.cs). */
const EVENTS: { key: keyof PushPreferences; title: string; subtitle?: string }[] = [
  { key: 'matchFound', title: 'Match Found' },
  { key: 'champSelectStarted', title: 'Champion Select', subtitle: 'When the draft starts' },
  { key: 'yourTurn', title: 'Your Turn', subtitle: 'To pick or ban' },
  { key: 'gameStarted', title: 'Game Started', subtitle: 'When the loading screen ends' },
  { key: 'lobbyInvite', title: 'Game Invites' },
  { key: 'friendRequest', title: 'Friend Requests' },
]

const setPreference = notifications.setPreference

function sendTest() {
  tapFeedback()
  void notifications.sendTest()
}
</script>

<template>
  <ListSection :footer="footer">
    <ListRow
      title="Notifications"
      :icon="BellIcon"
      tile-color="red"
      :value="canToggle ? undefined : notifications.statusLabel.value"
      :value-tone="status === 'denied' ? 'red' : undefined"
      :busy="notifications.busy.value"
    >
      <template #trailing>
        <LrSwitch v-if="canToggle && !notifications.busy.value" v-model="notificationsOn" aria-label="Notifications" />
      </template>
    </ListRow>
  </ListSection>

  <template v-if="enabled">
    <ListSection header="Alerts" footer="For this phone only. Nothing is sent while League Remote is open on screen.">
      <ListRow v-for="event in EVENTS" :key="event.key" :title="event.title" :subtitle="event.subtitle" wrap>
        <template #trailing>
          <LrSwitch
            :model-value="notifications.preferences[event.key]"
            :aria-label="event.title"
            @update:model-value="(on: boolean) => setPreference(event.key, on)"
          />
        </template>
      </ListRow>
    </ListSection>

    <ListSection>
      <ListRow title="Send Test Notification" variant="accent" tappable text :busy="notifications.testing.value" @click="sendTest" />
    </ListSection>
  </template>
</template>
