<script setup lang="ts">
/**
 * The capsule of a form text field (Add Friend's game name # tag): put the <input>s and any separators in the default
 * slot — inputs get the field's type, colours and a 16px floor (iOS would zoom in). Give each input an aria-label and
 * aria-invalid / aria-describedby itself; `invalid` draws the red ring. Search uses SearchField instead.
 */
defineProps<{ invalid?: boolean }>()
</script>

<template>
  <label class="lr-field" :class="{ 'is-invalid': invalid }"><slot /></label>
</template>
