<script setup lang="ts">
import { BellRingIcon } from '@lucide/vue'
import { computed } from 'vue'
import ConnectionEmptyState from '../components/ConnectionEmptyState.vue'
import ModeList from '../components/ModeList.vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListSection from '../components/lr/ListSection.vue'
import StatusLine from '../components/lr/StatusLine.vue'
import Tip from '../components/lr/Tip.vue'
import { connectionEmpty, connectionLine } from '../connection'
import { phaseLabel } from '../labels'
import { INSTALL_HINT, notifications } from '../notifications'
import { liveState, store } from '../store'

/**
 * Play: idle in the client, ready to start something. While something is missing (connection, PC, client) the mode
 * list is replaced by a content-unavailable view that says what to do (and the status line under the title steps
 * aside, so it isn't said twice). No action bar.
 */

const lcu = computed(() => !!liveState.value?.lcuConnected)
const phase = computed(() => liveState.value?.gameflowPhase ?? null)
/**
 * Only from the client's home screen ("None"). The post-game phases have their own screen on Home (Play Again, Leave:
 * the connector only takes those in EndOfGame), so a new mode is chosen after Leave, or in the lobby after Play Again.
 */
const canCreateLobby = computed(() => lcu.value && phase.value === 'None')

/** League-side reasons the mode list can't show yet; the connection's own come first (ConnectionEmptyState). */
const empty = computed(() => {
  if (connectionEmpty.value) return null
  if (!liveState.value && store.syncing.state) return { busy: true, title: 'Checking League…' }
  if (!lcu.value) return { busy: true, title: 'Waiting for League', message: 'Open the League client.' }
  return null
})
/** The status line under the title only says what no empty state says already (Ready, In Queue, …). */
const unavailable = computed(() => !!connectionEmpty.value || !!empty.value)

</script>

<template>
  <div class="lr-content">
    <LargeTitle title="Play">
      <template v-if="!unavailable" #default>
        <StatusLine
          :tone="connectionLine.tone"
          :pulse="connectionLine.pulse"
          :busy="connectionLine.busy"
          :text="lcu && !canCreateLobby && phase ? phaseLabel(phase) : connectionLine.text"
        />
      </template>
    </LargeTitle>

    <ConnectionEmptyState />
    <EmptyState v-if="empty" :busy="empty.busy" :title="empty.title" :message="empty.message" />

    <template v-else-if="!connectionEmpty">
      <ListSection v-if="canCreateLobby && notifications.showHint.value" bare>
        <Tip
          :icon="BellRingIcon"
          title="Notifications"
          :message="
            notifications.pushStatus.value === 'needs-install' ? INSTALL_HINT : 'Match found, your turn, game start.'
          "
          :action-label="notifications.pushStatus.value === 'default' ? 'Turn On Notifications' : undefined"
          :action-busy="notifications.busy.value"
          @action="notifications.enable"
          @dismiss="notifications.dismissHint"
        />
      </ListSection>

      <ModeList v-if="canCreateLobby" mode="start" />
    </template>
  </div>
</template>
