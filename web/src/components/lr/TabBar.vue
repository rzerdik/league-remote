<script setup lang="ts">
import { CircleUserIcon, Gamepad2Icon, MessageCircleIcon, SettingsIcon, UsersIcon } from '@lucide/vue'
import { computed, onBeforeUnmount, onMounted, ref, watch, type Component } from 'vue'
import { badges } from '../../badges'
import { haptic } from '../../haptics'
import { nav, selectTab, type Tab } from '../../navigation'
import Badge from './Badge.vue'

/**
 * iOS-style tab bar: a floating glass capsule at the bottom with icon + label per tab, one selection pill that slides
 * between them. Tabs with unread items show a count badge. Hidden while a modal sheet is up (never glass on glass) and,
 * by App.vue, during champion select, where the picker and Lock In own the bottom of the screen; a ready check covers it.
 * Scrolling down minimizes it to the current tab at the leading edge; scrolling up or tapping that tab brings it back.
 */
const ITEMS: { tab: Tab; label: string; icon: Component }[] = [
  { tab: 'home', label: 'Home', icon: Gamepad2Icon },
  { tab: 'profile', label: 'Profile', icon: CircleUserIcon },
  { tab: 'chat', label: 'Chat', icon: MessageCircleIcon },
  { tab: 'friends', label: 'Friends', icon: UsersIcon },
  { tab: 'settings', label: 'Settings', icon: SettingsIcon },
]
const selectedIndex = computed(() => Math.max(0, ITEMS.findIndex((i) => i.tab === nav.tab)))

/** Scroll travel (px) in one direction before the bar changes: a finger resting on the list doesn't flip it. */
const MINIMIZE_TRAVEL = 24
const minimized = ref(false)
let scroller: HTMLElement | null = null
let anchor = 0

function onScroll() {
  if (!scroller) return
  // Rubber-banding past either end isn't a change of direction.
  const top = Math.min(scroller.scrollHeight - scroller.clientHeight, Math.max(0, scroller.scrollTop))
  if (top <= 0) {
    minimized.value = false
    anchor = 0
    return
  }
  const travel = top - anchor
  // The anchor follows the scroll while it keeps going the way the bar already went, so a turn is measured from there.
  if (minimized.value ? travel > 0 : travel < 0) anchor = top
  else if (Math.abs(travel) >= MINIMIZE_TRAVEL) {
    minimized.value = travel > 0
    anchor = top
  }
}
onMounted(() => {
  scroller = document.getElementById('app')
  anchor = scroller?.scrollTop ?? 0
  scroller?.addEventListener('scroll', onScroll, { passive: true })
})
onBeforeUnmount(() => scroller?.removeEventListener('scroll', onScroll))
watch(
  () => nav.tab,
  () => (minimized.value = false),
)

function choose(tab: Tab) {
  if (minimized.value) {
    minimized.value = false
    anchor = scroller?.scrollTop ?? 0
    return
  }
  if (tab !== nav.tab) haptic('selection')
  selectTab(tab)
}

function tabLabel(item: (typeof ITEMS)[number]): string | undefined {
  const count = badges.value[item.tab]
  const unread = count > 0 ? `${item.label}, ${count} unread` : item.label
  if (minimized.value) return `${unread}, Show All Tabs`
  return count > 0 ? unread : undefined
}
</script>

<template>
  <!-- In <body>, beside the teleported sheets, like the action bar. -->
  <Teleport to="body">
    <nav class="lr-tabbar lr-glass" :class="{ 'is-minimized': minimized }" aria-label="Tabs" :style="{ '--lr-tab-index': minimized ? 0 : selectedIndex }">
      <span class="lr-tab-pill" aria-hidden="true" />
      <button
        v-for="item in ITEMS"
        v-show="!minimized || nav.tab === item.tab"
        :key="item.tab"
        type="button"
        class="lr-tab lr-glass-interactive"
        :class="{ 'is-selected': nav.tab === item.tab }"
        :aria-current="nav.tab === item.tab ? 'page' : undefined"
        :aria-label="tabLabel(item)"
        @click="choose(item.tab)"
      >
        <span class="lr-tab-icon">
          <component :is="item.icon" :stroke-width="nav.tab === item.tab ? 2.25 : 1.75" aria-hidden="true" />
          <Badge :count="badges[item.tab]" on aria-hidden="true" />
        </span>
        <span class="tab-label">{{ item.label }}</span>
      </button>
    </nav>
  </Teleport>
</template>
