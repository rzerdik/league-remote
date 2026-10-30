<script setup lang="ts">
import { CheckIcon, CircleDotIcon } from '@lucide/vue'
import { computed, reactive, ref, watch } from 'vue'
import {
  autoPickSettings,
  championList,
  emptyPreset,
  findPreset,
  hasBans,
  hasPicks,
  presetTitle,
  removeAutoPickPreset,
  runePageName,
  runePages,
  saveAutoPickPreset,
  samePreset,
  spellPair,
} from '../../autoPick'
import { useBusy } from '../../composables/useBusy'
import {
  AutoPickLockDelayMs,
  AutoPickMaxBans,
  AutoPickMaxPicks,
  type AutoPickPreset,
  type AutoPickQueueGroup,
  type AutoPickRole,
} from '../../contracts'
import { ddragon, spellsForMode } from '../../ddragon'
import { haptic, tapFeedback } from '../../haptics'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrAlert from '../lr/LrAlert.vue'
import LrButton from '../lr/LrButton.vue'
import type { MenuItem } from '../lr/LrMenu.vue'
import Sheet from '../lr/Sheet.vue'
import SpellSlots from '../lr/SpellSlots.vue'
import StatusLine from '../lr/StatusLine.vue'
import ChampionListSection from './ChampionListSection.vue'

/**
 * Editor for one auto pick preset (queue group × role): ban list, pick list, summoner spells (both or neither) and
 * rune page. Edits a local copy; the checkmark saves it with one 'SetAutoPickPreset' (nothing set removes the preset)
 * and closes once the PC confirmed. A role preset's empty fields fall back to the group's Any Role preset, and the
 * footers say what they would use. With unsaved changes, closing (✕, swipe, Escape, scrim) asks "Discard Changes?".
 */
const props = defineProps<{ target: { group: AutoPickQueueGroup; role: AutoPickRole } | null }>()
const emit = defineEmits<{ close: [] }>()

/** Kept after close so the sheet doesn't empty out while it animates away. */
const shown = ref<{ group: AutoPickQueueGroup; role: AutoPickRole }>(props.target ?? { group: 'draft', role: 'ANY' })
const draft = reactive<AutoPickPreset>(emptyPreset('draft', 'ANY'))

const saved = computed(() => findPreset(autoPickSettings.value, shown.value.group, shown.value.role))
/** The group's Any Role preset, which a role preset falls back to (null for Any itself). */
const fallback = computed(() =>
  shown.value.role === 'ANY' ? null : findPreset(autoPickSettings.value, shown.value.group, 'ANY'),
)

watch(
  () => props.target,
  (t) => {
    if (!t) return
    shown.value = t
    const p = findPreset(autoPickSettings.value, t.group, t.role) ?? emptyPreset(t.group, t.role)
    Object.assign(draft, { ...p, banChampionIds: [...p.banChampionIds], pickChampionIds: [...p.pickChampionIds] })
  },
  { immediate: true },
)

const open = computed({
  get: () => !!props.target,
  set: (o: boolean) => {
    if (!o) emit('close')
  },
})

const group = computed(() => shown.value.group)
const isRole = computed(() => shown.value.role !== 'ANY')
const title = computed(() => presetTitle(group.value, shown.value.role))

// ----- lists -----
const bans = computed({ get: () => draft.banChampionIds, set: (v: number[]) => (draft.banChampionIds = v) })
const picks = computed({ get: () => draft.pickChampionIds, set: (v: number[]) => (draft.pickChampionIds = v) })
const banFooter = computed(() => {
  const any = fallback.value?.banChampionIds ?? []
  if (!draft.banChampionIds.length && any.length) return `Empty: uses Any Role (${championList(any)}).`
  return 'The first one still free is banned.'
})
const delay = AutoPickLockDelayMs / 1000
const pickFooter = computed(() => {
  const any = fallback.value?.pickChampionIds ?? []
  if (!draft.pickChampionIds.length && any.length) return `Empty: uses Any Role (${championList(any)}).`
  return `The first one you own that’s still free is hovered, then locked in ${delay} seconds later.`
})

// ----- spells -----
const spellChoices = computed(() => (group.value === 'aram' ? spellsForMode('ARAM') : ddragon.spells))
/** An empty slot of a role preset reads "Any Role" when Any Role has spells to fall back to. */
const spellPlaceholder = computed(() =>
  fallback.value && fallback.value.spell1Id > 0 && fallback.value.spell2Id > 0 ? 'Any Role' : 'Choose',
)
const spellsSet = computed(() => draft.spell1Id > 0 || draft.spell2Id > 0)
/** Only one of the two chosen: the PC takes both or neither. */
const halfSpells = computed(() => (draft.spell1Id > 0) !== (draft.spell2Id > 0))
function setSpells(s1: number, s2: number) {
  draft.spell1Id = s1
  draft.spell2Id = s2
}
function clearSpells() {
  tapFeedback()
  setSpells(0, 0)
}
const spellFooter = computed(() => {
  if (draft.spell1Id > 0 && draft.spell2Id > 0) return 'Set when champion select starts.'
  const any = fallback.value ? spellPair(fallback.value.spell1Id, fallback.value.spell2Id) : ''
  return any ? `Empty: uses Any Role (${any}).` : 'Empty: your spells stay as they are.'
})

