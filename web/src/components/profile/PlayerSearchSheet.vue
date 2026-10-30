<script setup lang="ts">
import { SearchIcon } from '@lucide/vue'
import { computed, ref, useId, watch } from 'vue'
import { scrollToTop } from '../../composables/useCollapsingNav'
import { RiotIds, validateGameName, validateTagLine } from '../../contracts'
import { haptic, tapFeedback } from '../../haptics'
import { openPlayer } from '../../navigation'
import { isOwnRiotId, playerProfile, searchPlayer } from '../../profile'
import { parseRiotId } from '../../riotId'
import ListSection from '../lr/ListSection.vue'
import LrButton from '../lr/LrButton.vue'
import Sheet, { type DetentId } from '../lr/Sheet.vue'
import StatusLine from '../lr/StatusLine.vue'
import TextField from '../lr/TextField.vue'

/**
 * Search Players (the Profile tab's search button): a Riot ID field (game name # tag line, as in Add Friend) and Search
 * (in the sheet's header, like iOS's Done). The PC looks the Riot ID up — any case — and loads that player's profile;
 * once it is on the phone the sheet closes and the profile opens as a pushed view. "No player with that Riot ID." (or
 * any other refusal) shows under the field. Your own Riot ID just closes the sheet: your profile is the tab underneath.
 */
const open = defineModel<boolean>('open', { required: true })

const gameName = ref('')
const tagLine = ref('')
const error = ref('')
const searching = ref(false)
const tagInput = ref<HTMLInputElement | null>(null)
const detent = ref<DetentId>('medium')
const hintId = useId()

const parsed = computed(() => parseRiotId(gameName.value, tagLine.value))
const invalid = computed(() => validateGameName(parsed.value.gameName) ?? validateTagLine(parsed.value.tagLine))

watch(open, (o) => {
  if (!o) return
  detent.value = 'medium'
  gameName.value = ''
  tagLine.value = ''
  error.value = ''
})
watch([gameName, tagLine], () => (error.value = ''))

/**
 * "Name#TAG" typed or pasted into the name field: move the tag over (replacing one typed before, since a pasted Riot ID
 * is the whole answer); a bare "#" just moves on to the tag field.
 */
function onNameInput() {
  const hash = gameName.value.indexOf('#')
  if (hash < 0) return
  const tag = gameName.value.slice(hash + 1).replace(/#/g, '').trim()
  gameName.value = gameName.value.slice(0, hash).trim()
  if (tag) tagLine.value = tag
  tagInput.value?.focus()
}

async function submit() {
  if (searching.value) return
  tapFeedback()
  if (invalid.value) {
    error.value = invalid.value
    return
  }
  const { gameName: name, tagLine: tag } = parsed.value
  if (isOwnRiotId(name, tag)) {
    open.value = false
    scrollToTop()
    return
  }
  searching.value = true
  try {
    const result = await searchPlayer(name, tag)
    if (!open.value) return // closed while searching: don't push a view the user walked away from
    if (result.ok) {
      haptic('success')
      const summoner = playerProfile(result.puuid)?.summoner
      open.value = false
      openPlayer(result.puuid, summoner ? `${summoner.gameName}#${summoner.tagLine}` : `${name}#${tag}`)
    } else {
      haptic('error')
      error.value = result.error
    }
  } finally {
    searching.value = false
  }
}
</script>

<template>
  <Sheet
    v-model:open="open"
    v-model:detent="detent"
    modal
    :detents="['medium', 'large']"
    title="Search Players"
    body-class="lr-sheet-body--lists"
  >
    <template #head-trailing>
      <LrButton
        variant="prominent"
        :icon="SearchIcon"
        :aria-label="searching ? 'Searching…' : 'Search'"
        :loading="searching"
        :disabled="searching || !parsed.gameName || !parsed.tagLine"
        @click="submit"
      />
    </template>
    <form novalidate role="search" @submit.prevent="submit" @focusin="detent = 'large'">
      <ListSection bare>
        <TextField :invalid="!!error">
          <input
            v-model="gameName"
            type="text"
            placeholder="Game name"
            aria-label="Game name"
            :maxlength="RiotIds.maxGameName + RiotIds.maxTagLine + 1"
            autocomplete="off"
            autocorrect="off"
            autocapitalize="off"
            spellcheck="false"
            enterkeyhint="next"
            :aria-invalid="!!error || undefined"
            :aria-describedby="hintId"
            @input="onNameInput"
            @keydown.enter.prevent="tagInput?.focus()"
          />
          <span class="body lr-secondary" aria-hidden="true">#</span>
          <input
            ref="tagInput"
            v-model="tagLine"
            class="player-search-tag"
            type="text"
            placeholder="Tag"
            aria-label="Tag line"
            :maxlength="RiotIds.maxTagLine"
            :size="RiotIds.maxTagLine"
            autocomplete="off"
            autocorrect="off"
            autocapitalize="characters"
            spellcheck="false"
            enterkeyhint="search"
            :aria-invalid="!!error || undefined"
            :aria-describedby="hintId"
            @keydown.enter.prevent="submit"
          />
        </TextField>
        <template #footer>
          <StatusLine v-if="error" :id="hintId" tone="red" :text="error" role="alert" />
          <StatusLine v-else-if="searching" :id="hintId" busy text="Looking for that player…" role="status" />
          <span v-else :id="hintId">Enter their Riot ID: game name and tag. Capitals don’t matter.</span>
        </template>
      </ListSection>
    </form>
  </Sheet>
</template>

<style scoped>
/* The tag is sized to its 5 characters (size attribute); the game name takes the rest of the field. */
.player-search-tag {
  flex: none;
}
</style>
