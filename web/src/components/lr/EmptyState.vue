<script setup lang="ts">
import type { Component } from 'vue'
import ActivityIndicator from './ActivityIndicator.vue'
import LrButton from './LrButton.vue'

/** Content-unavailable view: symbol (or the activity indicator), title, message, optional action. */
defineProps<{ icon?: Component; busy?: boolean; title: string; message?: string; actionLabel?: string }>()
defineEmits<{ action: [] }>()
</script>

<template>
  <div class="lr-empty" role="status">
    <span class="lr-empty-symbol">
      <ActivityIndicator v-if="busy" :size="36" decorative />
      <component :is="icon" v-else-if="icon" :stroke-width="1.5" aria-hidden="true" />
    </span>
    <h2 class="title-2">{{ title }}</h2>
    <p v-if="message" class="subheadline">{{ message }}</p>
    <LrButton v-if="actionLabel" variant="gray" @click="$emit('action')">{{ actionLabel }}</LrButton>
  </div>
</template>
