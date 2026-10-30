<script setup lang="ts">
import { computed } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import StatGrid from '../../components/lr/StatGrid.vue'
import Tag from '../../components/lr/Tag.vue'
import TimerText from '../../components/lr/TimerText.vue'
import LoadoutStrip from '../../components/profile/LoadoutStrip.vue'
import type { LiveActivePlayer } from '../../contracts'
import { formatGold, kda, kdaRatio, kdaSpoken } from '../../format'
import { champName, csPerMinute, levelLine, liveHasTrinkets, loadoutOf, respawnLeft } from '../../liveGame'

/**
 * You in the running game: champion, level and position (dead: a gray portrait and the respawn countdown), then
 * K / D / A, the ratio, CS and CS per minute, unspent gold and (`rift`: Summoner's Rift; ARAM and Arena have no wards)
 * vision as a stat grid, then spells, keystone and items.
 */
const props = defineProps<{ me: LiveActivePlayer; rift: boolean }>()

const p = computed(() => props.me.player)
const respawn = computed(() => respawnLeft(p.value))

const cells = computed(() => {
  const perMinute = csPerMinute(p.value)
  return [
    { label: 'K / D / A', value: kda(p.value), spoken: kdaSpoken(p.value) },
    { label: 'KDA', value: kdaRatio(p.value, { withWord: false }) },
    { label: 'CS', value: String(p.value.creepScore) },
    ...(perMinute ? [{ label: 'CS / Min', value: perMinute, spoken: `${perMinute} per minute` }] : []),
    { label: 'Gold', value: formatGold(props.me.currentGold), accent: true },
    ...(props.rift ? [{ label: 'Vision', value: String(Math.round(p.value.wardScore)) }] : []),
  ]
})
</script>

<template>
  <ListSection header="You">
    <ListRow variant="you" wrap :title="champName(p)" :subtitle="levelLine(p)">
      <template #lead>
        <ChampionIcon :champion-id="p.championId" :state="p.isDead ? 'dead' : 'you'" decorative />
      </template>
      <template v-if="p.isDead" #trailing>
        <Tag tone="red">Dead · <TimerText :seconds="respawn" /></Tag>
      </template>
    </ListRow>
    <StatGrid :cells="cells" />
    <ListRow>
      <LoadoutStrip class="you-loadout" :player="loadoutOf(p)" loadout :no-trinket="!liveHasTrinkets" />
    </ListRow>
  </ListSection>
</template>

<style scoped>
/* Fills the row from the start (a row's content otherwise sits at its end, like a value). */
.you-loadout {
  flex: 1;
}
</style>
