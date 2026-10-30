<script setup lang="ts">
import { XIcon } from '@lucide/vue'
import { nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { pushModalLayer } from '../../composables/useModalLayer'
import { sheetStack, useSheet, type SheetDetent } from '../../composables/useSheet'
import { scaled, SIZE, SPACE } from '../../styles/tokens'
import LrButton from './LrButton.vue'

export type DetentId = 'peek' | 'medium' | 'large'

/**
 * Bottom sheet with detents. Non-modal (Champion Select picker): floats over the screen, no scrim. Modal (spell
 * picker, role and mode pickers, chat, Pair Again): a modal layer — scrim, everything else inert, focus moves in and
 * comes back on close — that closes by swipe down, the xmark button, a scrim tap and Escape. At partial detents it is
 * floating glass; at `large` it docks opaque. `confirmClose` (a modal editor with unsaved changes, like iOS's
 * isModalInPresentation): a swipe rubber-bands back and every way of closing emits `closeRequest` instead, so the
 * parent can ask "Discard Changes?" and close it itself.
 */
const props = withDefaults(
  defineProps<{
    open: boolean
    detents?: DetentId[]
    /** Initial / controlled detent (v-model:detent). */
    detent?: DetentId
    modal?: boolean
    title?: string
    /** Accessible name when there is no visible title. */
    label?: string
    closable?: boolean
    grabber?: boolean
    /** Fraction of the viewport for `medium`. */
    mediumFraction?: number
    /** px at the default text size for `peek` (scaled with Dynamic Type). */
    peekHeight?: number
    bodyClass?: string
    confirmClose?: boolean
  }>(),
  {
    detents: () => ['large'],
    modal: false,
    closable: true,
    grabber: undefined,
    mediumFraction: 0.55,
    peekHeight: SIZE.sheetPeek,
  },
)
const emit = defineEmits<{
  'update:open': [open: boolean]
  'update:detent': [detent: DetentId]
  closed: []
  closeRequest: []
}>()

const el = ref<HTMLElement | null>(null)
const scrim = ref<HTMLElement | null>(null)
const body = ref<HTMLElement | null>(null)
const titleId = useId()
const rendered = ref(false)
const scrimVisible = ref(false)
const viewport = ref({ h: window.innerHeight, safeTop: 0 })

function measure() {
  const probe = document.createElement('div')
  probe.style.cssText = 'position:fixed;top:0;height:env(safe-area-inset-top,0px);visibility:hidden;pointer-events:none'
  document.body.appendChild(probe)
  const safeTop = probe.getBoundingClientRect().height
  probe.remove()
  viewport.value = { h: window.innerHeight, safeTop }
}

function heights(): SheetDetent[] {
  const { h, safeTop } = viewport.value
  // Large: below the status bar, with a strip of the screen behind still showing (iOS page sheet).
  const large = Math.round(h - safeTop - scaled(SPACE.space4) - SPACE.space2)
  const all: Record<DetentId, number> = {
    peek: Math.min(large, Math.round(scaled(props.peekHeight))),
    medium: Math.min(large, Math.round(h * props.mediumFraction)),
    large,
  }
  return props.detents.map((id) => ({ id, height: all[id] }))
}

const sheet = useSheet({
  el,
  body,
  detents: heights,
  dismissible: () => props.modal && !props.confirmClose,
  onDetent: (id) => {
    if (id !== props.detent) emit('update:detent', id as DetentId)
  },
  onDismiss: () => emit('update:open', false),
  onDismissBlocked: () => {
    if (props.modal && props.confirmClose) emit('closeRequest')
  },
})

let unbindBody: (() => void) | null = null
let releaseLayer: (() => void) | null = null

/** A modal sheet is a modal layer while it is up (not while it leaves: focus goes back at once). */
function setModal(on: boolean) {
  if (!props.modal || on === !!releaseLayer) return
  sheetStack.modal += on ? 1 : -1
  if (on && el.value) releaseLayer = pushModalLayer([el.value, ...(scrim.value ? [scrim.value] : [])], el.value)
  else {
    releaseLayer?.()
    releaseLayer = null
  }
}

async function show() {
  measure()
  if (rendered.value) {
    // Reopened while still leaving: turn around from where it is (snapTo stops the hide spring and its callback).
    setModal(true)
    scrimVisible.value = true
    if (el.value) el.value.style.opacity = '1'
    sheet.snapTo(props.detent ?? props.detents[0] ?? 'large')
    return
  }
  rendered.value = true
  await nextTick()
  if (body.value) unbindBody = sheet.bindBody(body.value)
  setModal(true)
  scrimVisible.value = true
  sheet.present(props.detent ?? props.detents[0] ?? 'large')
}

function hide() {
  scrimVisible.value = false
  setModal(false)
  sheet.hide(() => {
    unbindBody?.()
    unbindBody = null
    rendered.value = false
    emit('closed')
  })
}

watch(
  () => props.open,
  (open) => (open ? void show() : hide()),
)
watch(
  () => props.detent,
  (d) => {
    if (d && rendered.value && props.open && d !== sheet.detent.value) sheet.snapTo(d)
  },
)

function onResize() {
  measure()
  sheet.relayout()
}
onMounted(() => {
  window.addEventListener('resize', onResize)
  if (props.open) void show()
})
onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  unbindBody?.()
  setModal(false)
})

function close() {
  if (props.confirmClose) emit('closeRequest')
  else emit('update:open', false)
}
function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && props.modal) {
    e.stopPropagation()
    close()
  }
}

const showGrabber = () => props.grabber ?? (props.detents.length > 1 || props.modal)
</script>

<template>
  <Teleport to="body">
    <div v-if="modal && rendered" ref="scrim" class="lr-scrim" :data-visible="scrimVisible" aria-hidden="true" @click="close" />
    <section
      v-if="rendered"
      ref="el"
      class="lr-sheet"
      :data-detent="sheet.detent.value"
      :role="modal ? 'dialog' : 'region'"
      :aria-modal="modal || undefined"
      :aria-label="title ? undefined : label"
      :aria-labelledby="title ? titleId : undefined"
      tabindex="-1"
      @keydown="onKeydown"
    >
      <div
        class="lr-sheet-drag"
        @pointerdown="sheet.handle.onPointerdown"
        @pointermove="sheet.handle.onPointermove"
        @pointerup="sheet.handle.onPointerup"
        @pointercancel="sheet.handle.onPointercancel"
      >
        <button
          v-if="showGrabber()"
          type="button"
          class="lr-grabber"
          :aria-label="`Resize ${title ?? label ?? 'sheet'}`"
          @click="sheet.cycle"
        />
        <div v-if="title || (modal && closable)" class="lr-sheet-head">
          <LrButton v-if="modal && closable" variant="glass" :icon="XIcon" aria-label="Close" @click="close" />
          <span v-else />
          <h2 v-if="title" :id="titleId" class="headline">{{ title }}</h2>
          <span v-else />
          <span><slot name="head-trailing" /></span>
        </div>
        <slot name="header" />
      </div>
      <div ref="body" class="lr-sheet-body" :class="bodyClass"><slot /></div>
      <!-- Pinned under the body (a chat composer): rides the on-screen keyboard. -->
      <div v-if="$slots.footer" class="lr-sheet-foot"><slot name="footer" /></div>
    </section>
  </Teleport>
</template>
