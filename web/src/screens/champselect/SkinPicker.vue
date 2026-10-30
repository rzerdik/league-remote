<script setup lang="ts">
import { CheckIcon, ShuffleIcon, StarIcon } from '@lucide/vue'
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import LrButton from '../../components/lr/LrButton.vue'
import Sheet from '../../components/lr/Sheet.vue'
import { useBusy } from '../../composables/useBusy'
import type { ChampionSkin, SkinSelection } from '../../contracts'
import { skinArtUrl } from '../../ddragon'
import { favouriteSkinId, setFavouriteSkin } from '../../favouriteSkins'
import { haptic, tapFeedback } from '../../haptics'
import { reduceMotion } from '../../lib/spring'
import { vImgLoaded } from '../../lib/vImgLoaded'
import { sendCommand } from '../../hub'
import { chromaLabel, findSkin, randomChoice, swatchBackground } from './skins'

/**
 * Skins: a modal, full-height sheet with your equippable skins for the locked-in champion as a row of Data Dragon
 * loading-art cards you swipe through (CSS scroll snap, so the momentum is the platform's), the centred skin's chromas
 * under it and Equip in the footer. Swiping only browses; the centred card, a chroma or Equip sends SelectSkin, and
 * a side card scrolls to the centre first. The equipped id comes from the state: our own choice shows at once
 * (`pending`) and is dropped when League echoes it back or the command fails. Favourite marks the centred skin (with
 * the chroma you wear, when it's the skin you wear) as this champion's favourite on this phone: Loadout then offers it
 * after lock-in. Marking one sends nothing.
 */
const props = defineProps<{ selection: SkinSelection; open: boolean }>()
const emit = defineEmits<{ 'update:open': [open: boolean] }>()

const skins = computed(() => props.selection.skins)

// ----- equipped (optimistic) -----
const pending = ref<number | null>(null)
const equipping = useBusy()
const equippedId = computed(() => pending.value ?? props.selection.selectedSkinId)
/** The skin you wear, whichever of its chromas. */
const wornSkinId = computed(() => findSkin(props.selection, equippedId.value)?.skin.id ?? 0)

// Every state update (a new selection object, even when the id didn't change): drop ours once League shows the one we
// sent last, or whenever nothing of ours is in flight — so a change made on the PC while a send was out can't leave a
// stale choice behind.
watch(
  () => props.selection,
  (s) => {
    if (s.selectedSkinId === pending.value || !equipping.busy.value) pending.value = null
  },
)

/** Sends choices one at a time; taps made meanwhile collapse into the latest, which is sent next. */
async function sendChoices() {
  let sent = 0
  while (pending.value && pending.value !== sent) {
    const skinId = pending.value
    sent = skinId
    const r = await sendCommand({ type: 'SelectSkin', skinId })
    if (r?.success) haptic('success')
    else if (pending.value === skinId) pending.value = null
  }
}
function equip(skinId: number) {
  if (skinId <= 0 || skinId === equippedId.value) return
  tapFeedback()
  pending.value = skinId
  void equipping.run(sendChoices) // already running: it picks this choice up when the current one returns
}

// ----- the carousel -----
const track = ref<HTMLElement | null>(null)
const viewIndex = ref(0)
const viewed = computed<ChampionSkin | null>(() => skins.value[viewIndex.value] ?? skins.value[0] ?? null)
const viewedWorn = computed(() => !!viewed.value && viewed.value.id === wornSkinId.value)
const viewedChromaName = computed(() => {
  const found = findSkin(props.selection, equippedId.value)
  return found?.chroma && found.skin.id === viewed.value?.id ? chromaLabel(found.skin, found.chroma) : ''
})

function cardAt(i: number): HTMLElement | null {
  return (track.value?.children[i] as HTMLElement | undefined) ?? null
}
function scrollToIndex(i: number, smooth: boolean) {
  const t = track.value
  const card = cardAt(i)
  if (!t || !card) return
  t.scrollTo({
    left: card.offsetLeft - (t.clientWidth - card.offsetWidth) / 2,
    behavior: smooth && !reduceMotion.value ? 'smooth' : 'instant',
  })
  viewIndex.value = i
}

/** The card nearest the track's centre is the one you're looking at. */
let frame = 0
function onScroll() {
  cancelAnimationFrame(frame)
  frame = requestAnimationFrame(() => {
    const t = track.value
    if (!t) return
    const centre = t.scrollLeft + t.clientWidth / 2
    let best = 0
    let bestDistance = Infinity
    for (let i = 0; i < t.children.length; i++) {
      const c = t.children[i] as HTMLElement
      const distance = Math.abs(c.offsetLeft + c.offsetWidth / 2 - centre)
      if (distance < bestDistance) [best, bestDistance] = [i, distance]
    }
    viewIndex.value = best
  })
}
onBeforeUnmount(() => cancelAnimationFrame(frame))

