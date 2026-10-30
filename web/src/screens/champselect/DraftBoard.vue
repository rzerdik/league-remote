<script setup lang="ts">
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'

/** Bans of both teams, one row per side (new bans pop in). The enemy team itself is a list of EnemyMemberRows. */
const props = defineProps<{ myBans: number[]; theirBans: number[] }>()

const sides = () => [
  { key: 'mine', label: 'Your Team', ids: props.myBans },
  { key: 'theirs', label: 'Enemy Team', ids: props.theirBans },
]
</script>

<template>
  <ListSection v-if="myBans.length || theirBans.length" header="Bans">
    <ListRow v-for="side in sides()" :key="side.key" :title="side.label" :value="side.ids.length ? undefined : 'None'">
      <template #trailing>
        <TransitionGroup v-if="side.ids.length" tag="span" name="lr-bans" class="lr-bans">
          <ChampionIcon v-for="(id, i) in side.ids" :key="`${i}:${id}`" :champion-id="id" size="ban" state="banned" />
        </TransitionGroup>
      </template>
    </ListRow>
  </ListSection>
</template>
