import { onBeforeUnmount, onMounted, useId, watch, type Ref } from 'vue'
import { storedGlassAmount } from '../lib/glassAmount'
import { animateSpring, type Spring } from '../lib/spring'
import { SPRING } from '../styles/tokens'

/*
 * Rim lensing for small, always visible glass (the tab bar, the action bar's capsule): an SVG displacement map in the
 * backdrop filter. Chromium only: Safari has no `backdrop-filter: url()` (WebKit bug 245510) and Firefox parses it but
 * draws nothing, so a JS gate decides; everyone else keeps the plain glass, which is the real look on iPhone.
 */

/** Refractive index of the imagined glass. */
const GLASS_INDEX = 1.5
/** A size change settles before the map is rebuilt (ms): the tab bar animates while it minimizes. */
const REBUILD_DELAY = 200
/** At this glass amount and above the surface is too tinted for lensing to show. */
const MAX_AMOUNT = 0.75

const OFF_QUERIES = [
  '(prefers-reduced-transparency: reduce)',
  '(prefers-reduced-motion: reduce)',
  '(prefers-contrast: more)',
  '(forced-colors: active)',
].map((q) => matchMedia(q))

function isChromium(): boolean {
  const brands = (navigator as Navigator & { userAgentData?: { brands: { brand: string }[] } }).userAgentData?.brands
  return !!brands?.some((b) => /Chromium/.test(b.brand)) && CSS.supports('backdrop-filter', 'url(#x)')
}

function canRefract(): boolean {
  return isChromium() && !OFF_QUERIES.some((q) => q.matches) && (storedGlassAmount() ?? 0) < MAX_AMOUNT
}

/** How far light bends at `t` across the bezel (0 at the rim, 1 where the flat top starts), from a convex squircle. */
function bend(t: number): number {
  const height = (x: number) => Math.pow(1 - Math.pow(1 - x, 4), 1 / 4)
  const step = 0.001
  const slope = (height(Math.min(1, t + step)) - height(Math.max(0, t - step))) / (2 * step)
  const incidence = Math.atan(slope)
  return Math.tan(incidence - Math.asin(Math.sin(incidence) / GLASS_INDEX))
}

/** The displacement map of a rounded rectangle: red and green are the x and y offset, 128 is none. As a data URL. */
function displacementMap(w: number, h: number, radius: number, bezel: number): string {
  const canvas = document.createElement('canvas')
  canvas.width = w
  canvas.height = h
  const context = canvas.getContext('2d')
  if (!context) return ''
  const image = context.createImageData(w, h)
  const peak = bend(0.001) || 1
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const px = x + 0.5 - w / 2
      const py = y + 0.5 - h / 2
      const qx = Math.abs(px) - (w / 2 - radius)
      const qy = Math.abs(py) - (h / 2 - radius)
      const outside = Math.hypot(Math.max(qx, 0), Math.max(qy, 0))
      const depth = radius - outside - Math.min(Math.max(qx, qy), 0)
      let dx = 0
      let dy = 0
      if (depth > 0 && depth < bezel) {
        // Outward along the edge's normal; the map samples inwards, the way a convex rim gathers light.
        const nx = qx > 0 && qy > 0 ? qx / outside : qx > qy ? 1 : 0
        const ny = qx > 0 && qy > 0 ? qy / outside : qx > qy ? 0 : 1
        const m = Math.min(1, bend(depth / bezel) / peak)
        dx = -Math.sign(px) * nx * m
        dy = -Math.sign(py) * ny * m
      }
      const i = (y * w + x) * 4
      image.data[i] = 128 + dx * 127
      image.data[i + 1] = 128 + dy * 127
      image.data[i + 2] = 128
      image.data[i + 3] = 255
    }
  }
  context.putImageData(image, 0, 0)
  return canvas.toDataURL()
}

const SVG_NS = 'http://www.w3.org/2000/svg'

/** Lens the rim of `el`'s glass while the phone's settings allow it. Attach to an element that is already `.lr-glass`. */
export function useRefraction(el: Ref<HTMLElement | null>) {
  if (typeof document === 'undefined' || !isChromium()) return
  const id = `lr-refract-${useId().replace(/[^a-zA-Z0-9-]/g, '')}`
  let svg: SVGSVGElement | null = null
  let observer: ResizeObserver | null = null
  let timer: ReturnType<typeof setTimeout> | undefined
  let reveal: Spring | null = null
  let shownFor: HTMLElement | null = null
  let builtW = 0
  let builtH = 0

  function clear() {
    builtW = 0
    builtH = 0
    clearTimeout(timer)
    reveal?.stop()
    observer?.disconnect()
    observer = null
    svg?.remove()
    svg = null
    shownFor?.style.removeProperty('backdrop-filter')
    shownFor = null
  }

  /** Build the filter for the element's current size. The first build ramps the lensing up: glass materializes. */
  function build(target: HTMLElement) {
    // Layout size: the box may be mid-materialize, scaled.
    const w = target.offsetWidth
    const h = target.offsetHeight
    if (!w || !h || (w === builtW && h === builtH)) return
    builtW = w
    builtH = h
    reveal?.stop()
    const style = getComputedStyle(target)
    const scale = Number(style.getPropertyValue('--refraction-scale')) || 0
    const bezel = parseFloat(style.getPropertyValue('--refraction-bezel')) || 0
    const radius = Math.min(parseFloat(style.borderTopLeftRadius) || 0, w / 2, h / 2)
    const first = !svg
    svg?.remove()
    svg = document.createElementNS(SVG_NS, 'svg')
    svg.setAttribute('aria-hidden', 'true')
    svg.style.cssText = 'position:absolute;width:0;height:0'
    svg.innerHTML = `<filter id="${id}" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">
      <feImage href="${displacementMap(w, h, radius, bezel)}" x="0" y="0" width="${w}" height="${h}" preserveAspectRatio="none" result="map" />
      <feDisplacementMap in="SourceGraphic" in2="map" scale="${first ? 0 : scale}" xChannelSelector="R" yChannelSelector="G" />
    </filter>`
    document.body.appendChild(svg)
    // After the blur: with url() first, Chromium drops the rest of the list.
    target.style.setProperty('backdrop-filter', `${style.getPropertyValue('--lr-glass-blur')} url(#${id})`)
    shownFor = target
    if (!first) return
    const map = svg.querySelector('feDisplacementMap')
    reveal = animateSpring({ from: 0, to: scale, ...SPRING.menu, onUpdate: (s) => map?.setAttribute('scale', String(s)) })
  }

  function update() {
    clear()
    const target = el.value
    if (!target || !canRefract()) return
    build(target)
    observer = new ResizeObserver(() => {
      clearTimeout(timer)
      timer = setTimeout(() => build(target), REBUILD_DELAY)
    })
    observer.observe(target)
  }

  onMounted(() => {
    update()
    OFF_QUERIES.forEach((q) => q.addEventListener('change', update))
  })
  // The capsule comes and goes with its status.
  watch(el, update, { flush: 'post' })
  onBeforeUnmount(() => {
    OFF_QUERIES.forEach((q) => q.removeEventListener('change', update))
    clear()
  })
}
