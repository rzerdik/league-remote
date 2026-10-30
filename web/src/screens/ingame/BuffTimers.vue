<script setup lang="ts">
import { computed } from 'vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatusLine from '../../components/lr/StatusLine.vue'
import TimerText from '../../components/lr/TimerText.vue'
import { formatSeconds } from '../../format'
import { haptic } from '../../haptics'
import { knowsMyTeam } from '../../liveGame'
import { BUFF_RESPAWN_S, buffLeft, buffList, toggleBuff, type BuffKey } from '../../liveGame/buffs'

/**
 * Hand-started buff timers (the game's API has no jungle camps): two small wells per team, under the team's name —
 * Your Team and Enemy (spectating: Blue Side and Red Side) — each its Blue Buff and Red Buff. An idle well reads
 * "Start Timer" in the accent (it is a button); tap when a buff is taken: it counts down 5:00 on the game clock,
 * then says "Up". Tap a running timer to clear it. Local to this phone; cleared when the game ends.
 */
function tile(key: BuffKey, short: string) {
  const b = buffList.value.find((x) => x.key === key)!
  const left = buffLeft(key)
  const state = left == null ? 'idle' : left > 0 ? 'running' : 'up'
  const aria =
    state === 'running'
      ? `Clear the timer for ${b.spoken}, ${formatSeconds(left ?? 0)} left`
      : `Start a ${formatSeconds(BUFF_RESPAWN_S)} timer for ${b.spoken}${state === 'up' ? ', it’s up' : ''}`
  return { key, short, left: left ?? 0, state, aria }
}

const groups = computed(() => [
  {
    title: knowsMyTeam.value ? 'Your Team' : 'Blue Side',
    tiles: [tile('myBlue', 'Blue Buff'), tile('myRed', 'Red Buff')],
  },
  {
    title: knowsMyTeam.value ? 'Enemy' : 'Red Side',
    tiles: [tile('enemyBlue', 'Blue Buff'), tile('enemyRed', 'Red Buff')],
  },
])

function tap(key: BuffKey) {
  haptic('selection')
  toggleBuff(key)
}
</script>

<template>
  <ListSection header="Buffs" footer="Tap when a buff is taken to time its respawn. Tap again to clear it.">
    <li class="buffs">
      <div v-for="g in groups" :key="g.title" class="buff-group" role="group" :aria-label="g.title">
        <span class="footnote lr-secondary buff-side" aria-hidden="true">{{ g.title }}</span>
        <div class="buff-wells">
          <button
            v-for="t in g.tiles"
            :key="t.key"
            type="button"
            class="buff"
            :class="{ 'is-running': t.state === 'running' }"
            :aria-label="t.aria"
            @click="tap(t.key)"
          >
            <span class="subheadline buff-name">{{ t.short }}</span>
            <TimerText v-if="t.state === 'running'" class="headline" :seconds="t.left" />
            <StatusLine v-else-if="t.state === 'up'" class="headline" tone="green" text="Up" />
            <span v-else class="headline lr-accent-text">Start Timer</span>
          </button>
        </div>
      </div>
    </li>
  </ListSection>
</template>

<style scoped>
.buffs {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-3);
}
.buff-side {
  display: block;
  padding: 0 var(--space-2) var(--space-1);
}
/* Two wells side by side; one column at large text sizes (a column is about seven characters of the label wide). */
.buff-wells {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, calc(var(--font-size-subheadline) * 7)), 1fr));
  gap: var(--space-2);
}
/* Concentric with the card: radius-card minus the padding. */
.buff {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  justify-content: center;
  gap: var(--space-0-5);
  min-height: var(--control-lg);
  padding: var(--space-2) var(--space-4);
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--bg-tertiary);
  color: var(--label);
  text-align: left;
  cursor: pointer;
  transition: background-color var(--dur-fade) var(--ease-fade);
}
.buff:active {
  background: var(--row-pressed);
  transition-duration: 0ms;
}
.buff-name {
  color: var(--label-secondary);
  overflow-wrap: break-word;
}
.buff.is-running .buff-name {
  color: var(--label);
}
</style>
