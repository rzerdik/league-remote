import { computed, onBeforeUnmount, onMounted, reactive, type Ref } from 'vue'

/**
 * Large title → inline title hand-off + the top scroll-edge band. The app scrolls inside #app; the screen's
 * LargeTitle registers itself, the NavBar reads `navState`.
 */
export const navState = reactive({
  collapsed: false,
  edge: false,
  /** The large title the nav bar follows (its element); null when the screen has none. */
  title: null as HTMLElement | null,
})

/**
 * Every mounted large title, oldest first. Usually one; a screen kept mounted under a pushed view (Champion Select under
 * a player's profile, hidden) keeps its own, so the nav bar follows the newest one that is on screen.
 */
const largeTitles: HTMLElement[] = []
let scroller: HTMLElement | null = null

function shownTitle(): HTMLElement | null {
  for (let i = largeTitles.length - 1; i >= 0; i--) if (largeTitles[i]!.getClientRects().length) return largeTitles[i]!
  return null
}

function navBottom(): number {
  return document.querySelector('.lr-nav-bar')?.getBoundingClientRect().bottom ?? 0
}

function updateNav() {
  if (!scroller) return
  const top = scroller.scrollTop
  const largeTitle = shownTitle()
  navState.title = largeTitle
  navState.collapsed = largeTitle
    ? (largeTitle.querySelector('h1') ?? largeTitle).getBoundingClientRect().bottom < navBottom() - 4
    : top > 8
  navState.edge = top > 2
}

/** Watch the #app scroller once (App.vue). */
export function useNavScroller(el: Ref<HTMLElement | null>) {
  onMounted(() => {
    scroller = el.value
    scroller?.addEventListener('scroll', updateNav, { passive: true })
    updateNav()
  })
  onBeforeUnmount(() => scroller?.removeEventListener('scroll', updateNav))
}

/**
 * A screen's large title block (LargeTitle.vue). `docked`: the title collapsed under the nav bar and it is the title
 * the nav bar follows (its trailing buttons go into the nav bar then).
 */
export function useCollapsingNav(title: Ref<HTMLElement | null>) {
  onMounted(() => {
    if (title.value) largeTitles.push(title.value)
    updateNav()
  })
  onBeforeUnmount(() => {
    const i = title.value ? largeTitles.indexOf(title.value) : -1
    if (i >= 0) largeTitles.splice(i, 1)
    updateNav()
  })
  return { docked: computed(() => navState.collapsed && !!title.value && navState.title === title.value) }
}

export function scrollToTop() {
  if (scroller) scroller.scrollTop = 0
  updateNav()
}
