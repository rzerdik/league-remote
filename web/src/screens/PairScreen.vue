<script setup lang="ts">
import { Gamepad2Icon, MonitorIcon, ScanQrCodeIcon } from '@lucide/vue'
import AppMark from '../components/AppMark.vue'
import ActionBar from '../components/lr/ActionBar.vue'
import LrButton from '../components/lr/LrButton.vue'
import { store } from '../store'
import PairingForm from './pair/PairingForm.vue'
import { usePairing } from './pair/usePairing'

/** First run: onboarding layout that pairs the phone with the connector. One action: Scan QR Code. */
const pairing = usePairing()

const FEATURES = [
  { icon: MonitorIcon, title: 'Run League Remote on your PC' },
  { icon: ScanQrCodeIcon, title: 'Scan its QR code' },
  { icon: Gamepad2Icon, title: 'Play from your phone' },
]
</script>

<template>
  <div class="lr-content lr-onboarding">
    <header class="lr-onboarding-head">
      <div class="lr-appmark"><AppMark /></div>
      <h1 class="title-1">Pair with Your PC</h1>
    </header>

    <ol class="lr-features">
      <li v-for="f in FEATURES" :key="f.title" class="lr-feature">
        <component :is="f.icon" :stroke-width="1.6" aria-hidden="true" />
        <span class="headline">{{ f.title }}</span>
      </li>
    </ol>

    <PairingForm class="lr-onboarding-form" :pairing="pairing" label="Or enter the code" />

    <ActionBar>
      <LrButton variant="prominent" size="lg" :leading="ScanQrCodeIcon" :disabled="store.pairing" @click="pairing.openScanner">
        Scan QR Code
      </LrButton>
    </ActionBar>
  </div>
</template>
