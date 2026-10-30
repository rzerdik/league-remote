<script setup lang="ts">
import { CircleUserIcon, EllipsisIcon } from '@lucide/vue'
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import LrButton from '../../components/lr/LrButton.vue'
import LrMenu, { type MenuItem } from '../../components/lr/LrMenu.vue'
import SpellIcon from '../../components/lr/SpellIcon.vue'
import Tag from '../../components/lr/Tag.vue'
import type { SwapOffer, TeamMember } from '../../contracts'
import { championName, spellById } from '../../ddragon'
import { hasMemberActions, hoveringLine, type DraftStage } from '../../draft'
import { positionLabel } from '../../labels'
import { openPlayer } from '../../navigation'
import { playerShortName } from '../../riotId'
import { SWAP_ACTION, SWAP_CANCEL, SWAP_ICON, SWAP_ORDER, SWAP_SENT, SWAP_SENT_SPOKEN, type SwapCommand } from './swaps'

/**
 * One ally in Champion Select: portrait, name or role, champion, summoner spells and (teammates only) the actions menu:
 * the swaps, and View Profile while League shows who they are (`member.puuid`; never in an anonymous ranked draft).
 * A teammate League names (`member.riotId`, same rule) is titled by their game name with the role leading the champion
 * line ("Top · Hovering Ahri"); an anonymous one, and your own row, by the role.
 * `hoverId` is your own optimistic hover while you pick. Teammates whose turn it is get a "Picking" / "Banning" chip.
 * Portraits are always at full opacity (as in the enemy list): a locked-in champion is named, a hovered or intended
 * one reads "Hovering Ahri". The trailing column lines up down the list: spells, then the actions button or (with
 * `reserveActions`, when any teammate has one) an empty slot its size. State tags ("Picking", "Swap Sent") go on the
 * champion line, so they never push a row's spells out of the column.
 */
const props = defineProps<{
  member: TeamMember
  /** Offers with this teammate that we may send (AVAILABLE) or already sent (SENT). */
  offers: SwapOffer[]
  hoverId?: number
  /**
   * Bench modes: the champion your bench swap is taking, until League echoes it (BenchSection's optimistic swap), so
   * this row and Your Champion change together.
   */
  benchSwapId?: number | null
  stage?: DraftStage
  popping?: boolean
  swapBusy?: boolean
  /** Some row of the list has an actions button: rows without one keep its slot, so the spells line up. */
  reserveActions?: boolean
}>()
const emit = defineEmits<{ swap: [type: SwapCommand, offer: SwapOffer] }>()

const role = computed(() => positionLabel(props.member.assignedPosition))
const you = computed(() => props.member.isLocalPlayer)
const locked = computed(() => props.member.isLocked)
/** The member's champion, or (your row) the bench champion your swap is taking. */
const championId = computed(() => (you.value && props.benchSwapId) || props.member.championId)
const hovering = computed(() => you.value && !locked.value && !!props.hoverId && props.hoverId > 0)
const banning = computed(() => props.stage === 'banning' && props.member.isActing)
const picking = computed(() => props.stage !== 'banning' && props.member.isActing && !locked.value)

const portraitId = computed(() =>
  hovering.value ? props.hoverId! : championId.value || props.member.championPickIntent,
)
const portraitState = computed(() => {
  if (you.value) return 'you' as const
  if (picking.value) return 'acting' as const
  return portraitId.value > 0 ? ('locked' as const) : ('empty' as const)
})

/** The teammate's game name while League shows it (never in an anonymous draft); '' for you and hidden teammates. */
const name = computed(() => (you.value ? '' : playerShortName(props.member.riotId)))
const title = computed(() => name.value || role.value || (you.value ? 'You' : 'Teammate'))
/** The locked-in champion, or "Hovering Ahri" while it isn't locked yet; after the role when the title is a name. */
const subtitle = computed(() => {
  const champion = locked.value
    ? championName(championId.value)
    : portraitId.value > 0
      ? hoveringLine(portraitId.value)
      : ''
  return [name.value ? role.value : '', champion].filter(Boolean).join(' · ') || undefined
})
/** Who the row is to VoiceOver and in the menu: their name, else the role. */
const who = computed(() => name.value || role.value || 'Teammate')
/**
 * "Picking" / "Banning" chip while it's a teammate's turn. Not on your own row: the picker's header ("Your Pick") and
 * your ring say it, and the chip would squeeze your champion's name.
 */
const actingTag = computed(() => (you.value ? '' : banning.value ? 'Banning' : picking.value ? 'Picking' : ''))

const spellIds = computed(() => [Number(props.member.spell1Id), Number(props.member.spell2Id)])
const spellsLabel = computed(() => {
  const names = spellIds.value.map((id) => spellById(id)?.name).filter(Boolean)
  return names.length ? `Summoner spells: ${names.join(', ')}` : 'Summoner spells'
})

const sent = computed(() => props.offers.find((o) => o.state === 'SENT') ?? null)

const menuTitle = computed(() => [who.value, championName(championId.value)].filter(Boolean).join(' · '))
/** Their profile, pushed over Champion Select (it closes by itself when your turn or a swap offer comes). */
const canView = computed(() => !you.value && !!props.member.puuid)
/** The actions button: swaps with this teammate, or View Profile (see `hasMemberActions` for the slot it reserves). */
const hasActions = computed(() => hasMemberActions(props.member, props.offers))
const menuItems = computed<MenuItem[]>(() => [
  ...(props.offers.length ? swapItems.value : []),
  ...(canView.value ? [{ label: 'View Profile', icon: CircleUserIcon, onSelect: () => openPlayer(props.member.puuid, props.member.riotId) }] : []),
])
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
</script>

<template>
  <!-- `wrap`: "Hovering Aurelion Sol" wraps under the role rather than truncating beside the spells. -->
  <ListRow :variant="you ? 'you' : 'default'" :title="title" wrap>
    <template #lead>
      <ChampionIcon
        :champion-id="portraitId"
        :state="portraitState"
        :popping="you && popping"
        :decorative="!!subtitle"
      />
    </template>
    <template v-if="subtitle || actingTag || sent" #subtitle>
      <span class="lr-row-subline">
        <span v-if="subtitle">{{ subtitle }}</span>
        <Tag v-if="actingTag" :tone="banning ? 'red' : 'accent'" :dot="banning ? 'red' : 'green'" pulse>{{ actingTag }}</Tag>
        <Tag v-if="sent" tone="accent" :icon="SWAP_ICON[sent.kind]">
          <span aria-hidden="true">{{ SWAP_SENT[sent.kind] }}</span><span class="lr-sr">{{ SWAP_SENT_SPOKEN[sent.kind] }}</span>
        </Tag>
      </span>
    </template>
    <template #trailing>
      <!-- One group, so the spells and the actions button wrap under the title together, never one by one. -->
      <span class="lr-row-group">
        <span class="lr-spells" role="img" :aria-label="spellsLabel">
          <SpellIcon v-for="(id, i) in spellIds" :key="i" :spell-id="id" />
        </span>
        <LrMenu v-if="hasActions" :items="menuItems" :title="menuTitle">
          <LrButton variant="gray" size="sm" :icon="EllipsisIcon" :aria-label="`Actions for ${who}`" />
        </LrMenu>
        <span v-else-if="reserveActions" class="lr-row-slot" aria-hidden="true" />
      </span>
    </template>
  </ListRow>
</template>
