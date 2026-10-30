<script setup lang="ts">
import { SearchIcon, XIcon } from '@lucide/vue'
import { ref } from 'vue'

/** Capsule search field: filters as you type; the clear button shows only with text; Escape clears. */
const model = defineModel<string>({ required: true })
defineProps<{ placeholder: string; label?: string }>()
defineEmits<{ focus: []; blur: [] }>()

const input = ref<HTMLInputElement | null>(null)
function clear() {
  model.value = ''
  input.value?.focus()
}
defineExpose({ focus: () => input.value?.focus(), blur: () => input.value?.blur() })
</script>

<template>
  <label class="lr-search">
    <span class="lr-sr">{{ label ?? placeholder }}</span>
    <SearchIcon class="lr-icon" :stroke-width="2" aria-hidden="true" />
    <input
      ref="input"
      v-model="model"
      type="search"
      enterkeyhint="search"
      :placeholder="placeholder"
      autocomplete="off"
      autocorrect="off"
      autocapitalize="off"
      spellcheck="false"
      @focus="$emit('focus')"
      @blur="$emit('blur')"
      @keydown.esc.prevent="model = ''"
      @keydown.enter.prevent="input?.blur()"
    />
    <button v-if="model" type="button" class="lr-search-clear" aria-label="Clear search" @click.prevent="clear">
      <span><XIcon :stroke-width="3" aria-hidden="true" /></span>
    </button>
  </label>
</template>
