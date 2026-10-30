<script setup lang="ts">
import { CircleUserIcon, SearchIcon } from '@lucide/vue'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import ConnectionEmptyState from '../components/ConnectionEmptyState.vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListSection from '../components/lr/ListSection.vue'
import LrButton from '../components/lr/LrButton.vue'
import PlayerSearchSheet from '../components/profile/PlayerSearchSheet.vue'
import { connectionEmpty } from '../connection'
import { loadDdragonExtras } from '../ddragon'
import { nav } from '../navigation'
import { mastery, matchById, matches, ranks, refreshProfile, summoner } from '../profile'
import { onAuthenticated } from '../hub'
import { liveProfile, liveState, store } from '../store'
import ChampionStatsSection from './profile/ChampionStatsSection.vue'
import FormSection from './profile/FormSection.vue'
import MasterySection from './profile/MasterySection.vue'
import MatchDetail from './profile/MatchDetail.vue'
import MatchHistorySection from './profile/MatchHistorySection.vue'
import ProfileHeader from './profile/ProfileHeader.vue'
import RankedSection from './profile/RankedSection.vue'
import RoleStatsSection from './profile/RoleStatsSection.vue'

/**
 * Profile tab: your Riot ID and level, ranks, recent form, champions and roles over your last games (profileStats.ts),
 * champion mastery and match history → one game's details as a pushed
 * view (`nav.profileGameId`; App.vue shows the back button and hides the tab bar while one is open). Asks for a fresh
 * profile whenever the tab opens, the app comes back to the front or the phone signs in again. The search button on
 * the title row looks another player up by Riot ID (PlayerSearchSheet) and opens their profile over the tab.
 */
const searchOpen = ref(false)
const open = computed(() => matchById(nav.profileGameId))
// The game dropped out of the history (20 newer ones): back to the profile.
watch(
  () => nav.profileGameId != null && !open.value && !!liveProfile.value,
  (gone) => {
    if (gone) nav.profileGameId = null
  },
  { immediate: true },
)

function onVisible() {
  if (document.visibilityState === 'visible') refreshProfile()
}
let offAuthenticated = () => {}
onMounted(() => {
  refreshProfile()
  void loadDdragonExtras()
  offAuthenticated = onAuthenticated(refreshProfile)
  document.addEventListener('visibilitychange', onVisible)
})
onBeforeUnmount(() => {
  offAuthenticated()
  document.removeEventListener('visibilitychange', onVisible)
})
</script>

<template>
  <MatchDetail v-if="open" :key="open.gameId" :match="open" />

  <div v-else class="lr-content">
    <LargeTitle title="Profile">
      <!-- On the title row; docks into the nav bar once the title collapses. Only once the PC answers: it does the search. -->
      <template v-if="liveProfile && !connectionEmpty" #trailing>
        <LrButton variant="glass" :icon="SearchIcon" aria-label="Search Players" aria-haspopup="dialog" @click="searchOpen = true" />
      </template>
    </LargeTitle>

    <ConnectionEmptyState />
    <template v-if="connectionEmpty" />
    <template v-else-if="!liveProfile">
      <EmptyState v-if="store.syncing.profile" busy title="Loading Profile" />
      <EmptyState
        v-else
        :icon="CircleUserIcon"
        title="Profile Unavailable"
        message="Your PC hasn’t sent your profile yet."
        action-label="Try Again"
        @action="refreshProfile"
      />
    </template>
    <EmptyState
      v-else-if="!summoner"
      :icon="CircleUserIcon"
      :title="liveState?.lcuConnected ? 'Signing In to League' : 'Waiting for League'"
      message="Your profile shows up once you’re signed in to League on your PC."
    />

    <template v-else>
      <ListSection bare><ProfileHeader :summoner="summoner" /></ListSection>
      <RankedSection :ranks="ranks" />
      <FormSection :matches="matches" />
      <ChampionStatsSection :matches="matches" />
      <RoleStatsSection :matches="matches" />
      <MasterySection v-if="mastery" :mastery="mastery" />
      <MatchHistorySection :matches="matches" />
    </template>

    <PlayerSearchSheet v-model:open="searchOpen" />
  </div>
</template>