/** Opens on the skin you wear. The track exists once the sheet has rendered its content. */
watch(
  () => [track.value, props.open] as const,
  async ([el, open]) => {
    if (!el || !open) return
    await nextTick()
    const i = Math.max(0, skins.value.findIndex((s) => s.id === wornSkinId.value))
    scrollToIndex(i, false)
  },
)
// A new carousel (a trade, a reroll) may be shorter.
watch(
  () => skins.value.length,
  (n) => {
    if (viewIndex.value >= n) viewIndex.value = Math.max(0, n - 1)
  },
)

function onCard(i: number, skin: ChampionSkin) {
  if (i !== viewIndex.value) return scrollToIndex(i, true)
  if (skin.id !== wornSkinId.value) equip(skin.id)
}

const canRandom = computed(() => randomChoice(props.selection, equippedId.value) !== null)
function equipRandom() {
  const id = randomChoice(props.selection, equippedId.value)
  const found = id ? findSkin(props.selection, id) : null
  if (!id || !found) return
  scrollToIndex(skins.value.indexOf(found.skin), true)
  equip(id)
}

// ----- favourite (this phone only) -----
const favouriteId = computed(() => favouriteSkinId(props.selection.championId))
/** The centred skin is the favourite (the skin itself or one of its chromas). */
const viewedFavourite = computed(
  () => !!viewed.value && findSkin(props.selection, favouriteId.value ?? 0)?.skin.id === viewed.value.id,
)
function toggleFavourite() {
  const skin = viewed.value
  if (!skin) return
  haptic('selection')
  if (viewedFavourite.value) return setFavouriteSkin(props.selection.championId, null)
  // The skin you wear keeps the chroma you wear it in; any other skin is remembered as it is.
  setFavouriteSkin(props.selection.championId, viewedWorn.value ? equippedId.value : skin.id)
}

function art(skin: ChampionSkin) {
  return skinArtUrl(props.selection.championId, skin.id) ?? undefined
}

const equipLabel = computed(() => {
  if (!viewed.value) return ''
  if (viewedWorn.value) return 'Equipped'
  return equipping.busy.value && findSkin(props.selection, pending.value ?? 0)?.skin.id === viewed.value.id
    ? 'Equipping…'
    : `Equip ${viewed.value.name}`
})
const statusLine = computed(() => {
  const position = `${viewIndex.value + 1} of ${skins.value.length}`
  if (!viewedWorn.value) return position
  return viewedChromaName.value ? `Equipped · ${viewedChromaName.value}` : `Equipped · ${position}`
})
</script>

<template>
  <Sheet
    :open="open"
    modal
    title="Skins"
    :detents="['large']"
    body-class="skins-sheet-body"
    @update:open="emit('update:open', $event)"
  >
    <template #head-trailing>
      <LrButton variant="glass" :icon="ShuffleIcon" aria-label="Equip a Random Skin" :disabled="!canRandom" @click="equipRandom" />
    </template>

    <ul ref="track" class="skins-track" aria-label="Your skins" @scroll.passive="onScroll">
      <li v-for="(skin, i) in skins" :key="skin.id" class="skins-item">
        <button
          type="button"
          class="skins-card"
          :class="{ 'is-viewed': i === viewIndex }"
          :aria-label="skin.name"
          :aria-pressed="skin.id === wornSkinId"
          @click="onCard(i, skin)"
        >
          <img
            v-if="art(skin)"
            v-img-loaded
            class="skins-art"
            :src="art(skin)"
            alt=""
            crossorigin="anonymous"
            loading="lazy"
            decoding="async"
            draggable="false"
          />
          <span v-if="skin.id === wornSkinId" class="skins-worn" aria-hidden="true">
            <CheckIcon :stroke-width="3" />
          </span>
        </button>
      </li>
    </ul>

    <div v-if="viewed" class="skins-info">
      <h3 class="title-3 skins-name">{{ viewed.name }}</h3>
      <p class="footnote lr-secondary" aria-live="polite">{{ statusLine }}</p>
      <LrButton
        class="skins-favourite"
        variant="gray"
        size="sm"
        :leading="StarIcon"
        :pressed="viewedFavourite"
        :aria-label="`Favourite ${viewed.name}`"
        @click="toggleFavourite"
      >
        <span>Favourite</span>
      </LrButton>

      <div v-if="viewed.chromas.length" class="skins-chromas" role="group" :aria-label="`Chromas for ${viewed.name}`">
        <button
          type="button"
          class="skins-swatch skins-swatch--base"
          :aria-pressed="equippedId === viewed.id"
          :aria-label="`${viewed.name}, no chroma`"
          @click="equip(viewed.id)"
        />
        <button
          v-for="c in viewed.chromas"
          :key="c.id"
          type="button"
          class="skins-swatch"
          :style="swatchBackground(c.colors) ? { background: swatchBackground(c.colors)! } : undefined"
          :aria-pressed="equippedId === c.id"
          :aria-label="c.name"
          @click="equip(c.id)"
        />
      </div>
    </div>

    <template #footer>
      <div v-if="viewed" class="skins-foot">
        <LrButton
          variant="prominent"
          size="lg"
          block
          :leading="viewedWorn ? CheckIcon : undefined"
          :loading="equipLabel === 'Equipping…'"
          :disabled="viewedWorn"
          @click="equip(viewed.id)"
        >
          <span>{{ equipLabel }}</span>
        </LrButton>
      </div>
    </template>
  </Sheet>