// ----- rune page -----
const runeFallback = computed(() =>
  fallback.value?.runePageId ? runePageName(fallback.value.runePageId, fallback.value.runePageName) : '',
)
const noRunePage = computed(() => (isRole.value ? 'Same as Any Role' : 'Don’t Change'))
const runeValue = computed(() => runePageName(draft.runePageId, draft.runePageName) || noRunePage.value)
/** The name goes along so the row reads right at once; the PC stores its own copy from the client when saving. */
function setRunePage(id: number, name: string) {
  draft.runePageId = id
  draft.runePageName = name
}
const runeItems = computed<(MenuItem | 'separator')[]>(() => [
  { label: noRunePage.value, checked: !(draft.runePageId > 0), onSelect: () => setRunePage(0, '') },
  'separator',
  ...runePages.value
    .filter((p) => p.id > 0)
    .map((p) => ({ label: p.name, checked: p.id === draft.runePageId, onSelect: () => setRunePage(p.id, p.name) })),
])
const runeFooter = computed(() => {
  if (!runePages.value.length) return 'Your rune pages show up here while League runs on your PC.'
  if (draft.runePageId > 0) return 'Chosen when champion select starts.'
  if (runeFallback.value) return `Empty: uses Any Role (${runeFallback.value}).`
  return 'Empty: your rune page stays as it is.'
})

// ----- save / remove -----
const save = useBusy()
const remove = useBusy()
const dirty = computed(() => !samePreset(draft, saved.value ?? emptyPreset(group.value, shown.value.role)))
const canSave = computed(() => dirty.value && !halfSpells.value && !save.busy.value && !remove.busy.value)

function onSave() {
  if (!canSave.value) return
  tapFeedback()
  // Blind pick shows no ban list but keeps whatever the preset already had; ARAM has neither list.
  const aram = group.value === 'aram'
  const preset: AutoPickPreset = {
    ...draft,
    banChampionIds: aram ? [] : [...draft.banChampionIds],
    pickChampionIds: aram ? [] : [...draft.pickChampionIds],
  }
  void save.run(async () => {
    if (await saveAutoPickPreset(preset)) {
      haptic('success')
      emit('close')
    }
  })
}

function onRemove() {
  tapFeedback()
  void remove.run(async () => {
    if (await removeAutoPickPreset(group.value, shown.value.role)) {
      haptic('success')
      emit('close')
    }
  })
}

/** Unsaved edits: the sheet asks before it goes (Sheet `confirmClose`). Not while a save is closing it anyway. */
const confirmClose = computed(() => dirty.value && !save.busy.value && !remove.busy.value)
const discardOpen = ref(false)
watch(open, (o) => {
  if (!o) discardOpen.value = false
})

const saveLabel = computed(() => (save.busy.value ? 'Saving…' : 'Save'))
</script>

<template>
  <Sheet
    v-model:open="open"
    modal
    :detents="['large']"
    :title="title"
    body-class="lr-sheet-body--lists"
    :confirm-close="confirmClose"
    @close-request="discardOpen = true"
  >
    <template #head-trailing>
      <LrButton
        variant="prominent"
        :icon="CheckIcon"
        :aria-label="saveLabel"
        :loading="save.busy.value"
        :disabled="!canSave"
        @click="onSave"
      />
    </template>

    <ChampionListSection v-if="hasBans(group)" v-model="bans" header="Bans" kind="ban" :max="AutoPickMaxBans" :footer="banFooter" />
    <ChampionListSection v-if="hasPicks(group)" v-model="picks" header="Picks" kind="pick" :max="AutoPickMaxPicks" :footer="pickFooter" />

    <ListSection header="Summoner Spells">
      <SpellSlots
        :spell1-id="draft.spell1Id"
        :spell2-id="draft.spell2Id"
        :spells="spellChoices"
        :placeholder="spellPlaceholder"
        @change="setSpells"
      />
      <ListRow v-if="spellsSet" title="Clear Spells" variant="accent" tappable text @click="clearSpells" />
      <template #footer>
        <StatusLine v-if="halfSpells" tone="red" text="Choose both spells, or clear them." role="alert" />
        <span v-else>{{ spellFooter }}</span>
      </template>
    </ListSection>

    <ListSection :footer="runeFooter">
      <ListRow
        v-if="runePages.length"
        title="Rune Page"
        :icon="CircleDotIcon"
        tile-color="indigo"
        :value="runeValue"
        :menu="runeItems"
        menu-title="Rune Page"
      />
      <ListRow v-else title="Rune Page" :icon="CircleDotIcon" tile-color="indigo" :value="runeValue" disabled />
    </ListSection>

    <ListSection v-if="saved">
      <ListRow title="Remove Preset" variant="danger" tappable text :busy="remove.busy.value" @click="onRemove" />
    </ListSection>
  </Sheet>

  <LrAlert
    v-model:open="discardOpen"
    title="Discard Changes?"
    message="Your changes to this preset haven’t been saved."
    cancel-label="Keep Editing"
    action-label="Discard"
    destructive
    @action="emit('close')"
  />
</template>
