<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import TimerText from '../../components/lr/TimerText.vue'
import LoadoutStrip from '../../components/profile/LoadoutStrip.vue'
import type { LivePlayer } from '../../contracts'
import { kda, kdaSpoken } from '../../format'
import { champName, liveHasTrinkets, loadoutOf, playerLabel, respawnLeft } from '../../liveGame'

/**
 * One scoreboard line of the running game, compact enough for 375 px: name and K / D / A; champion, level and CS;
 * spells, keystone and items; while dead a gray portrait and "Respawns in 0:12". Your own team by game name (champion
 * on the second line), enemies by champion only — the connector never sends their names. Your row is highlighted.
 */
const props = defineProps<{ player: LivePlayer }>()

const named = computed(() => !!props.player.gameName)
const detail = computed(() =>
  [named.value ? champName(props.player) : '', `Level ${props.player.level}`, `${props.player.creepScore} CS`]
    .filter(Boolean)
    .join(' · '),
)
const respawn = computed(() => respawnLeft(props.player))
</script>

<template>
  <ListRow :variant="player.isMe ? 'you' : 'default'">
    <template #lead>
      <ChampionIcon :champion-id="player.championId" :state="player.isDead ? 'dead' : player.isMe ? 'you' : null" decorative />
    </template>
    <span class="live-player">
      <span class="live-player-line">
        <span class="body live-player-name" :class="{ 'is-me': player.isMe }">
          {{ playerLabel(player) }}<span v-if="player.isMe" class="lr-sr">, you</span>
        </span>
        <span class="body live-player-kda" :aria-label="kdaSpoken(player)">{{ kda(player) }}</span>
      </span>
      <span class="footnote lr-secondary">{{ detail }}</span>
      <span v-if="player.isDead" class="footnote live-player-dead">Respawns in <TimerText :seconds="respawn" /></span>
      <LoadoutStrip class="live-player-loadout" :player="loadoutOf(player)" loadout :no-trinket="!liveHasTrinkets" />
    </span>
  </ListRow>
</template>

<style scoped>
.live-player {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-0-5);
}
.live-player-line {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 0 var(--space-2);
}
.live-player-name {
  min-width: 0;
  overflow-wrap: break-word;
}
.live-player-name.is-me {
  color: var(--accent-text);
  font-weight: 600;
}
.live-player-kda {
  font-variant-numeric: tabular-nums;
}
.live-player-dead {
  color: var(--red-text);
}
.live-player-loadout {
  margin: var(--space-1) 0;
}
</style>
