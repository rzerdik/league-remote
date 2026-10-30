<script setup lang="ts">
import { ScrollTextIcon } from '@lucide/vue'
import { computed, onMounted } from 'vue'
import { gameClock, gameLoaded } from '../clock'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListSection from '../components/lr/ListSection.vue'
import LiveGameCard from '../components/lr/LiveGameCard.vue'
import LrSegmented, { panelId, tabId } from '../components/lr/LrSegmented.vue'
import { loadDdragonExtras } from '../ddragon'
import { queueLabel } from '../labels'
import { itemLead, liveClock, liveGame, me, teams } from '../liveGame'
import { feed } from '../liveGame/feed'
import { nav, type InGameView } from '../navigation'
import { enemyNumbers } from '../teams'
import BuffTimers from './ingame/BuffTimers.vue'
import EventRow from './ingame/EventRow.vue'
import LiveScore from './ingame/LiveScore.vue'
import ObjectivesSection from './ingame/ObjectivesSection.vue'
import ScoreboardTeam from './ingame/ScoreboardTeam.vue'
import YouSection from './ingame/YouSection.vue'

/**
 * In Game: the clock, and — once the game's live data arrives (LiveGameUpdated) — three views under a segmented
 * control: Overview (the score, you, objective timers, hand-started buff timers), Scoreboard (both teams, yours first)
 * and Events (the feed, newest first). Without live data (loading screen) it is the clock card alone,
 * with a footer saying why. Once the game has a result: "Game Over", the result on the card, no objective or buff
 * timers (nothing is coming any more), the final score, stats, scoreboard and feed stay.
 */
const props = defineProps<{ queueId: number | null }>()
const queue = computed(() => queueLabel(props.queueId))

const VIEWS: { value: InGameView; label: string }[] = [
  { value: 'overview', label: 'Overview' },
  { value: 'scoreboard', label: 'Scoreboard' },
  { value: 'events', label: 'Events' },
]
const SEGMENTS_ID = 'ingame-view'

/** Objective timers (and buffs, and the Rift's objective counts) only exist on Summoner's Rift. */
const rift = computed(() => !!liveGame.value?.timers)
/** The game ended (a result came): the title says so and the timers for what comes next go away. */
const gameOver = computed(() => !!liveGame.value?.result)
const enemyNumber = computed(() => enemyNumbers(teams.value))

// Item prices for the scoreboard's item value, item names and rune art for VoiceOver and the loadouts.
onMounted(() => void loadDdragonExtras())
</script>

<template>
  <div class="lr-content">
    <LargeTitle :title="gameOver ? 'Game Over' : 'In Game'">
      <template v-if="queue">{{ queue }}</template>
    </LargeTitle>

    <ListSection
      v-if="!liveGame"
      bare
      :footer="gameLoaded ? 'The score, timers and scoreboard show up once your PC can read them from the game.' : undefined"
    >
      <LiveGameCard :seconds="gameClock" :loaded="gameLoaded" />
    </ListSection>

    <template v-else>
      <ListSection bare>
        <LrSegmented :id="SEGMENTS_ID" v-model="nav.inGameView" :options="VIEWS" aria-label="In Game Views" />
      </ListSection>

      <div :id="panelId(SEGMENTS_ID)" role="tabpanel" :aria-labelledby="tabId(SEGMENTS_ID, nav.inGameView)">
        <template v-if="nav.inGameView === 'overview'">
          <ListSection bare>
            <LiveGameCard :seconds="liveClock" loaded :result="liveGame.result">
              <LiveScore :teams="teams" />
            </LiveGameCard>
          </ListSection>
          <YouSection v-if="me" :me="me" :rift="rift" />
          <template v-if="!gameOver">
            <ObjectivesSection v-if="liveGame.timers && liveClock != null" :timers="liveGame.timers" :clock="liveClock" />
            <BuffTimers v-if="rift" />
          </template>
        </template>

        <template v-else-if="nav.inGameView === 'scoreboard'">
          <ScoreboardTeam
            v-for="(t, i) in teams"
            :key="t.side"
            :team="t"
            :rift="rift"
            :enemy-number="enemyNumber[i]"
            :footer="t.isMyTeam ? itemLead() || undefined : undefined"
          />
        </template>

        <template v-else>
          <ListSection v-if="feed.length">
            <EventRow v-for="item in feed" :key="item.id" :item="item" />
          </ListSection>
          <EmptyState v-else :icon="ScrollTextIcon" title="No Events Yet" message="Kills, objectives and towers show up here as they happen." />
        </template>
      </div>
    </template>
  </div>
</template>
