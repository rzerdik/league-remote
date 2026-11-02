<script setup lang="ts">
import { BellIcon, Gamepad2Icon, InfoIcon, MonitorIcon, ScanQrCodeIcon, VibrateIcon, Volume2Icon, WandSparklesIcon } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { autoPickAvailable, autoPickEnabled } from '../autoPick'
import AutoAcceptRow from '../components/autopick/AutoAcceptRow.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListRow from '../components/lr/ListRow.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrAlert from '../components/lr/LrAlert.vue'
import LrButton from '../components/lr/LrButton.vue'
import LrSwitch from '../components/lr/LrSwitch.vue'
import Sheet from '../components/lr/Sheet.vue'
import AboutSettings from '../components/settings/AboutSettings.vue'
import NotificationSettings from '../components/settings/NotificationSettings.vue'
import SoundSettings from '../components/settings/SoundSettings.vue'
import { clientStatus, pcStatus } from '../connection'
import { hapticSettings } from '../haptics'
import { lazy } from '../lib/lazy'
import { nav, openSettingsView } from '../navigation'
import { notifications } from '../notifications'
import { forgetPushPairing } from '../push'
import { unpairAndRevoke } from '../hub'
import { liveAutoPick, store } from '../store'
import PairingForm from './pair/PairingForm.vue'
import { usePairing } from './pair/usePairing'

/** Settings › Auto Pick & Ban: its own chunk (preset editor, champion lists), fetched when Settings opens. */
const AutoPickView = lazy(() => import('./settings/AutoPickView.vue'))
onMounted(() => AutoPickView.prefetch())

/**
 * Settings tab, short enough to nearly fit a screen: connection status first (what you check when something breaks),
 * automation (only once the PC sends its state), rows into the pushed views (Auto Pick & Ban, Notifications, Sounds &
 * Haptics, About), this phone. Pair Again is a sheet.
 */
const pairOpen = ref(false)

// ----- auto pick & ban -----
const autoPickValue = computed(() => {
  if (!autoPickEnabled.value) return 'Off'
  return liveAutoPick.value?.pausedForThisGame ? 'Paused' : 'On'
})

// ----- this phone -----
const pairing = usePairing(() => (pairOpen.value = false))
const unpairAlert = ref(false)
async function confirmUnpair() {
  await forgetPushPairing()
  await unpairAndRevoke()
}
</script>

<template>
  <AutoPickView v-if="nav.settingsView === 'autopick'" />

  <div v-else-if="nav.settingsView === 'notifications'" class="lr-content lr-swipe-follow">
    <LargeTitle title="Notifications" />
    <NotificationSettings />
  </div>

  <div v-else-if="nav.settingsView === 'sounds'" class="lr-content lr-swipe-follow">
    <LargeTitle title="Sounds & Haptics" />
    <ListSection>
      <ListRow title="Haptics" :icon="VibrateIcon" tile-color="gray">
        <template #trailing><LrSwitch v-model="hapticSettings.enabled" aria-label="Haptics" /></template>
      </ListRow>
    </ListSection>
    <SoundSettings />
  </div>

  <div v-else-if="nav.settingsView === 'about'" class="lr-content lr-swipe-follow">
    <LargeTitle title="About" />
    <AboutSettings />
  </div>

  <div v-else class="lr-content">
    <LargeTitle title="Settings" />

    <ListSection>
      <ListRow title="PC" :icon="MonitorIcon" :value="pcStatus.text" :value-tone="pcStatus.tone" />
      <ListRow title="League" :icon="Gamepad2Icon" :value="clientStatus.text" :value-tone="clientStatus.tone" />
    </ListSection>

    <ListSection v-if="autoPickAvailable" header="Automation" footer="Your PC does these, even with your phone locked.">
      <AutoAcceptRow />
      <ListRow
        title="Auto Pick & Ban"
        :icon="WandSparklesIcon"
        tile-color="orange"
        :value="autoPickValue"
        chevron
        tappable
        @click="openSettingsView('autopick')"
      />
    </ListSection>

    <ListSection>
      <ListRow
        title="Notifications"
        :icon="BellIcon"
        tile-color="red"
        :value="notifications.statusLabel.value"
        :value-tone="notifications.pushStatus.value === 'denied' ? 'red' : undefined"
        chevron
        tappable
        @click="openSettingsView('notifications')"
      />
      <ListRow
        title="Sounds & Haptics"
        :icon="Volume2Icon"
        tile-color="pink"
        chevron
        tappable
        @click="openSettingsView('sounds')"
      />
    </ListSection>

    <ListSection header="This Phone">
      <ListRow title="Pair Again…" tappable text @click="pairOpen = true" />
      <ListRow v-if="store.grant" title="Unpair This Phone" variant="danger" tappable text @click="unpairAlert = true" />
    </ListSection>

    <ListSection>
      <ListRow title="About" :icon="InfoIcon" tile-color="gray" chevron tappable @click="openSettingsView('about')" />
    </ListSection>

    <!-- Inside the one root the tab transition needs; the sheet renders in <body> (teleport). -->
    <Sheet v-model:open="pairOpen" modal :detents="['large']" title="Pair Again" body-class="lr-sheet-body--lists">
      <ListSection bare>
        <PairingForm :pairing="pairing" label="Scan the QR code on your PC or enter its code.">
          <LrButton variant="gray" size="lg" block :leading="ScanQrCodeIcon" :disabled="store.pairing" @click="pairing.openScanner">
            Scan QR Code
          </LrButton>
        </PairingForm>
      </ListSection>
    </Sheet>

    <LrAlert
      v-model:open="unpairAlert"
      title="Unpair This Phone?"
      message="You’ll need a new code to pair again."
      action-label="Unpair"
      destructive
      @action="confirmUnpair"
    />
  </div>
</template>

