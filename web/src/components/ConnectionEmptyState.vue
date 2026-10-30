<script setup lang="ts">
import { connectionEmpty } from '../connection'
import { start } from '../hub'
import EmptyState from './lr/EmptyState.vue'

/**
 * The content-unavailable view of a tab that shows the PC's data while the phone can't reach it (`connectionEmpty`):
 * "Offline" with Try Again, "Reconnecting…", "Signing In…". Renders nothing once signed in, so a screen puts it first
 * and its own content under `v-if="!connectionEmpty"`.
 */
</script>

<template>
  <EmptyState
    v-if="connectionEmpty"
    :icon="connectionEmpty.icon"
    :busy="connectionEmpty.busy"
    :title="connectionEmpty.title"
    :message="connectionEmpty.message"
    :action-label="connectionEmpty.action"
    @action="start()"
  />
</template>
