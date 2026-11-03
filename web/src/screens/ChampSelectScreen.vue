<script setup lang="ts">
import { BanIcon, LockIcon } from '@lucide/vue'
import { computed, ref } from 'vue'
import { draftChat, draftQuickReplies } from '../chat'
import ChatButton from '../components/chat/ChatButton.vue'
import ChatSheet from '../components/chat/ChatSheet.vue'
import ActionBar, { type ActionBarAccessory } from '../components/lr/ActionBar.vue'
import EdgeGlow from '../components/lr/EdgeGlow.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrButton from '../components/lr/LrButton.vue'
import { sheetStack } from '../composables/useSheet'
import { autoPickGroupForQueue, type ChampSelectState, type RunePage } from '../contracts'
import { byPickOrder, DRAFT_DOT, draftView, hasMemberActions, myTurnNow } from '../draft'
import { queueLabel } from '../labels'
import AutoPickBanner from './champselect/AutoPickBanner.vue'
import AutoPickSection from './champselect/AutoPickSection.vue'
import BenchSection from './champselect/BenchSection.vue'
import ChampionPicker from './champselect/ChampionPicker.vue'
import DodgeSection from './champselect/DodgeSection.vue'
import DraftBoard from './champselect/DraftBoard.vue'
import EnemyMemberRow from './champselect/EnemyMemberRow.vue'
import LoadoutSection from './champselect/LoadoutSection.vue'
import SwapRequest from './champselect/SwapRequest.vue'
import TeamMemberRow from './champselect/TeamMemberRow.vue'
import TurnHeader from './champselect/TurnHeader.vue'
import { useChampionChoice } from './champselect/useChampionChoice'
import { useDraftTimer } from './champselect/useDraftTimer'
import { useSwaps } from './champselect/useSwaps'

const props = defineProps<{
  cs: ChampSelectState
  runePages: RunePage[]
  queueId: number | null
  /**
   * A player's profile is pushed over this screen (App.vue keeps it mounted underneath, hidden): its search, optimistic
   * hover and timers carry on, but its floating parts (picker, action bar, edge glow) step aside.
   */
  covered?: boolean
}>()
const chatOpen = ref(false)
const dodgeAlert = ref(false)
const quickReplies = computed(() => draftQuickReplies(props.cs))
const queue = computed(() => queueLabel(props.queueId))

const me = computed(() => props.cs.myTeam.find((m) => m.isLocalPlayer) ?? null)
const action = computed(() => props.cs.myAction)
const isBan = computed(() => action.value?.type === 'ban')
const actionHot = computed(() => myTurnNow(props.cs))

const { timeLeft, showTimer, finalBeat } = useDraftTimer(() => props.cs)

/** Where the draft stands and who is acting (see draft.ts). */
const draft = computed(() => draftView(props.cs))
const stage = computed(() => draft.value.stage)

/**
 * Bench modes (ARAM): champions are dealt, not picked. Your champion, Reroll and the bench take the picker's place.
 * A bench mode with subset picks still has an open pick action to lock in during BAN_PICK: the picker shows for that.
 */
const benchGame = computed(() => props.cs.benchEnabled)
/** The bench champion your swap is taking until League echoes it: your Your Team row shows it with Your Champion. */
const benchSwapId = ref<number | null>(null)
/** ARAM queues offer their own summoner spells (Mark / Snowball, Clarity). */
const aramQueue = computed(() => props.queueId !== null && autoPickGroupForQueue(props.queueId) === 'aram')

/**
 * The picker is up while you have a pick or ban left: your own turn, Declare Intent, or ahead of your pick so your
 * team sees your hover. Not for your pick while the bans are still running (League only takes bans then).
 */
const canAct = computed(() => {
  const a = action.value
  if (!a || a.completed) return false
  if (a.type === 'pick' && me.value && me.value.isLocked) return false
  if (stage.value === 'finalization' || stage.value === 'starting') return false
  return !(a.type === 'pick' && !a.isInProgress && stage.value === 'banning')
})

const choice = useChampionChoice(() => props.cs, () => canAct.value)
const { selectedId, myHoverId, popping, locking, lockDisabled, lockVerb, lockName, lockLabel } = choice

/**
 * ARAM's team lists: once your champion is set, the loadout is what's left to do, so it moves up under the bench. A keyed
 * list, so Vue moves the one LoadoutSection instead of remounting it (an open spell or skin sheet, its busy states stay),
 * and the DOM order VoiceOver reads is the order on screen.
 */
const loadoutFirst = computed(() => !!me.value?.isLocked || stage.value === 'finalization' || stage.value === 'starting')
const middle = computed(() => (loadoutFirst.value ? (['loadout', 'teams'] as const) : (['teams', 'loadout'] as const)))

/**
 * The draft's status is always on screen in one place: the picker's turn header while the picker is up, the action
 * bar's status otherwise. Both name the turn being played with the acting side's colour; when yours comes is the bar's
 * detail line.
 */
const turnTitle = computed(() => {
  if (actionHot.value) return isBan.value ? 'Your Ban' : 'Your Pick'
  return draft.value.title
})
const accessory = computed<ActionBarAccessory>(() => ({
  tone: DRAFT_DOT[draft.value.tone],
  title: draft.value.title,
  detail: draft.value.detail,
  countdown: showTimer.value ? timeLeft.value : undefined,
  announce: true,
}))

/** Never glass on glass: the picker steps aside while a modal sheet, the Dodge alert or a pushed profile is up. */
const pickerHidden = computed(() => sheetStack.modal > 0 || !!props.covered || dodgeAlert.value)

