<script setup lang="ts">
import { LogOutIcon, RotateCcwIcon } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import ActionBar, { type ActionBarAccessory } from '../components/lr/ActionBar.vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListRow from '../components/lr/ListRow.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrButton from '../components/lr/LrButton.vue'
import { useBusy } from '../composables/useBusy'
import { loadDdragonExtras } from '../ddragon'
import { formatSeconds } from '../format'
import { tapFeedback } from '../haptics'
import { openMatch } from '../navigation'
import { canAnswer, leavePostGame, myLine, playAgain, postGame, postGamePhase, postGameQueue, postGameTitle } from '../postGame'
import { matchById } from '../profile'
import { enemyNumbers } from '../teams'
import PostGameProgress from './postgame/PostGameProgress.vue'
import PostGameTeamSection from './postgame/PostGameTeamSection.vue'
import PostGameYou from './postgame/PostGameYou.vue'

/**
 * The client's post-game screen (phase EndOfGame): the result as the title (Arena: your place), the LP and mastery the
 * game gave, your stats, both teams (yours first; enemies by champion), and the client's two buttons, Play Again (back
 * to a lobby for this queue with your party) and Leave (back to Home). Before League has the stats (WaitingForStats,
 * PreEndOfGame — the PC shows the honor vote, which stays on the PC) it waits. Once the game is in your match history,
 * a row opens it there (bans and objectives).
 */
const { busy, run } = useBusy()
/** Which button's spinner shows while its command is on the way. */
const sending = ref<'again' | 'leave' | null>(null)

const summary = postGame
const me = computed(() => (summary.value ? myLine(summary.value) : null))
const title = computed(() => (summary.value ? postGameTitle(summary.value) : 'Game Over'))
const subtitle = computed(() => {
  const s = summary.value
  return s ? [postGameQueue(s), formatSeconds(s.durationSeconds)].filter(Boolean).join(' · ') : ''
})

const enemyNumber = computed(() => enemyNumbers(summary.value?.teams ?? []))

const waiting = computed(() =>
  postGamePhase.value === 'PreEndOfGame'
    ? { title: 'Game Ending', message: 'The scoreboard shows up here once League has the results.' }
    : { title: 'Waiting for Stats', message: 'The scoreboard shows up here once League has the results.' },
)

/** Play Again is offered until the summary says otherwise; the PC checks it anyway. */
const canPlayAgain = computed(() => summary.value?.canPlayAgain ?? true)
const accessory = computed<ActionBarAccessory | null>(() =>
  canAnswer.value && !canPlayAgain.value ? { tone: 'gray', title: 'Play Again Unavailable' } : null,
)

/** The finished game once it is in the match history (the PC rereads it after a game). */
const inHistory = computed(() => (summary.value ? matchById(summary.value.gameId) : null))

function again() {
  tapFeedback()
  sending.value = 'again'
  void run(playAgain).finally(() => (sending.value = null))
}
function leave() {
  tapFeedback()
  sending.value = 'leave'
  void run(leavePostGame).finally(() => (sending.value = null))
}

// Item names for VoiceOver and rune art for the loadouts.
onMounted(() => void loadDdragonExtras())
</script>

<template>
  <div class="lr-content" :class="{ 'lr-content--accessory': accessory }">
    <LargeTitle :title="title">
      <template v-if="subtitle">{{ subtitle }}</template>
    </LargeTitle>

    <template v-if="summary">
      <PostGameProgress :summary="summary" />
      <PostGameYou v-if="me" :me="me" :duration-seconds="summary.durationSeconds" :map-id="summary.mapId" />
      <PostGameTeamSection
        v-for="(t, i) in summary.teams"
        :key="t.teamId"
        :team="t"
        :summary="summary"
        :enemy-number="enemyNumber[i]"
      />
      <ListSection v-if="inHistory">
        <ListRow tappable chevron title="Open in Match History" @click="openMatch(inHistory.gameId)" />
      </ListSection>
    </template>
    <!-- Also in EndOfGame until the stats arrive (a moment after the phase, or while League's service is busy). -->
    <EmptyState v-else busy :title="waiting.title" :message="waiting.message" />

    <ActionBar v-if="canAnswer" :accessory="accessory">
      <template v-if="canPlayAgain">
        <LrButton
          variant="prominent"
          size="lg"
          block
          :leading="RotateCcwIcon"
          :loading="busy && sending === 'again'"
          :disabled="busy"
          @click="again"
        >
          Play Again
        </LrButton>
        <LrButton variant="glass" size="lg" :leading="LogOutIcon" :loading="busy && sending === 'leave'" :disabled="busy" @click="leave">
          Leave
        </LrButton>
      </template>
      <LrButton v-else variant="glass" size="lg" block :leading="LogOutIcon" :loading="busy" @click="leave">Leave</LrButton>
    </ActionBar>
  </div>
</template>
