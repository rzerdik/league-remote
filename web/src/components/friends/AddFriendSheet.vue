<script setup lang="ts">
import { CheckIcon } from '@lucide/vue'
import { computed, ref, useId, watch } from 'vue'
import { RiotIds, validateGameName, validateTagLine } from '../../contracts'
import { sendFriendRequest } from '../../friendRequests'
import { tapFeedback } from '../../haptics'
import { parseRiotId, riotId } from '../../riotId'
import { toast } from '../../store'
import ListSection from '../lr/ListSection.vue'
import LrButton from '../lr/LrButton.vue'
import Sheet, { type DetentId } from '../lr/Sheet.vue'
import StatusLine from '../lr/StatusLine.vue'
import TextField from '../lr/TextField.vue'

/**
 * Add Friend: a Riot ID field (game name # tag line, as the client's Add Friend box) and Send Request (✓ in the sheet's
 * header, like iOS's Done). A pasted "Name#TAG" in the name field is split. League's reason for a refusal ("No player
 * with that Riot ID.") shows under the field; the sheet closes once the request is sent. One field needs no full-height
 * sheet: it opens at the medium detent and rises to large once the field takes focus (the keyboard would cover a
 * medium sheet's lower half).
 */
const open = defineModel<boolean>('open', { required: true })

const gameName = ref('')
const tagLine = ref('')
const error = ref('')
const sending = ref(false)
const tagInput = ref<HTMLInputElement | null>(null)
const detent = ref<DetentId>('medium')
/** The footer under the field: the hint, or League's reason the request failed. */
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

/** "Name#TAG" typed or pasted into the name field: move the tag over. */
function onNameInput() {
  const hash = gameName.value.indexOf('#')
  if (hash < 0) return
  const id = parseRiotId(gameName.value, tagLine.value)
  gameName.value = id.gameName
  tagLine.value = id.tagLine
  tagInput.value?.focus()
}

async function submit() {
  if (sending.value) return
  tapFeedback()
  if (invalid.value) {
    error.value = invalid.value
    return
  }
  sending.value = true
  try {
    const { gameName: name, tagLine: tag } = parsed.value
    const result = await sendFriendRequest(name, tag)
    if (result.ok) {
      toast(`Friend request sent to ${riotId({ gameName: name, tagLine: tag })}`, 'success')
      open.value = false
    } else {
      error.value = result.error ?? ''
    }
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <Sheet
    v-model:open="open"
    v-model:detent="detent"
    modal
    :detents="['medium', 'large']"
    title="Add Friend"
    body-class="lr-sheet-body--lists"
  >
    <template #head-trailing>
      <LrButton
        variant="prominent"
        :icon="CheckIcon"
        :aria-label="sending ? 'Sending Request…' : 'Send Request'"
        :loading="sending"
        :disabled="sending || !parsed.gameName || !parsed.tagLine"
        @click="submit"
      />
    </template>
    <form novalidate @submit.prevent="submit" @focusin="detent = 'large'">
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
            class="add-friend-tag"
            type="text"
            placeholder="Tag"
            aria-label="Tag line"
            :maxlength="RiotIds.maxTagLine"
            :size="RiotIds.maxTagLine"
            autocomplete="off"
            autocorrect="off"
            autocapitalize="characters"
            spellcheck="false"
            enterkeyhint="send"
            :aria-invalid="!!error || undefined"
            :aria-describedby="hintId"
            @keydown.enter.prevent="submit"
          />
        </TextField>
        <template #footer>
          <StatusLine v-if="error" :id="hintId" tone="red" :text="error" role="alert" />
          <span v-else :id="hintId">Their Riot ID is on their profile in League, like Faker #KR1.</span>
        </template>
      </ListSection>
    </form>
  </Sheet>
</template>

<style scoped>
/* The tag is sized to its 5 characters (size attribute); the game name takes the rest of the field. */
.add-friend-tag {
  flex: none;
}
</style>