const myTeamOrdered = computed(() => byPickOrder(props.cs.myTeam))
/** The enemy team in pick order, like yours (League hides their positions in most queues). */
const theirTeamOrdered = computed(() => byPickOrder(props.cs.theirTeam ?? []))
/** Bench modes hide the enemy team until the game (no champions to show): no five "Not Picked Yet" rows then. */
const showEnemies = computed(
  () => theirTeamOrdered.value.length > 0 && (!benchGame.value || theirTeamOrdered.value.some((e) => e.championId > 0)),
)

const swaps = useSwaps(() => props.cs)
const { rowOffers, requests: swapRequests, busy: swapBusy } = swaps
/** Some teammate row has an actions button (⋯): the others keep its slot so the spells line up. */
const hasRowActions = computed(() => props.cs.myTeam.some((m) => hasMemberActions(m, rowOffers(m.cellId))))
</script>

<template>
  <div class="lr-content">
    <!-- Bench modes (ARAM): champions are dealt, so there is no draft to show: the team lists under a large title. -->
    <template v-if="benchGame">
      <LargeTitle title="Champion Select" compact>
        <template v-if="queue">{{ queue }}</template>
        <!-- On the title row; docks into the nav bar once the title collapses, so it stays reachable while the draft scrolls. -->
        <template v-if="draftChat" #trailing>
          <ChatButton :conversation="draftChat" @open="chatOpen = true" />
        </template>
      </LargeTitle>

      <BenchSection :cs="cs" :stage="stage" @pending="benchSwapId = $event" />

      <template v-for="part in middle" :key="part">
        <LoadoutSection v-if="part === 'loadout'" :me="me" :rune-pages="runePages" :aram="aramQueue" :skins="cs.skins" />
        <template v-else>
          <ListSection header="Your Team">
            <TeamMemberRow
              v-for="m in myTeamOrdered"
              :key="m.cellId"
              :member="m"
              :stage="stage"
              :offers="m.isLocalPlayer ? [] : rowOffers(m.cellId)"
              :hover-id="m.isLocalPlayer ? myHoverId : 0"
              :bench-swap-id="m.isLocalPlayer ? benchSwapId : null"
              :popping="m.isLocalPlayer && popping"
              :swap-busy="swapBusy"
              :reserve-actions="hasRowActions"
              @swap="swaps.send"
            />
          </ListSection>

          <ListSection v-if="showEnemies" header="Enemy Team">
            <EnemyMemberRow v-for="e in theirTeamOrdered" :key="e.cellId" :enemy="e" />
          </ListSection>
        </template>
      </template>
    </template>

    <!-- The draft on one screen: the title, the Dodge menu and the chat button are in the nav bar. -->
    <template v-else>
      <Teleport defer to="#lr-nav-trailing">
        <span v-if="draftChat && !covered" class="lr-largetitle-dock">
          <ChatButton :conversation="draftChat" @open="chatOpen = true" />
        </span>
      </Teleport>
      <DraftBoard
        :cs="cs"
        :stage="stage"
        :selected-id="selectedId"
        :popping="popping"
        :swap-busy="swapBusy"
        :offers-for="rowOffers"
        @swap="swaps.send"
      />
      <LoadoutSection :me="me" :rune-pages="runePages" :aram="aramQueue" :skins="cs.skins" tray />
    </template>

    <AutoPickSection :class="{ 'lr-board-extra': !benchGame }" :quiet-header="!benchGame" />

    <DodgeSection v-model:alert="dodgeAlert" :can-dodge="cs.canDodge" :in-nav="!benchGame" :nav-hidden="covered" />

    <ChampionPicker
      :cs="cs"
      :can-act="canAct"
      :hidden="pickerHidden"
      :selected-id="selectedId"
      @select="choice.tap"
    >
      <template #turn>
        <TurnHeader
          :title="turnTitle"
          :tone="actionHot ? (isBan ? 'ban' : 'pick') : null"
          :dot="DRAFT_DOT[actionHot ? (isBan ? 'you-ban' : 'you-pick') : draft.tone]"
          :seconds="showTimer ? timeLeft : null"
        />
      </template>
    </ChampionPicker>

    <!-- Modal: the picker steps aside while it is up and comes back when it closes. -->
    <ChatSheet v-model:open="chatOpen" :conversation="draftChat" :quick-replies="quickReplies" />

    <EdgeGlow v-if="!covered" :beat="finalBeat" :tone="isBan ? 'ban' : 'pick'" />

    <!-- Lock In / Ban only on your own turn: Declare Intent and the turns before yours are hovers, nothing to lock. -->
    <ActionBar v-if="!covered" :accessory="canAct ? null : accessory" :over-sheet="canAct">
      <template v-if="canAct && actionHot" #default>
        <LrButton
          :variant="isBan ? 'destructive' : 'prominent'"
          size="sm"
          :leading="isBan ? BanIcon : LockIcon"
          :loading="locking"
          :disabled="lockDisabled"
          :aria-label="lockLabel"
          @click="choice.lockIn"
        >
          <span class="lr-fit-label">
            <span>{{ lockVerb }}</span>
            <span v-if="lockName">&nbsp;{{ lockName }}</span>
          </span>
        </LrButton>
      </template>
    </ActionBar>

    <AutoPickBanner />

    <Teleport defer to="#lr-banners-actions">
      <TransitionGroup name="lr-pop">
        <SwapRequest
          v-for="s in swapRequests"
          :key="swaps.swapKey(s)"
          :offer="s"
          :message="swaps.requestText(s)"
          :busy="swaps.answerBusy(s)"
          :disabled="swapBusy"
          @accept="swaps.send('AcceptSwap', s)"
          @decline="swaps.send('DeclineSwap', s)"
        />
      </TransitionGroup>
    </Teleport>
  </div>
</template>
