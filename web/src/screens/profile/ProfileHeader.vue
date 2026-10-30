<script setup lang="ts">
import { computed } from 'vue'
import Avatar from '../../components/lr/Avatar.vue'
import LrProgress from '../../components/lr/LrProgress.vue'
import type { ProfileSummoner } from '../../contracts'
import { profileIconUrl } from '../../ddragon'
import { formatCount } from '../../format'
import { xpProgress } from '../../profile'
import { riotIdParts } from '../../riotId'

/**
 * The top of the Profile tab: profile icon, Riot ID ("Nova #EUW", the tag secondary), level and the XP bar to the
 * next one. One opaque card (content, not chrome). Another player's profile leaves the XP out (`xp` false), as the
 * client's does.
 */
const props = withDefaults(defineProps<{ summoner: ProfileSummoner; xp?: boolean }>(), { xp: true })

const id = computed(() => riotIdParts(props.summoner))
const progress = computed(() => (props.xp ? xpProgress(props.summoner) : null))
const xpLine = computed(() =>
  progress.value === null
    ? ''
    : `${formatCount(props.summoner.xpSinceLastLevel)} / ${formatCount(props.summoner.xpForNextLevel)} XP`,
)
</script>

<template>
  <div class="profile-header">
    <Avatar :name="summoner.gameName" :image-url="profileIconUrl(summoner.profileIconId)" size="lg" />
    <div class="profile-header-text">
      <p class="title-2 profile-header-name">
        {{ id.name }} <span v-if="id.tag" class="lr-secondary">{{ id.tag }}</span>
      </p>
      <p class="subheadline lr-secondary">Level {{ summoner.level }}</p>
      <template v-if="progress !== null">
        <LrProgress class="profile-header-xp" :value="progress" :label="`Progress to level ${summoner.level + 1}`" />
        <p class="caption-1 lr-secondary profile-header-xp-line">{{ xpLine }}</p>
      </template>
    </div>
  </div>
</template>

<style scoped>
.profile-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-4);
  padding: var(--space-5);
  background: var(--bg-elevated);
  border-radius: var(--radius-card);
}
/* Beside the icon; under it at large text sizes (about nine body characters wide at least). */
.profile-header-text {
  flex: 1 1 calc(var(--font-size-body) * 9);
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-0-5);
}
.profile-header-name {
  overflow-wrap: break-word;
}
.profile-header-xp {
  margin-top: var(--space-2);
}
.profile-header-xp-line {
  font-variant-numeric: tabular-nums;
}
</style>
