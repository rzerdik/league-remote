<script setup lang="ts">
import { CircleMinusIcon, MenuIcon } from '@lucide/vue'
import { computed, ref } from 'vue'
import { useDragReorder } from '../../composables/useDragReorder'
import { championName } from '../../ddragon'
import { tapFeedback } from '../../haptics'
import ChampionIcon from '../lr/ChampionIcon.vue'
import ListRow from '../lr/ListRow.vue'
import ListSection from '../lr/ListSection.vue'
import LrButton from '../lr/LrButton.vue'
import ChampionPickerSheet from './ChampionPickerSheet.vue'

/**
 * A preset's ban or pick list in priority order (v-model): one row per champion, Remove leading (as in an iOS list
 * being edited) and the reorder grip trailing: drag a row by it to its new place (pointer and touch). The grip is also
 * an adjustable slider for keyboards and VoiceOver (position "2 of 5"; Up / Down arrow or swipe moves the row), so
 * there are no Move Up / Move Down buttons. Then "Add Ban…" / "Add Pick…" (champion picker sheet) until `max`.
 * The header counts "2 of 5".
 */
const props = defineProps<{ header: string; max: number; kind: 'ban' | 'pick'; footer?: string }>()
const ids = defineModel<number[]>({ required: true })

const pickerOpen = ref(false)
const full = computed(() => ids.value.length >= props.max)
const addLabel = computed(() => (props.kind === 'ban' ? 'Add Ban…' : 'Add Pick…'))

/**
 * Keyboard / VoiceOver reorder: Up / Left move the row one place earlier, Down / Right one later, Home / End to the
 * first / last place. The grip keeps focus (the row element is reused).
 */
function onGripKey(e: KeyboardEvent, i: number) {
  if (e.metaKey || e.ctrlKey || e.altKey) return
  const last = ids.value.length - 1
  let j: number
  if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') j = i - 1
  else if (e.key === 'ArrowDown' || e.key === 'ArrowRight') j = i + 1
  else if (e.key === 'Home') j = 0
  else if (e.key === 'End') j = last
  else return
  e.preventDefault()
  if (j < 0 || j > last || j === i) return
  tapFeedback()
  const next = [...ids.value]
  next.splice(j, 0, ...next.splice(i, 1))
  ids.value = next
}
const reorder = useDragReorder((from, to) => {
  const next = [...ids.value]
  next.splice(to, 0, ...next.splice(from, 1))
  ids.value = next
})
const grip = reorder.handlers
function remove(i: number) {
  tapFeedback()
  ids.value = ids.value.filter((_, k) => k !== i)
}
function add(id: number) {
  if (full.value || ids.value.includes(id)) return
  ids.value = [...ids.value, id]
}
</script>

<template>
  <ListSection :header="header" :header-trailing="`${ids.length} of ${max}`" :footer="footer">
    <!-- While a row is dragged it moves itself: no move transition from the group then. -->
    <TransitionGroup name="lr-rows" :move-class="reorder.active.value ? 'autopick-moved' : undefined">
      <ListRow v-for="(id, i) in ids" :key="id" :title="championName(id)" :data-reorder-index="i" :class="reorder.rowClass(i)">
        <template #lead>
          <span class="autopick-lead">
            <LrButton variant="plain" danger size="sm" :icon="CircleMinusIcon" :aria-label="`Remove ${championName(id)}`" @click="remove(i)" />
            <ChampionIcon :champion-id="id" size="tile" decorative />
          </span>
        </template>
        <template #trailing>
          <span class="lr-row-group">
            <span
              v-if="ids.length > 1"
              class="autopick-grip"
              role="slider"
              tabindex="0"
              aria-orientation="vertical"
              :aria-label="`Reorder ${championName(id)}`"
              :aria-valuemin="1"
              :aria-valuemax="ids.length"
              :aria-valuenow="i + 1"
              :aria-valuetext="`${i + 1} of ${ids.length}`"
              @keydown="onGripKey($event, i)"
              @pointerdown="grip.onPointerdown($event, i)"
              @pointermove="grip.onPointermove"
              @pointerup="grip.onPointerup"
              @pointercancel="grip.onPointercancel"
              @touchstart.stop
              @touchmove.stop.prevent
            >
              <MenuIcon class="autopick-grip-symbol" :stroke-width="2" />
            </span>
          </span>
        </template>
      </ListRow>
    </TransitionGroup>
    <ListRow v-if="!full" :title="addLabel" tappable text @click="pickerOpen = true" />
  </ListSection>

  <ChampionPickerSheet
    v-model:open="pickerOpen"
    :title="kind === 'ban' ? 'Add Ban' : 'Add Pick'"
    :tone="kind === 'ban' ? 'red' : 'accent'"
    :exclude="ids"
    @select="add"
  />
</template>

<style scoped>
.autopick-lead {
  display: inline-flex;
  align-items: center;
}
/* The reorder grip (iOS's three lines): a 44-pt hit region around the symbol; a drag there never scrolls or moves the sheet. */
.autopick-grip {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--symbol);
  height: var(--control-sm);
  color: var(--label-tertiary);
  touch-action: none;
  cursor: grab;
}
.autopick-grip::after {
  content: '';
  position: absolute;
  inset: calc((var(--control-sm) - var(--hit-min)) / 2) calc((var(--symbol) - var(--hit-min)) / 2);
}
.autopick-grip-symbol {
  width: var(--symbol-sm);
  height: var(--symbol-sm);
}
/* The lifted row follows the finger above the others; the others step aside. */
.is-dragging {
  position: relative;
  z-index: 1;
  background: var(--bg-elevated);
  box-shadow: var(--glass-lift);
  cursor: grabbing;
}
.is-shifting {
  transition: transform var(--dur-snappy) var(--spring-snappy);
}
@media (prefers-reduced-motion: reduce) {
  .is-shifting {
    transition: none;
  }
}
/* The minus glyph sits near the row's leading inset, as in an iOS list being edited (its hit region stays 44 pt). */
.autopick-lead > .lr-btn {
  margin-left: calc(-1 * var(--space-2));
}
</style>
