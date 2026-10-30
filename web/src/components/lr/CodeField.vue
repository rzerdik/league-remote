<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { extractPairCode } from '../../pairing'
import { DURATION } from '../../styles/tokens'

/**
 * Six-box verification-code field for the pairing code. One real input (numeric, one-time-code autofill) sits
 * invisibly over six boxes split 3–3. A pasted or autofilled pairing link is reduced to its 6 digits; `complete`
 * fires at 6 digits. Bump `errorKey` to shake it (rejected code).
 */
const model = defineModel<string>({ required: true })
const props = defineProps<{ disabled?: boolean; errorKey?: number; label?: string }>()
const emit = defineEmits<{ complete: [code: string] }>()

const input = ref<HTMLInputElement | null>(null)
const focused = ref(false)
const shaking = ref(false)

const digits = computed(() => Array.from({ length: 6 }, (_, i) => model.value[i] ?? ''))
const active = computed(() => (focused.value && !props.disabled ? Math.min(model.value.length, 5) : -1))

function set(raw: string) {
  const code = extractPairCode(raw) ?? raw.replace(/\D/g, '').slice(0, 6)
  model.value = code
  if (input.value && input.value.value !== code) input.value.value = code
  if (code.length === 6) emit('complete', code)
}
function onInput(e: Event) {
  set((e.target as HTMLInputElement).value)
}
function onPaste(e: ClipboardEvent) {
  const text = e.clipboardData?.getData('text') ?? ''
  const code = extractPairCode(text)
  if (!code) return // let the browser paste; onInput keeps only digits
  e.preventDefault()
  set(code)
}

watch(
  () => props.errorKey,
  (k, prev) => {
    if (k === undefined || k === prev) return
    shaking.value = false
    requestAnimationFrame(() => (shaking.value = true))
    setTimeout(() => (shaking.value = false), DURATION.shake)
  },
)

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div class="lr-code" :class="{ 'is-error': shaking, 'is-disabled': disabled }">
    <template v-for="(d, i) in digits" :key="i">
      <span v-if="i === 3" class="lr-code-dash" aria-hidden="true" />
      <span class="lr-code-box code-digit" :class="{ 'is-active': i === active }" aria-hidden="true">{{ d }}</span>
    </template>
    <input
      ref="input"
      :value="model"
      type="text"
      inputmode="numeric"
      pattern="[0-9]*"
      autocomplete="one-time-code"
      maxlength="64"
      :aria-label="label ?? '6-digit pairing code'"
      :disabled="disabled"
      @input="onInput"
      @paste="onPaste"
      @focus="focused = true"
      @blur="focused = false"
    />
  </div>
</template>
