<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import LoadoutStrip from '../../components/profile/LoadoutStrip.vue'
import type { MatchParticipant } from '../../contracts'
import { championName } from '../../ddragon'
import { formatCompact, kda, kdaSpoken } from '../../format'
import { openPlayer } from '../../navigation'
import { canOpenPlayer, csLine, participantName } from '../../profile'
import { participantRiotId } from '../../riotId'
import { ARENA_MAP_ID, hasTrinkets } from '../../teams'

/**
 * One player in a game's details, compact enough for 375 px: name and K / D / A; champion, level and CS; spells,
 * keystone and items; damage, gold and vision. Your own team by game name (the champion on the second line), enemies
 * by champion only — the connector never sends their names. Your own row is highlighted like in Champion Select. A
 * named teammate's row opens their profile (the connector sends a puuid only beside a teammate's name, so enemies and
 * anonymous players never open). `meLabel`: set in another player's game (their name), where `isMe` is them: their row
 * isn't announced as "you".
 */
const props = defineProps<{ player: MatchParticipant; durationSeconds: number; mapId: number; meLabel?: string }>()

const named = computed(() => !!props.player.gameName)
const openable = computed(() => canOpenPlayer(props.player))

function open() {
  if (canOpenPlayer(props.player)) openPlayer(props.player.puuid, participantRiotId(props.player))
}

const detail = computed(() =>
  [
    named.value ? championName(props.player.championId) : '',
    `Level ${props.player.champLevel}`,
    props.mapId === ARENA_MAP_ID ? '' : csLine(props.player, props.durationSeconds),
  ]
    .filter(Boolean)
    .join(' · '),
)
const totals = computed(() =>
  [
    `${formatCompact(props.player.damage)} damage`,
    `${formatCompact(props.player.gold)} gold`,
    `${props.player.visionScore} vision`,
  ].join(' · '),
)
</script>

<template>
  <ListRow :variant="player.isMe ? 'you' : 'default'" :tappable="openable" :chevron="openable" @click="open">
    <template #lead>
      <ChampionIcon :champion-id="player.championId" :state="player.isMe ? 'you' : null" decorative />
    </template>
    <span class="player-row">
      <span class="player-row-line">
        <span class="body player-row-name" :class="{ 'is-me': player.isMe }">
          {{ participantName(player) }}<span v-if="player.isMe && !meLabel" class="lr-sr">, you</span
          ><span v-if="openable" class="lr-sr">, View Profile</span>
        </span>
        <span class="body player-row-kda" :aria-label="kdaSpoken(player)">{{ kda(player) }}</span>
      </span>
      <span class="footnote lr-secondary">{{ detail }}</span>
      <LoadoutStrip class="player-row-loadout" :player="player" loadout :no-trinket="!hasTrinkets(mapId)" />
      <span class="footnote lr-secondary player-row-totals">{{ totals }}</span>
    </span>
  </ListRow>
</template>

<style scoped>
.player-row {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-0-5);
}
.player-row-line {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 0 var(--space-2);
}
.player-row-name {
  min-width: 0;
  overflow-wrap: break-word;
}
.player-row-name.is-me {
  color: var(--accent-text);
  font-weight: 600;
}
.player-row-kda,
.player-row-totals {
  font-variant-numeric: tabular-nums;
}
.player-row-loadout {
  margin: var(--space-1) 0;
}
</style>
