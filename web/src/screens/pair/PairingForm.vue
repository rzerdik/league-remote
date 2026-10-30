<script setup lang="ts">
import { ScanQrCodeIcon } from '@lucide/vue'
import { onMounted, useId } from 'vue'
import CodeField from '../../components/lr/CodeField.vue'
import StatusLine from '../../components/lr/StatusLine.vue'
import Tip from '../../components/lr/Tip.vue'
import { linkStatus } from '../../connection'
import { lazy } from '../../lib/lazy'
import { store, toast } from '../../store'
import type { Pairing } from './usePairing'

/**
 * The pairing code part of pairing, shared by the Pair screen and Settings › Pair Again: a label, the 6-digit code
 * (or "Pairing…" while a code is being redeemed), a status line when the PC isn't reachable, the Camera-app tip
 * on plain http, and the QR scanner cover. The Scan QR Code button is the parent's: in the action bar (Pair screen)
 * or in the default slot, under the code (Settings › Pair Again).
 */
const props = defineProps<{ pairing: Pairing; label: string }>()
const labelId = useId()

/** The camera cover is its own chunk, fetched as soon as the form shows (before anyone taps Scan QR Code). */
const QrScanner = lazy(() => import('../../components/QrScanner.vue'), {
  loading: null,
  failed: null,
  // Nothing to show on a cover that couldn't load (or took longer than lazy.ts's bound): say so and close it, so Scan
  // QR Code can be tapped again.
  onFailed: () => {
    props.pairing.scanning.value = false
    toast('Couldn’t load the scanner. Try again.', 'warning')
  },
})
onMounted(() => QrScanner.prefetch())
</script>

<template>
  <section class="pair-form" :aria-labelledby="labelId">
    <Tip
      v-if="props.pairing.cameraUnavailable.value"
      class="pair-form-tip"
      :icon="ScanQrCodeIcon"
      title="Use the Camera App"
      message="Scan the QR code with your phone's Camera app."
      @dismiss="props.pairing.cameraUnavailable.value = false"
    />
    <p :id="labelId" class="pair-form-label subheadline">{{ label }}</p>
    <StatusLine v-if="store.pairing" class="pair-form-status headline" busy text="Pairing…" role="status" />
    <template v-else>
      <CodeField
        v-model="props.pairing.code.value"
        :error-key="props.pairing.errorKey.value"
        @complete="props.pairing.submit"
      />
      <!-- Only when something is wrong: a healthy connection needs no line. -->
      <StatusLine
        v-if="linkStatus.tone !== 'green'"
        class="pair-form-status footnote"
        role="status"
        :tone="linkStatus.tone"
        :busy="linkStatus.tone === 'orange'"
        :text="linkStatus.tone === 'red' ? 'Offline' : 'Connecting…'"
      />
    </template>
    <div v-if="$slots.default" class="pair-form-actions"><slot /></div>

    <!-- A cover in <body>: a modal layer over everything but banners. -->
    <Teleport to="body">
      <Transition name="lr-cover">
        <QrScanner
          v-if="props.pairing.scanning.value"
          @close="props.pairing.scanning.value = false"
          @code="props.pairing.onScanned"
        />
      </Transition>
    </Teleport>
  </section>
</template>

<style scoped>
.pair-form {
  display: flex;
  flex-direction: column;
  align-items: stretch;
}
.pair-form-actions {
  margin-top: var(--space-6);
}
.pair-form-tip {
  margin-bottom: var(--space-6);
}
.pair-form-label {
  text-align: center;
  color: var(--label-secondary);
  margin-bottom: var(--space-3);
}
/* "Pairing…" stands where the code boxes were, at their height, so nothing jumps. */
.pair-form-status {
  align-self: center;
  justify-content: center;
  margin-top: var(--space-3);
  color: var(--label-secondary);
}
.pair-form-status.headline {
  min-height: var(--code-box);
  margin-top: 0;
  color: var(--label);
}
</style>