</template>

<style>
/* The sheet's body (outside this component's scope): the carousel runs edge to edge. */
.skins-sheet-body {
  padding: 0 0 var(--space-5);
}
</style>

<style scoped>
.skins-track {
  /* Cards are as tall as the token allows and the viewport leaves room for; their width follows the art (308:560). */
  --card-h: min(var(--skin-card-h), 56dvh);
  --card-w: calc(var(--card-h) * 308 / 560);
  display: flex;
  gap: var(--space-3);
  margin: 0;
  padding: var(--space-2) calc((100% - var(--card-w)) / 2) var(--space-4);
  list-style: none;
  overflow-x: auto;
  overscroll-behavior-x: contain;
  scroll-snap-type: x mandatory;
  scrollbar-width: none;
}
.skins-track::-webkit-scrollbar {
  display: none;
}
.skins-item {
  flex: none;
  scroll-snap-align: center;
}
.skins-card {
  position: relative;
  display: block;
  width: var(--card-w);
  height: var(--card-h);
  padding: 0;
  border: 0;
  border-radius: var(--radius-card);
  overflow: hidden;
  background: var(--fill-tertiary);
  box-shadow: var(--sheet-lift);
  cursor: pointer;
  transform: scale(0.92);
  opacity: 0.7;
  transition:
    transform var(--dur-snappy) var(--spring-snappy),
    opacity var(--dur-fade) var(--ease-fade);
}
.skins-card.is-viewed {
  transform: none;
  opacity: 1;
}
.skins-card[aria-pressed='true'] {
  --ring-gap: var(--lr-ring-gap);
  box-shadow:
    0 0 0 var(--ring-gap) var(--bg-elevated),
    0 0 0 calc(var(--ring-gap) + var(--lr-ring-thick)) var(--accent);
}
.skins-art {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
/* Shimmer until the art has loaded (v-img-loaded), like the other Data Dragon art. */
.skins-art:not(.is-loaded) {
  background-color: var(--fill-tertiary);
  background-image: linear-gradient(100deg, transparent 30%, var(--fill-secondary) 50%, transparent 70%);
  background-size: 200% 100%;
  animation: lr-shimmer var(--dur-shimmer) linear infinite;
}
.skins-worn {
  position: absolute;
  top: var(--space-3);
  right: var(--space-3);
  display: grid;
  place-items: center;
  width: var(--control-sm);
  height: var(--control-sm);
  border-radius: var(--radius-full);
  background: var(--accent);
  color: var(--on-accent);
}
.skins-worn > svg {
  width: var(--symbol-sm);
  height: var(--symbol-sm);
}
.skins-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-0-5);
  padding: 0 var(--space-5);
  text-align: center;
}
.skins-name {
  margin: 0;
  overflow-wrap: anywhere;
}
.skins-info > p {
  margin: 0;
}
.skins-favourite {
  margin-top: var(--space-2);
}
.skins-chromas {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  /* swatch + gap = hit-min, so the 44pt hit regions don't overlap */
  gap: var(--space-4);
  margin-top: var(--space-3);
}
.skins-swatch {
  position: relative;
  width: var(--chroma-swatch);
  height: var(--chroma-swatch);
  padding: 0;
  border: var(--lr-hairline) solid var(--separator);
  border-radius: var(--radius-full);
  background: var(--fill-secondary);
  cursor: pointer;
}
/* The skin without a chroma: an empty ring, as in the client. */
.skins-swatch--base {
  background: transparent;
  border: var(--lr-stroke) solid var(--label-secondary);
}
.skins-swatch[aria-pressed='true'] {
  box-shadow:
    0 0 0 var(--lr-ring-gap) var(--bg-elevated),
    0 0 0 calc(var(--lr-ring-gap) + var(--lr-stroke)) var(--accent);
}
/* 44pt hit region around the swatch. */
.skins-swatch::after {
  content: '';
  position: absolute;
  inset: calc((var(--hit-min) - var(--chroma-swatch)) / -2);
}
.skins-foot {
  padding: var(--space-3) var(--space-5) var(--space-2);
}
@media (prefers-reduced-motion: reduce) {
  .skins-card {
    transition: opacity var(--dur-fade) var(--ease-fade);
  }
}
</style>
