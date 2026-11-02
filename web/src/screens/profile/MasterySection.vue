<script setup lang="ts">
import { computed, ref } from 'vue'
import ChampionIcon from '../../components/lr/ChampionIcon.vue'
import ListRow from '../../components/lr/ListRow.vue'
import ListSection from '../../components/lr/ListSection.vue'
import Tag from '../../components/lr/Tag.vue'
import type { ProfileMastery } from '../../contracts'
import { championName } from '../../ddragon'
import { formatCount, plural } from '../../format'

/**
 * Champion mastery: the score in the header, the top champions by points (portrait, name, level and points, the best
 * grade this season as a tag). The first few show; the rest fold away like the Friends tab's offline list.
 */
const props = defineProps<{ mastery: ProfileMastery }>()

const FOLDED = 3
const expanded = ref(false)
const shown = computed(() => (expanded.value ? props.mastery.topChampions : props.mastery.topChampions.slice(0, FOLDED)))
const hidden = computed(() => props.mastery.topChampions.length - FOLDED)

const header = computed(() => (props.mastery.score > 0 ? `Score ${formatCount(props.mastery.score)}` : undefined))
const footer = computed(() =>
  props.mastery.championCount > 0 ? `Mastery on ${plural(props.mastery.championCount, 'champion')}.` : undefined,
)
</script>

<template>
  <ListSection v-if="mastery.topChampions.length" header="Mastery" :header-trailing="header" :footer="footer">
    <ListRow
      v-for="c in shown"
      :key="c.championId"
      wrap
      :title="championName(c.championId)"
      :subtitle="`Level ${c.level} · ${formatCount(c.points)} points`"
    >
      <template #lead><ChampionIcon :champion-id="c.championId" decorative /></template>
      <template v-if="c.highestGrade" #trailing>
        <Tag>{{ c.highestGrade }}</Tag>
      </template>
    </ListRow>
    <ListRow
      v-if="hidden > 0"
      tappable
      text
      :title="expanded ? 'Show Fewer' : `Show ${hidden} More`"
      @click="expanded = !expanded"
    />
  </ListSection>
</template>
