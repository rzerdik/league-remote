<script setup lang="ts">
/**
 * `?demo=materials`: the glass surfaces over the worst backdrop there is, busy champion art with a white region
 * (`&glass=0`, `0.5` or `1` for the glass amount). `scripts/check-glass-contrast.mjs` has the numbers.
 */
import { ref } from 'vue'
import { useRefraction } from '../../composables/useRefraction'

const lens = ref<HTMLElement | null>(null)
useRefraction(lens)
</script>

<template>
  <div class="materials">
    <img src="https://ddragon.leagueoflegends.com/cdn/img/champion/splash/Ahri_0.jpg" alt="" />
    <div class="white" />
    <div class="black" />
    <div class="surfaces">
      <div class="surface lr-glass">
        <span class="headline">glass</span>
        <span class="subheadline lr-secondary">Secondary text on regular glass</span>
      </div>
      <div ref="lens" class="surface lr-glass capsule">
        <span class="headline">glass + rim lensing</span>
        <span class="subheadline lr-secondary">Chromium only: plain glass elsewhere</span>
      </div>
      <div class="surface lr-glass-thick">
        <span class="headline">glass-thick</span>
        <span class="subheadline lr-secondary">Secondary text on thick glass</span>
      </div>
      <div class="clear">
        <div class="surface lr-glass-clear">
          <span class="headline">glass-clear</span>
          <span class="subheadline">Over dim-clear, only on the camera</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.materials {
  position: fixed;
  inset: 0;
  z-index: var(--z-alert);
  overflow: hidden;
  background: var(--bg);
}
img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.white,
.black {
  position: absolute;
  top: 0;
  bottom: 0;
  width: 33%;
}
.white {
  right: 0;
  background: #fff;
}
.black {
  left: 0;
  background: #000;
}
.surfaces {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: var(--space-6);
  padding: var(--space-4);
}
.surface {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  padding: var(--space-5);
  border-radius: var(--radius-xl);
}
.capsule {
  border-radius: var(--radius-full);
}
.clear {
  background: var(--dim-clear);
  border-radius: var(--radius-xl);
}
</style>
