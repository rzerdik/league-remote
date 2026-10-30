<script setup lang="ts">
import { PauseIcon, WandSparklesIcon } from '@lucide/vue'
import { computed } from 'vue'
import {
  autoPickEnabled,
  autoPickSettings,
  GROUP_INFO,
  resumeAutoPick,
  roleSummary,
  rolesFor,
  roleTitle,
  setAutoPickEnabled,
} from '../../autoPick'
import AutoPickPresetSheet from '../../components/autopick/AutoPickPresetSheet.vue'
import EmptyState from '../../components/lr/EmptyState.vue'
import LargeTitle from '../../components/lr/LargeTitle.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import LrButton from '../../components/lr/LrButton.vue'
import LrSwitch from '../../components/lr/LrSwitch.vue'
import StatusLine from '../../components/lr/StatusLine.vue'
import { useBusy } from '../../composables/useBusy'
import { AutoPickLockDelayMs, AutoPickQueueGroups, type AutoPickQueueGroup, type AutoPickRole } from '../../contracts'
import { tapFeedback } from '../../haptics'
import type { StatusTone } from '../../labels'
import { nav } from '../../navigation'
import { liveAutoPick, store } from '../../store'

/**
 * Settings › Auto Pick & Ban (a pushed view): the master switch, a pause that is still on, and one row per preset
 * (queue group × role) with what it sets; a row opens its editor sheet (`nav.autoPickEdit`). The presets live on the
 * PC (the connector runs the automation), so they show only while it is online.
 */
const toggle = useBusy()
const enabled = computed({
  get: () => autoPickEnabled.value,
  set: (on: boolean) => void toggle.run(() => setAutoPickEnabled(on)),
})

const paused = computed(() => !!liveAutoPick.value?.pausedForThisGame)
const resume = useBusy()
function onResume() {
  tapFeedback()
  void resume.run(resumeAutoPick)
}

const status = computed<{ tone: StatusTone; text: string }>(() => {
  if (!enabled.value) return { tone: 'gray', text: 'Off' }
  return paused.value ? { tone: 'orange', text: 'Paused for this game' } : { tone: 'green', text: 'On' }
})

const delay = AutoPickLockDelayMs / 1000
const intro = `When your turn comes, your PC hovers your first choice and locks it in ${delay} seconds later, unless you pick another champion or tap Cancel. It runs on your PC, even with your phone locked.`
const FALLBACK = 'Any Role is used when you have no role, and fills in whatever a role leaves empty.'
function footer(g: AutoPickQueueGroup) {
  return g === 'aram' ? GROUP_INFO[g].queues : `${GROUP_INFO[g].queues} ${FALLBACK}`
}

function edit(group: AutoPickQueueGroup, role: AutoPickRole) {
  tapFeedback()
  nav.autoPickEdit = { group, role }
}
</script>

<template>
  <div class="lr-content lr-swipe-follow">
    <LargeTitle title="Auto Pick & Ban">
      <StatusLine v-if="autoPickSettings" :tone="status.tone" :text="status.text" />
    </LargeTitle>

    <template v-if="!autoPickSettings">
      <EmptyState v-if="store.syncing.autoPick" busy title="Loading" />
      <EmptyState
        v-else
        :icon="WandSparklesIcon"
        title="Auto Pick Unavailable"
        message="Your PC hasn’t sent your auto pick settings yet."
      />
    </template>

    <template v-else>
      <ListSection :footer="intro">
        <ListRow title="Auto Pick & Ban" :icon="WandSparklesIcon" tile-color="orange" :busy="toggle.busy.value">
          <template #trailing>
            <LrSwitch v-if="!toggle.busy.value" v-model="enabled" aria-label="Auto Pick & Ban" />
          </template>
        </ListRow>
        <ListRow
          v-if="paused"
          title="Paused for This Game"
          subtitle="Nothing is automated until this champion select ends."
          :icon="PauseIcon"
          tile-color="gray"
          wrap
        >
          <template #trailing>
            <LrButton variant="gray" size="sm" :loading="resume.busy.value" :disabled="resume.busy.value" @click="onResume">
              Resume
            </LrButton>
          </template>
        </ListRow>
      </ListSection>

      <ListSection v-for="g in AutoPickQueueGroups" :key="g" :header="GROUP_INFO[g].title" :footer="footer(g)">
        <ListRow
          v-for="r in rolesFor(g)"
          :key="r"
          :title="roleTitle(r, g)"
          :subtitle="roleSummary(autoPickSettings, g, r)"
          wrap
          chevron
          tappable
          :aria-label="`${GROUP_INFO[g].title}, ${roleTitle(r, g)}: ${roleSummary(autoPickSettings, g, r)}`"
          @click="edit(g, r)"
        />
      </ListSection>
    </template>

    <AutoPickPresetSheet :target="autoPickSettings ? nav.autoPickEdit : null" @close="nav.autoPickEdit = null" />
  </div>
</template>
