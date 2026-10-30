<script setup lang="ts">
import { computed, ref } from 'vue'
import { secondsSinceReading } from '../clock'
import CallButton from '../components/lr/CallButton.vue'
import CountdownRing from '../components/lr/CountdownRing.vue'
import { useBusy } from '../composables/useBusy'
import { useModalLayer } from '../composables/useModalLayer'
import type { ReadyCheckState } from '../contracts'
import { tapFeedback } from '../haptics'
import { queueLabel } from '../labels'
import { sendCommand } from '../hub'

/**
 * The Ready Check cover, laid out like an incoming call: countdown ring, Decline (leading) and Accept (trailing).
 * The one place where Accept is green. A modal layer: everything but banners is inert, Accept has focus and Tab stays
 * on the cover (and banners); Decline is the way out (no close button). Leaves by cross-fading once the client has the answer.
 */
const props = defineProps<{ readyCheck: ReadyCheckState; queueId: number | null }>()
/** League's ready check lasts 12 s. */
const DURATION_S = 12

const { busy, run } = useBusy()
const elapsed = computed(() =>
  Math.min(DURATION_S, props.readyCheck.timer + secondsSinceReading('readycheck', props.readyCheck.timer)),
)

const subtitle = computed(() => queueLabel(props.queueId))

function accept() {
  tapFeedback('success')
  void run(() => sendCommand({ type: 'AcceptReadyCheck' }))
}
function decline() {
  tapFeedback()
  void run(() => sendCommand({ type: 'DeclineReadyCheck' }))
}

const root = ref<HTMLElement | null>(null)
const acceptButton = ref<InstanceType<typeof CallButton> | null>(null)
const acceptEl = computed(() => (acceptButton.value?.$el as HTMLElement | undefined) ?? null)
useModalLayer(root, acceptEl)
</script>

<template>
  <div
    ref="root"
    class="lr-cover"
    role="alertdialog"
    aria-modal="true"
    aria-labelledby="rc-title"
    :aria-describedby="subtitle ? 'rc-desc' : undefined"
    tabindex="-1"
  >
    <p class="lr-cover-eyebrow subheadline">Ready Check</p>
    <h1 id="rc-title" class="large-title">Match Found</h1>
    <p v-if="subtitle" id="rc-desc" class="lr-cover-subtitle body">{{ subtitle }}</p>

    <div class="lr-cover-hero">
      <CountdownRing :remaining="DURATION_S - elapsed" :total="DURATION_S" />
    </div>

    <div class="lr-call">
      <CallButton kind="decline" label="Decline" :disabled="busy" @click="decline" />
      <CallButton ref="acceptButton" kind="accept" label="Accept" :disabled="busy" @click="accept" />
    </div>
  </div>
</template>
