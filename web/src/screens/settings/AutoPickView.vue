<script setup lang="ts">
import { PauseIcon, WandSparklesIcon } from '@lucide/vue'
import { computed } from 'vue'
import {
  autoPickEnabled,
  autoPickSettings,
  findPreset,
  GROUP_INFO,
  resumeAutoPick,
  roleSummaryLines,
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
import type { MenuItem } from '../../components/lr/LrMenu.vue'
import LrSwitch from '../../components/lr/LrSwitch.vue'
import { useBusy } from '../../composables/useBusy'
import { AutoPickQueueGroups, type AutoPickQueueGroup, type AutoPickRole } from '../../contracts'
import { tapFeedback } from '../../haptics'
import { nav } from '../../navigation'
import { liveAutoPick, store } from '../../store'

/**
 * Settings › Auto Pick & Ban (a pushed view): the master switch, a pause that is still on, and per queue
 * group its Any Role row plus the roles with their own preset, each with what it sets; a row opens its editor sheet
 * (`nav.autoPickEdit`), and Add Role… opens it for a role without a preset. The presets live on the PC (the connector
 * runs the automation), so they show only while it is online.
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

const intro = 'Hovers your first choice and locks it in during your turn’s last second, unless you pick or tap Cancel.'
const FALLBACK = 'Any Role is used when you have no role, and fills in whatever a role leaves empty.'

/** Per group: the rows to show (Any Role always, a role once it has a preset) and the Add Role… menu for the rest. */
const groups = computed(() =>
  AutoPickQueueGroups.map((id) => {
    const shown = (r: AutoPickRole) => r === 'ANY' || !!findPreset(autoPickSettings.value, id, r)
    const roles = rolesFor(id)
    return {
      id,
      roles: roles.filter(shown),
      // The menu gives the selection haptic itself. A preset left empty in the editor just doesn't appear.
      addItems: roles
        .filter((r) => !shown(r))
        .map((role): MenuItem => ({ label: roleTitle(role, id), onSelect: () => (nav.autoPickEdit = { group: id, role }) })),
    }
  }),
)

function edit(group: AutoPickQueueGroup, role: AutoPickRole) {
  tapFeedback()
  nav.autoPickEdit = { group, role }
}
</script>

<template>
  <div class="lr-content lr-swipe-follow">
    <LargeTitle title="Auto Pick & Ban" />

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
        <ListRow title="Enabled" :busy="toggle.busy.value">
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

      <ListSection v-for="g in groups" :key="g.id" :header="GROUP_INFO[g.id].title" :footer="GROUP_INFO[g.id].queues">
        <ListRow
          v-for="r in g.roles"
          :key="r"
          :title="roleTitle(r, g.id)"
          wrap
          chevron
          tappable
          :aria-label="`${GROUP_INFO[g.id].title}, ${roleTitle(r, g.id)}: ${roleSummaryLines(autoPickSettings, g.id, r).join(', ')}`"
          @click="edit(g.id, r)"
        >
          <template #subtitle>
            <span v-for="line in roleSummaryLines(autoPickSettings, g.id, r)" :key="line" class="lr-row-line">{{ line }}</span>
          </template>
        </ListRow>
        <ListRow
          v-if="g.addItems.length"
          title="Add Role…"
          text
          :menu="g.addItems"
          menu-title="Add Role"
          :aria-label="`Add Role to ${GROUP_INFO[g.id].title}`"
        />
      </ListSection>

      <ListSection bare :footer="FALLBACK" />
    </template>

    <AutoPickPresetSheet :target="autoPickSettings ? nav.autoPickEdit : null" @close="nav.autoPickEdit = null" />
  </div>
</template>
