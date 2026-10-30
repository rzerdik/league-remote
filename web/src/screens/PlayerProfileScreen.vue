<script setup lang="ts">
import { CircleAlertIcon } from '@lucide/vue'
import { computed, onMounted, watch } from 'vue'
import EmptyState from '../components/lr/EmptyState.vue'
import LargeTitle from '../components/lr/LargeTitle.vue'
import ListSection from '../components/lr/ListSection.vue'
import { loadDdragonExtras } from '../ddragon'
import { closePlayerMatch, nav } from '../navigation'
import { playerMatchById } from '../profile'
import { playerShortName } from '../riotId'
import ChampionStatsSection from './profile/ChampionStatsSection.vue'
import FormSection from './profile/FormSection.vue'
import MasterySection from './profile/MasterySection.vue'
import MatchDetail from './profile/MatchDetail.vue'
import MatchHistorySection from './profile/MatchHistorySection.vue'
import ProfileHeader from './profile/ProfileHeader.vue'
import RankedSection from './profile/RankedSection.vue'
import RoleStatsSection from './profile/RoleStatsSection.vue'
import { usePlayerProfile } from './profile/usePlayerProfile'

/**
 * Another player's profile (View Profile from a friend, a party member or a champ-select teammate, a teammate's row in a
 * game, or a Riot ID search), pushed over the current screen (`nav.player`; App.vue shows Back and hides the tab bar):
 * the same parts as your Profile tab — header (no XP), ranks, recent form, champions, roles, mastery, match history — and
 * one of their games as a pushed game view, seen from their side (their named teammates open over it). The connector
 * decides who may be opened; a refusal shows here with Try Again.
 */
const props = defineProps<{ puuid: string; name: string; gameId: number | null }>()

const { profile, status, error, load } = usePlayerProfile(() => props.puuid)
const title = computed(() => profile.value?.summoner.gameName || playerShortName(props.name) || 'Player')
const openGame = computed(() => playerMatchById(props.puuid, props.gameId))

// The game dropped out of their history (a reread with 20 newer ones): back to the profile.
watch(
  () => props.gameId != null && !!profile.value && !openGame.value,
  (gone) => {
    if (gone && nav.player) closePlayerMatch()
  },
  { immediate: true },
)

onMounted(() => void loadDdragonExtras())
</script>

<template>
  <MatchDetail
    v-if="openGame && profile"
    :key="openGame.gameId"
    :match="openGame"
    :puuid="puuid"
    :player-name="profile.summoner.gameName"
  />

  <div v-else class="lr-content lr-swipe-follow">
    <LargeTitle :title="title" />

    <EmptyState
      v-if="status === 'error'"
      :icon="CircleAlertIcon"
      title="Can’t Show This Profile"
      :message="error"
      action-label="Try Again"
      @action="load"
    />
    <EmptyState v-else-if="status === 'loading' || !profile" busy title="Loading Profile" />

    <template v-else>
      <ListSection bare><ProfileHeader :summoner="profile.summoner" :xp="false" /></ListSection>
      <RankedSection :ranks="profile.ranks" />
      <FormSection :matches="profile.matches" />
      <ChampionStatsSection :matches="profile.matches" />
      <RoleStatsSection :matches="profile.matches" />
      <MasterySection :mastery="profile.mastery" />
      <MatchHistorySection :matches="profile.matches" player />
    </template>
  </div>
</template>
