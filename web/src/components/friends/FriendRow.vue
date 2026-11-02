<script setup lang="ts">
import type { Friend } from '../../contracts'
import { profileIconUrl } from '../../ddragon'
import { friendStatus, presenceTone } from '../../friends'
import Avatar from '../lr/Avatar.vue'
import ListRow from '../lr/ListRow.vue'
import StatusLine from '../lr/StatusLine.vue'

/**
 * A friend as every list shows one (Friends tab, Invite Friends): avatar, name, and their presence as a status line
 * ("● In Game 12:30 · Ranked Solo/Duo"). Everything else (tappable, title, value, busy, click…) goes to the ListRow.
 */
defineOptions({ inheritAttrs: false })
defineProps<{ friend: Friend; title?: string }>()
</script>

<template>
  <ListRow v-bind="$attrs" :title="title ?? friend.gameName">
    <template #lead><Avatar :name="friend.gameName" :image-url="profileIconUrl(friend.profileIconId)" /></template>
    <template #subtitle><StatusLine :tone="presenceTone(friend)" :text="friendStatus(friend)" /></template>
    <template v-if="$slots.trailing" #trailing><slot name="trailing" /></template>
  </ListRow>
</template>
