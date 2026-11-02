<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import StatusDot from '../../components/lr/StatusDot.vue'
import type { ChampSelectState, EnemyMember, SwapOffer } from '../../contracts'
import { championName } from '../../ddragon'
import { byPickOrder, type DraftStage } from '../../draft'
import DraftSlotAlly from './DraftSlotAlly.vue'
import DraftSlotBody from './DraftSlotBody.vue'
import type { SwapCommand } from './swaps'

/**
 * Champion Select on one screen: both teams face each other, each with its bans above it, portraits on the screen's outer
 * edges. An empty slot shows its pick number, so the draft order needs no strip of its own. `density` follows the room the
 * picker leaves: `roomy` with the picker down, `peek` at its low detent, `compact` above that. Enemies: champion, hover and
 * whose turn only (never names; bans hidden until League reveals them).
 */
const props = defineProps<{
  cs: ChampSelectState
  stage: DraftStage
  density: 'roomy' | 'peek' | 'compact'
  /** Your optimistic hover, a pick or a ban. */
  selectedId: number
  popping?: boolean
  swapBusy?: boolean
  offersFor: (cellId: number) => SwapOffer[]
}>()
defineEmits<{ swap: [type: SwapCommand, offer: SwapOffer] }>()

const allies = computed(() => byPickOrder(props.cs.myTeam))
const enemies = computed(() => byPickOrder(props.cs.theirTeam ?? []))
const banning = computed(() => props.cs.myAction?.type === 'ban' && !props.cs.myAction.completed)

/** Blind pick has no bans. */
const hasBans = computed(
  () =>
    props.cs.turns.some((t) => t.type === 'ban') ||
    (props.cs.bans?.myTeamBans ?? []).length > 0 ||
    (props.cs.bans?.theirTeamBans ?? []).length > 0,
)
const positive = (ids: number[] | undefined) => (ids ?? []).filter((id) => id > 0)
/** One tile per player of the team. During the bans your team's empty tiles ring, the first with your own hover. */
function banTiles(ids: number[], size: number, ours: boolean) {
  const acting = ours && props.stage === 'banning' ? props.cs.myTeam.filter((m) => m.isActing || m.isLocalPlayer).length : 0
  return Array.from({ length: Math.max(size, ids.length) }, (_, i) => {
    const open = i - ids.length
    const now = open >= 0 && open < acting
    const hover = now && open === 0 && banning.value ? props.selectedId : 0
    return { key: `${i}:${ids[i] ?? 0}`, championId: ids[i] ?? hover, banned: i < ids.length, acting: now, intent: hover > 0 }
  })
}
const myBans = computed(() => banTiles(positive(props.cs.bans?.myTeamBans), allies.value.length, true))
const theirBans = computed(() => banTiles(positive(props.cs.bans?.theirTeamBans), enemies.value.length || allies.value.length, false))

const portraitState = (e: EnemyMember) => (e.isActing && !e.isLocked ? ('enemy-acting' as const) : null)
</script>

<template>
  <h1 class="lr-sr">Champion Select</h1>
  <section class="lr-board" :class="density !== 'roomy' && `lr-board--${density}`" aria-label="Draft board">
    <div class="lr-board-sides">
      <h2 class="lr-status caption-1 lr-secondary"><StatusDot tone="blue" /><span>Your Team</span></h2>
      <h2 v-if="enemies.length" class="lr-status caption-1 lr-secondary"><span>Enemy Team</span><StatusDot tone="red" /></h2>
    </div>

    <div v-if="hasBans" class="lr-board-bans">
      <TransitionGroup tag="ul" name="lr-bans" class="lr-ban-tiles" aria-label="Your Team bans">
        <li v-for="b in myBans" :key="b.key">
          <ChampionIcon :champion-id="b.championId" size="ban" :state="b.banned ? 'banned' : b.acting ? 'acting' : null" :intent="b.intent" />
        </li>
      </TransitionGroup>
      <TransitionGroup tag="ul" name="lr-bans" class="lr-ban-tiles" aria-label="Enemy Team bans">
        <li v-for="b in theirBans" :key="b.key">
          <ChampionIcon :champion-id="b.championId" size="ban" :state="b.banned ? 'banned' : null" />
        </li>
      </TransitionGroup>
    </div>

    <div class="lr-board-rows">
      <ol class="lr-slots" aria-label="Your Team">
        <li v-for="m in allies" :key="m.cellId">
          <DraftSlotAlly
            :member="m"
            :stage="stage"
            :offers="m.isLocalPlayer ? [] : offersFor(m.cellId)"
            :hover-id="m.isLocalPlayer && !banning ? selectedId : 0"
            :popping="m.isLocalPlayer && popping"
            :swap-busy="swapBusy"
            @swap="(type, offer) => $emit('swap', type, offer)"
          />
        </li>
      </ol>
      <ol v-if="enemies.length" class="lr-slots" aria-label="Enemy Team">
        <li v-for="e in enemies" :key="e.cellId">
          <div
            class="lr-slot lr-slot--enemy"
            :aria-label="e.championId > 0 ? undefined : `Pick ${e.pickOrder}: not picked yet`"
          >
            <DraftSlotBody
              :champion-id="e.championId"
              :state="portraitState(e)"
              :intent="e.championId > 0 && !e.isLocked"
              :order="e.pickOrder"
              :title="championName(e.championId)"
              :line="e.isActing && !e.isLocked ? 'Picking…' : undefined"
            />
          </div>
        </li>
      </ol>
    </div>
  </section>
</template>
