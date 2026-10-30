<script setup lang="ts">
import { CircleAlertIcon, CircleDotIcon, PauseIcon, SparklesIcon, WandSparklesIcon } from '@lucide/vue'
import { computed, type Component } from 'vue'
import { autoPickRun, logKey, pauseAutoPick, presetChoices, resumeAutoPick, roleTitle, runPreset } from '../../autoPick'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import IconTile, { type TileColor } from '../../components/lr/IconTile.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import LrButton from '../../components/lr/LrButton.vue'
import { useBusy } from '../../composables/useBusy'
import type { AutoPickLogEntry } from '../../contracts'
import { tapFeedback } from '../../haptics'

/**
 * Champion Select › Auto Pick: what the PC's automation does in this draft — on (for which role, with which preset),
 * paused for this game, or no preset for your role — with Pause / Resume, and its last few log lines. Hidden when it
 * is switched off or the queue isn't automated (status 'disabled' / 'unsupported'): nothing to say then.
 */
const run = autoPickRun
const visible = computed(() => !!run.value && (run.value.status === 'active' || run.value.status === 'paused' || run.value.status === 'no-preset'))

const role = computed(() => (run.value?.role ? roleTitle(run.value.role, run.value.queueGroup) : ''))
const title = computed(() => {
  switch (run.value?.status) {
    case 'paused':
      return 'Paused for This Game'
    case 'no-preset':
      return run.value.role && run.value.role !== 'ANY' ? `No Preset for ${role.value}` : 'No Preset'
    default:
      return run.value?.role && run.value.role !== 'ANY' ? `On for ${role.value}` : 'On'
  }
})
const subtitle = computed(() => {
  switch (run.value?.status) {
    case 'paused':
      return 'Resume to let your PC ban and pick again.'
    case 'no-preset':
      return 'Set one in Settings › Auto Pick & Ban.'
    default:
      return (runPreset.value && presetChoices(runPreset.value)) || undefined
  }
})

/** The newest lines first; the full log is at most AutoPickMaxLogEntries. */
const RECENT = 3
const recent = computed(() => (run.value?.log ?? []).slice(-RECENT).reverse())

const LOG_TILE: Partial<Record<AutoPickLogEntry['kind'], { icon: Component; color: TileColor }>> = {
  spells: { icon: SparklesIcon, color: 'purple' },
  'rune-page': { icon: CircleDotIcon, color: 'indigo' },
  skipped: { icon: CircleAlertIcon, color: 'orange' },
  failed: { icon: CircleAlertIcon, color: 'red' },
}
const tileFor = (e: AutoPickLogEntry) => LOG_TILE[e.kind] ?? { icon: WandSparklesIcon, color: 'orange' as TileColor }

const toggle = useBusy()
function onToggle() {
  tapFeedback()
  void toggle.run(run.value?.status === 'paused' ? resumeAutoPick : pauseAutoPick)
}
</script>

<template>
  <ListSection v-if="visible && run" header="Auto Pick">
    <ListRow
      :title="title"
      :subtitle="subtitle"
      :icon="run.status === 'paused' ? PauseIcon : WandSparklesIcon"
      :tile-color="run.status === 'active' ? 'orange' : 'gray'"
      wrap
    >
      <template v-if="run.status !== 'no-preset'" #trailing>
        <LrButton
          variant="gray"
          size="sm"
          :loading="toggle.busy.value"
          :disabled="toggle.busy.value"
          :aria-label="run.status === 'paused' ? 'Resume Auto Pick' : 'Pause Auto Pick for This Game'"
          @click="onToggle"
        >
          {{ run.status === 'paused' ? 'Resume' : 'Pause' }}
        </LrButton>
      </template>
    </ListRow>
    <ListRow v-for="e in recent" :key="logKey(e)" :title="e.text" wrap>
      <template #lead>
        <ChampionIcon v-if="e.championId > 0" :champion-id="e.championId" size="tile" decorative />
        <IconTile v-else :icon="tileFor(e).icon" :color="tileFor(e).color" />
      </template>
    </ListRow>
  </ListSection>
</template>
