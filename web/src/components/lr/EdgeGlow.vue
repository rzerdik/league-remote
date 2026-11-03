<script setup lang="ts">
import { computed, ref, watch } from 'vue'

/**
 * The final seconds of your own turn: a heartbeat glow from the left and right screen edges only, strongest at the
 * vertical middle and fading out towards the top and bottom (the status bar / Dynamic Island and the Liquid Glass bars
 * stay clear). Gold on your pick (the Lock In colour), red on your ban.
 *
 * `beat` is the whole second left (from `useFinalSeconds`, counting down to 1; 0 = hidden). Each new value restarts one
 * lub-dub pulse, so it lands with the countdown tick sound. The glow grows from the first beat to the last: the window
 * isn't assumed, it is whatever the first beat was. Teleported to `<body>` so no transformed ancestor can clip it. It
 * lies under sheets: while it shows, the champion picker thins its glass (`sheets.css`) so the light comes through.
 * Decorative (`aria-hidden`, no touches): the timer next to it already says the time; under Reduce Motion it holds
 * still.
 */
const props = defineProps<{ beat: number; tone: 'pick' | 'ban' }>()

/** The first beat of this run of final seconds (the window's length). */
const firstBeat = ref(0)
watch(
  () => props.beat,
  (b, prev) => {
    if (b > 0 && (!prev || b > firstBeat.value)) firstBeat.value = b
  },
  { immediate: true },
)

/** 0 on the first beat → 1 on the last (beat 1): drives intensity and reach. */
const level = computed(() => {
  const first = firstBeat.value
  if (props.beat <= 0 || first <= 1) return 1
  return Math.min(1, Math.max(0, (first - props.beat) / (first - 1)))
})
</script>

<template>
  <Teleport to="body">
    <Transition name="lr-edge-glow">
      <div
        v-if="beat > 0"
        class="lr-edge-glow"
        :class="`lr-edge-glow--${tone}`"
        :style="{ '--lr-glow-level': level }"
        aria-hidden="true"
      >
        <div :key="beat" class="lr-edge-glow-beat">
          <span class="lr-edge-glow-side lr-edge-glow-side--left" />
          <span class="lr-edge-glow-side lr-edge-glow-side--right" />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
