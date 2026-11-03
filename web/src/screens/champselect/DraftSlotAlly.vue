<script setup lang="ts">
import { CircleUserIcon } from '@lucide/vue'
import { computed } from 'vue'
import LrMenu, { type MenuItem } from '../../components/lr/LrMenu.vue'
import type { SwapOffer, TeamMember } from '../../contracts'
import { championName, spellById } from '../../ddragon'
import { hasMemberActions, type DraftStage } from '../../draft'
import { positionLabel } from '../../labels'
import { openPlayer } from '../../navigation'
import { playerShortName } from '../../riotId'
import DraftSlotBody from './DraftSlotBody.vue'
import { SWAP_ACTION, SWAP_CANCEL, SWAP_ICON, SWAP_ORDER, SWAP_SENT, SWAP_SENT_SPOKEN, type SwapCommand } from './swaps'

/**
 * One ally on the draft board: the champion as the title, over the role and who ("Top · NightOwl", "Mid · You"). While
 * nothing is chosen the role is the title. The second line says "Picking…" during their pick and "Swap Sent" once you
 * asked. A teammate's row is the trigger of their actions: the swaps, and View Profile while League shows who they
 * are (`member.puuid`; never in an anonymous ranked draft). `hoverId` is your own optimistic hover while you pick.
 */
const props = defineProps<{
  member: TeamMember
  /** Offers with this teammate that we may send (AVAILABLE) or already sent (SENT). */
  offers: SwapOffer[]
  hoverId?: number
  stage: DraftStage
  popping?: boolean
  swapBusy?: boolean
}>()
const emit = defineEmits<{ swap: [type: SwapCommand, offer: SwapOffer] }>()

const you = computed(() => props.member.isLocalPlayer)
const locked = computed(() => props.member.isLocked)
const role = computed(() => positionLabel(props.member.assignedPosition))
const hovering = computed(() => you.value && !locked.value && !!props.hoverId && props.hoverId > 0)
const portraitId = computed(() => (hovering.value ? props.hoverId! : props.member.championId || props.member.championPickIntent))

const name = computed(() => playerShortName(props.member.riotId))
const who = computed(() => (you.value ? 'You' : name.value))
/** Picking now, you included. The bans ring on the ban tiles above the team, not on the rows. */
const acting = computed(() => props.stage !== 'banning' && props.member.isActing && !locked.value)
const actingWord = computed(() => (acting.value ? 'Picking…' : ''))
const sent = computed(() => props.offers.find((o) => o.state === 'SENT') ?? null)

const title = computed(() => championName(portraitId.value) || role.value || (you.value ? 'You' : 'Teammate'))
const line = computed(() => {
  if (sent.value) return SWAP_SENT[sent.value.kind]
  const second = actingWord.value || who.value
  const named = championName(portraitId.value) ? role.value : ''
  return [named, second].filter(Boolean).join(' · ')
})

const spellIds = computed(() => [Number(props.member.spell1Id), Number(props.member.spell2Id)])
const spellsLabel = computed(() => {
  const names = spellIds.value.map((id) => spellById(id)?.name).filter(Boolean)
  return names.length ? `Summoner spells: ${names.join(', ')}` : 'Summoner spells'
})

const actionsFor = computed(() => name.value || role.value || 'Teammate')
const menuTitle = computed(() => [actionsFor.value, championName(props.member.championId)].filter(Boolean).join(' · '))
const hasActions = computed(() => hasMemberActions(props.member, props.offers))
const canView = computed(() => !you.value && !!props.member.puuid)
const swapItems = computed<MenuItem[]>(() =>
  SWAP_ORDER.map((kind) => {
    const pending = props.offers.find((o) => o.kind === kind && o.state === 'SENT')
    if (pending)
      return {
        label: SWAP_CANCEL[kind],
        icon: SWAP_ICON[kind],
        destructive: true,
        disabled: props.swapBusy,
        onSelect: () => emit('swap', 'CancelSwap', pending),
      }
    const available = props.offers.find((o) => o.kind === kind && o.state === 'AVAILABLE')
    return {
      label: SWAP_ACTION[kind],
      icon: SWAP_ICON[kind],
      disabled: !available || props.swapBusy,
      onSelect: () => available && emit('swap', 'RequestSwap', available),
    }
  }),
)
const menuItems = computed<MenuItem[]>(() => [
  ...(props.offers.length ? swapItems.value : []),
  ...(canView.value ? [{ label: 'View Profile', icon: CircleUserIcon, onSelect: () => openPlayer(props.member.puuid, props.member.riotId) }] : []),
])

const body = computed(() => ({
  championId: portraitId.value,
  state: you.value ? ('you' as const) : acting.value ? ('acting' as const) : null,
  intent: portraitId.value > 0 && !locked.value,
  popping: you.value && props.popping,
  title: title.value,
  line: line.value,
  lineAccent: !!sent.value,
  spellIds: spellIds.value,
  spellsLabel: spellsLabel.value,
}))
</script>

<template>
  <LrMenu v-if="hasActions" :items="menuItems" :title="menuTitle" align="start">
    <button type="button" class="lr-slot lr-slot--ally" :aria-label="`Actions for ${actionsFor}, pick ${member.pickOrder}`">
      <DraftSlotBody v-bind="body">
        <template v-if="sent" #line>
          <span aria-hidden="true">{{ SWAP_SENT[sent.kind] }}</span><span class="lr-sr">{{ SWAP_SENT_SPOKEN[sent.kind] }}</span>
        </template>
      </DraftSlotBody>
    </button>
  </LrMenu>
  <div v-else class="lr-slot lr-slot--ally" :class="{ 'is-you': you }">
    <DraftSlotBody v-bind="body" />
  </div>
</template>
