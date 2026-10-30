<script setup lang="ts">
import { BugIcon, CastleIcon, CrownIcon, EyeIcon, FlameIcon } from '@lucide/vue'
import { computed, type Component } from 'vue'
import type { TileColor } from '../../components/lr/IconTile.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatusLine from '../../components/lr/StatusLine.vue'
import TimerText from '../../components/lr/TimerText.vue'
import type { LiveObjectiveTimer, LiveObjectiveTimers } from '../../contracts'
import { formatSeconds } from '../../format'
import { inhibitorTitle, objectiveStatus, objectiveTitle, takenBy, type ObjectiveStatus } from '../../liveGame/objectives'

/**
 * Summoner's Rift objective timers on the interpolated game clock: dragon (its element once known, then the Elder),
 * Voidgrubs, Rift Herald and Baron count down to their spawn, say "Up" (with when an untaken Herald or grub camp
 * leaves), or "Taken" / "Gone" once they won't come back (those rows move to the end); destroyed inhibitors count
 * down to their respawn. The timers come from the game's events, so a spawn is inferred, never seen.
 */
const props = defineProps<{ timers: LiveObjectiveTimers; clock: number }>()

const STYLE: Record<string, { icon: Component; tile: TileColor }> = {
  dragon: { icon: FlameIcon, tile: 'orange' },
  elder: { icon: FlameIcon, tile: 'indigo' },
  grubs: { icon: BugIcon, tile: 'purple' },
  herald: { icon: EyeIcon, tile: 'cyan' },
  baron: { icon: CrownIcon, tile: 'purple' },
}

interface Row {
  key: string
  title: string
  icon: Component
  tile: TileColor
  status: ObjectiveStatus
  subtitle: string
}

function subtitle(t: LiveObjectiveTimer, s: ObjectiveStatus): string {
  if (s.state === 'soon') return `Spawns at ${formatSeconds(s.at)}`
  if (s.state === 'up') return s.until != null ? `Leaves at ${formatSeconds(s.until)}` : ''
  return s.taken ? takenBy(t.kind) : 'Not taken'
}

const rows = computed<Row[]>(() => {
  const t = props.timers
  const list = [t.dragon, t.grubs, t.herald, t.baron].map((timer) => {
    const status = objectiveStatus(timer, props.clock)
    const style = STYLE[timer.kind] ?? STYLE.dragon!
    return { key: timer.kind, title: objectiveTitle(timer), icon: style.icon, tile: style.tile, status, subtitle: subtitle(timer, status) }
  })
  // Done for the game: after the ones still to come.
  return [...list.filter((r) => r.status.state !== 'done'), ...list.filter((r) => r.status.state === 'done')]
})

const inhibitors = computed(() =>
  props.timers.inhibitors.map((i) => ({
    key: `${i.side}-${i.lane}`,
    title: inhibitorTitle(i),
    seconds: Math.max(0, i.respawnsAt - props.clock),
    subtitle: `Respawns at ${formatSeconds(i.respawnsAt)}`,
  })),
)
</script>

<template>
  <ListSection header="Objectives">
    <ListRow v-for="r in rows" :key="r.key" wrap :icon="r.icon" :tile-color="r.tile" :title="r.title" :subtitle="r.subtitle || undefined">
      <template #trailing>
        <TimerText v-if="r.status.state === 'soon'" class="body" :seconds="r.status.seconds" />
        <StatusLine v-else-if="r.status.state === 'up'" class="body" tone="green" text="Up" />
        <span v-else class="body lr-secondary">{{ r.status.taken ? 'Taken' : 'Gone' }}</span>
      </template>
    </ListRow>
    <ListRow v-for="i in inhibitors" :key="i.key" wrap :icon="CastleIcon" tile-color="dark" :title="i.title" :subtitle="i.subtitle">
      <template #trailing>
        <TimerText class="body" :seconds="i.seconds" />
      </template>
    </ListRow>
  </ListSection>
</template>
