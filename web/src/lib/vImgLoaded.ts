import type { Directive } from 'vue'

/**
 * `v-img-loaded` on an <img class="skeleton">: adds `.is-loaded` once the current src has loaded (or failed), which
 * stops the shimmer. Re-arms when the src changes.
 */
function arm(el: HTMLImageElement) {
  const src = el.src
  if (el.dataset.loadedSrc === src && el.classList.contains('is-loaded')) return
  el.dataset.loadedSrc = src
  const done = () => {
    if (el.dataset.loadedSrc === (el.src)) el.classList.add('is-loaded')
  }
  if (el.complete && el.naturalWidth > 0) return done()
  el.classList.remove('is-loaded')
  el.addEventListener('load', done, { once: true })
  el.addEventListener('error', done, { once: true })
}

export const vImgLoaded: Directive<HTMLImageElement> = {
  mounted: arm,
  updated: arm,
}
