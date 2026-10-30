import { onBeforeUnmount, onMounted } from 'vue'

/**
 * Publishes the on-screen keyboard's height as --lr-keyboard (px) on <html>, from visualViewport, so the action bar
 * can sit above the keyboard (Champion Select search).
 */
export function useKeyboardInset() {
  const vv = typeof window !== 'undefined' ? window.visualViewport : null
  function update() {
    if (!vv) return
    const inset = Math.max(0, window.innerHeight - vv.height - vv.offsetTop)
    document.documentElement.style.setProperty('--lr-keyboard', `${Math.round(inset)}px`)
  }
  onMounted(() => {
    vv?.addEventListener('resize', update)
    vv?.addEventListener('scroll', update)
    update()
  })
  onBeforeUnmount(() => {
    vv?.removeEventListener('resize', update)
    vv?.removeEventListener('scroll', update)
  })
}
