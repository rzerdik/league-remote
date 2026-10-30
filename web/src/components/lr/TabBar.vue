<script setup lang="ts">
import { CircleUserIcon, Gamepad2Icon, MessageCircleIcon, SettingsIcon, UsersIcon } from '@lucide/vue'
import type { Component } from 'vue'
import { badges } from '../../badges'
import { haptic } from '../../haptics'
import { nav, selectTab, type Tab } from '../../navigation'
import Badge from './Badge.vue'

/**
 * iOS-style tab bar: a floating glass capsule at the bottom with icon + label per tab, the selected one tinted. Tabs
 * with unread items show a count badge. Hidden while a modal sheet is up (never glass on glass) and, by App.vue,
 * during champion select, where the picker and Lock In own the bottom of the screen.
 */
const ITEMS: { tab: Tab; label: string; icon: Component }[] = [
  { tab: 'home', label: 'Home', icon: Gamepad2Icon },
  { tab: 'profile', label: 'Profile', icon: CircleUserIcon },
  { tab: 'chat', label: 'Chat', icon: MessageCircleIcon },
  { tab: 'friends', label: 'Friends', icon: UsersIcon },
  { tab: 'settings', label: 'Settings', icon: SettingsIcon },
]

function choose(tab: Tab) {
  if (tab !== nav.tab) haptic('selection')
  selectTab(tab)
}
</script>

<template>
  <!-- In <body>, beside the teleported sheets, like the action bar. -->
  <Teleport to="body">
    <nav class="lr-tabbar lr-glass" aria-label="Tabs">
      <button
        v-for="item in ITEMS"
        :key="item.tab"
        type="button"
        class="lr-tab"
        :class="{ 'is-selected': nav.tab === item.tab }"
        :aria-current="nav.tab === item.tab ? 'page' : undefined"
        :aria-label="badges[item.tab] > 0 ? `${item.label}, ${badges[item.tab]} unread` : undefined"
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
