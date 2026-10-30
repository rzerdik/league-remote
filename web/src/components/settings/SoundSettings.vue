<script setup lang="ts">
import { AudioLinesIcon, MusicIcon, Volume2Icon } from '@lucide/vue'
import { computed } from 'vue'
import { play, SOUND_CATEGORIES, soundSettings, type SoundCategory, type SoundKey } from '../../sound'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrSlider from '../lr/LrSlider.vue'
import LrSwitch from '../lr/LrSwitch.vue'

/**
 * Settings › sounds and music: the League client's sound effects (switch, volume, one switch per kind of moment) and
 * its music and ambience loops (switch, volume), and whether they go on in the background (`sound/session.ts`).
 * Everything persists in `soundSettings`.
 */

const soundsOn = computed({
  get: () => !soundSettings.muted,
  set: (on: boolean) => (soundSettings.muted = !on),
})
const volume = computed({
  get: () => Math.round(soundSettings.volume * 100),
  set: (v: number) => {
    soundSettings.volume = Math.min(1, Math.max(0, v / 100))
    if (v > 0) soundSettings.muted = false
  },
})
const musicVolume = computed({
  get: () => Math.round(soundSettings.musicVolume * 100),
  set: (v: number) => {
    soundSettings.musicVolume = Math.min(1, Math.max(0, v / 100))
    if (v > 0) soundSettings.music = true
  },
})

/** A preview sound at most this often while the slider is dragged (ms). */
const PREVIEW_EVERY_MS = 180
let previewAt = 0
function preview() {
  if (Date.now() - previewAt < PREVIEW_EVERY_MS) return
  previewAt = Date.now()
  play('hover', { volume: 2 })
}

/** What a category sounds like, played when it is switched on. */
const SAMPLE: Record<SoundCategory, SoundKey> = {
  readyCheck: 'readyAccept',
  champSelect: 'lockIn',
  countdown: 'timerTick',
  party: 'memberJoined',
  social: 'chatMessage',
  taps: 'click',
}

function category(id: SoundCategory) {
  return computed({
    get: () => soundSettings.categories[id],
    set: (on: boolean) => {
      soundSettings.categories[id] = on
      if (on) play(SAMPLE[id])
    },
  })
}
const categories = SOUND_CATEGORIES.map((c) => ({ ...c, model: category(c.id) }))
</script>

<template>
  <ListSection header="Sound and Music" footer="Your League client’s own sounds and music.">
    <ListRow title="Sounds" :icon="Volume2Icon" tile-color="pink">
      <template #trailing><LrSwitch v-model="soundsOn" aria-label="Sounds" /></template>
    </ListRow>
    <ListRow>
      <LrSlider v-model="volume" label="Sound Volume" :muted="soundSettings.muted" @input="preview" @change="play('yourTurn')" />
    </ListRow>
    <ListRow title="Music" :icon="MusicIcon" tile-color="purple">
      <template #trailing><LrSwitch v-model="soundSettings.music" aria-label="Music" /></template>
    </ListRow>
    <ListRow>
      <LrSlider v-model="musicVolume" label="Music Volume" :muted="!soundSettings.music" />
    </ListRow>
  </ListSection>

  <ListSection
    footer="Sounds and music go on with the app in the background or your phone locked, until you close it. On iPhone, other apps’ audio pauses while you’re connected."
  >
    <ListRow title="Play in Background" :icon="AudioLinesIcon" tile-color="indigo">
      <template #trailing><LrSwitch v-model="soundSettings.background" aria-label="Play in Background" /></template>
    </ListRow>
  </ListSection>

  <ListSection header="Sound Effects">
    <ListRow v-for="c in categories" :key="c.id" :title="c.title" :disabled="soundSettings.muted">
      <template #trailing>
        <LrSwitch v-model="c.model.value" :aria-label="`${c.title} Sounds`" :disabled="soundSettings.muted" />
      </template>
    </ListRow>
  </ListSection>
</template>
