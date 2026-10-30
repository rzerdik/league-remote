<script setup lang="ts">
import { CircleDotIcon, PaletteIcon, StarIcon } from '@lucide/vue'
import { computed, nextTick, ref, watch } from 'vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import type { MenuItem } from '../../components/lr/LrMenu.vue'
import SpellSlots from '../../components/lr/SpellSlots.vue'
import { useBusy } from '../../composables/useBusy'
import { focusQuietly } from '../../composables/useModalLayer'
import type { RunePage, SkinSelection, TeamMember } from '../../contracts'
import { spellsForMode } from '../../ddragon'
import { favouriteSkinId } from '../../favouriteSkins'
import { haptic, tapFeedback } from '../../haptics'
import { sendCommand } from '../../hub'
import SkinPicker from './SkinPicker.vue'
import { choiceName } from './skins'

/**
 * Summoner spells (two slots → spell picker sheet), rune page (pop-up menu) and, once your champion is locked in, your
 * skin (→ the Skins carousel sheet) for your own pick. When this phone has a favourite skin for the champion you locked
 * in (marked in the Skins sheet) and you aren't wearing it, a one-tap "Equip Favourite: Star Guardian Ahri" row offers
 * it; SelectSkin goes out only on that tap, never by itself.
 */
const props = defineProps<{
  me: TeamMember | null
  runePages: RunePage[]
  /** ARAM queue: offer ARAM's spells. */
  aram?: boolean
  skins?: SkinSelection | null
}>()

const spellChoices = computed(() => (props.aram ? spellsForMode('ARAM') : undefined))

// ----- summoner spells -----
const spells = useBusy()
function setSpells(spell1Id: number, spell2Id: number) {
  void spells.run(() => sendCommand({ type: 'SetSummonerSpells', spell1Id, spell2Id }))
}

// ----- rune pages -----
const runes = useBusy()
const currentRunePage = computed(() => props.runePages.find((p) => p.isCurrent) ?? null)
const runeItems = computed<MenuItem[]>(() =>
  props.runePages.map((p) => ({
    label: p.name,
    checked: p.isCurrent,
    onSelect: () => {
      if (p.id > 0 && !p.isCurrent) void runes.run(() => sendCommand({ type: 'SelectRunePage', runePageId: p.id }))
    },
  })),
)

// ----- skin -----
const skinsOpen = ref(false)
const skinValue = computed(() => (props.skins ? choiceName(props.skins, props.skins.selectedSkinId) || 'Choose' : ''))

// ----- favourite skin (a suggestion, sent only on tap) -----
const favourite = useBusy()
/** The favourite League accepted, until the state shows it (the row doesn't come back in between). */
const favouriteSent = ref<number | null>(null)
watch(
  () => props.skins?.selectedSkinId,
  () => (favouriteSent.value = null),
)
/** The favourite to offer: still yours to equip (listed now) and not the one you wear. */
const favouriteId = computed(() => {
  const s = props.skins
  const id = s ? favouriteSkinId(s.championId) : null
  return s && id && id !== s.selectedSkinId && id !== favouriteSent.value && choiceName(s, id) ? id : null
})
const favouriteName = computed(() => (props.skins && favouriteId.value ? choiceName(props.skins, favouriteId.value) : ''))
const skinRow = ref<{ $el: HTMLElement } | null>(null)
const favouriteRow = ref<{ $el: HTMLElement } | null>(null)
function equipFavourite() {
  const skinId = favouriteId.value
  if (!skinId || favourite.busy.value) return
  // The row goes away once it's equipped: focus (keyboard, VoiceOver) moves on to the Skin row instead of the page.
  const hadFocus = !!favouriteRow.value?.$el.contains(document.activeElement)
  tapFeedback()
  void favourite.run(async () => {
    const r = await sendCommand({ type: 'SelectSkin', skinId })
    if (!r?.success) {
      haptic('error')
      return
    }
    favouriteSent.value = skinId
    haptic('success')
    if (!hadFocus) return
    await nextTick()
    const next = skinRow.value?.$el.querySelector('button')
    if (next) focusQuietly(next)
  })
}
</script>

<template>
  <ListSection v-if="me || runePages.length" header="Loadout">
    <SpellSlots
      v-if="me"
      :spell1-id="Number(me.spell1Id)"
      :spell2-id="Number(me.spell2Id)"
      :spells="spellChoices"
      :disabled="spells.busy.value"
      @change="setSpells"
    />
    <ListRow
      v-if="runePages.length"
      title="Rune Page"
      :icon="CircleDotIcon"
      tile-color="indigo"
      :value="currentRunePage?.name ?? 'Choose'"
      :busy="runes.busy.value"
      :disabled="runes.busy.value"
      :menu="runeItems"
      menu-title="Rune Page"
    />
    <ListRow
      v-if="skins"
      ref="skinRow"
      title="Skin"
      :icon="PaletteIcon"
      tile-color="pink"
      :value="skinValue"
      tappable
      pop-up
      @click="skinsOpen = true"
    />
    <ListRow
      v-if="favouriteId"
      ref="favouriteRow"
      :title="`Equip Favourite: ${favouriteName}`"
      :icon="StarIcon"
      tile-color="accent"
      variant="accent"
      tappable
      wrap
      :busy="favourite.busy.value"
      @click="equipFavourite"
    />
  </ListSection>
  <SkinPicker v-if="skins" v-model:open="skinsOpen" :selection="skins" />
</template>
