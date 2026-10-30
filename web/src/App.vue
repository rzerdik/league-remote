<script setup lang="ts">
import { ChevronLeftIcon } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { goBack, navTitle, openChat, openGame, openPlayer, openSettings, pushed, readyCheck, screen, view } from './appView'
import InviteBanners from './components/invites/InviteBanners.vue'
import BannerHost from './components/lr/BannerHost.vue'
import LrButton from './components/lr/LrButton.vue'
import NavBar from './components/lr/NavBar.vue'
import TabBar from './components/lr/TabBar.vue'
import { useBackgroundAudio } from './composables/useBackgroundAudio'
import { scrollToTop, useNavScroller } from './composables/useCollapsingNav'
import { useEdgeSwipeBack } from './composables/useEdgeSwipeBack'
import { useKeyboardInset } from './composables/useKeyboardInset'
import { useMusic } from './composables/useMusic'
import { sheetStack } from './composables/useSheet'
import { useSoundCues } from './composables/useSoundCues'
import { useWakeLock } from './composables/useWakeLock'
import { loadDdragon } from './ddragon'
import { myTurnNow, receivedSwaps } from './draft'
import { lazy, prefetchLater } from './lib/lazy'
import { closePlayers, nav } from './navigation'
import { takePairCodeFromUrl } from './pairing'
import { refreshPushStatus } from './push'
import { redeemPairingCode, start } from './hub'
import ChampSelectScreen from './screens/ChampSelectScreen.vue'
import ChatScreen from './screens/ChatScreen.vue'
import FriendsScreen from './screens/FriendsScreen.vue'
import LobbyScreen from './screens/LobbyScreen.vue'
import PairScreen from './screens/PairScreen.vue'
import PlayScreen from './screens/PlayScreen.vue'
import ReadyCheckCover from './screens/ReadyCheckCover.vue'
import { liveAutoPick, liveState as live, store, toast } from './store'

// Not needed for the first screen: in their own chunks, fetched in the background once the app has started.
const ProfileScreen = lazy(() => import('./screens/ProfileScreen.vue'))
const PlayerProfileScreen = lazy(() => import('./screens/PlayerProfileScreen.vue'))
const InGameScreen = lazy(() => import('./screens/InGameScreen.vue'))
const PostGameScreen = lazy(() => import('./screens/PostGameScreen.vue'))
const SettingsScreen = lazy(() => import('./screens/SettingsScreen.vue'))

onMounted(() => {
  // Deep link from the connector's QR code: {pwaUrl}/?pair=123456 → pair in one step (replaces any saved pairing).
  const deepLinkCode = takePairCodeFromUrl()
  void start()
  void loadDdragon()
  void refreshPushStatus()
  if (deepLinkCode === 'invalid') toast('Invalid pairing link.')
  else if (deepLinkCode) void redeemPairingCode(deepLinkCode)
})

// Paired (at start, or once a pairing in this session succeeds): fetch the other screens in the background.
watch(
  () => !!store.grant,
  (paired) => {
    if (paired) prefetchLater(InGameScreen, PostGameScreen, ProfileScreen, PlayerProfileScreen, SettingsScreen)
  },
  { immediate: true },
)

useSoundCues()
useMusic()
useBackgroundAudio()
useKeyboardInset()
// Keep the screen on while something is counting down or waiting for you: queue, ready check, champion select.
useWakeLock(
  computed(() => {
    const s = live.value
    if (!s?.lcuConnected) return false
    const phase = s.gameflowPhase
    return !!readyCheck.value || phase === 'ChampSelect' || phase === 'Matchmaking' || !!s.lobby?.isSearching
  }),
)

// Champion select takes over: it is time-critical and needs the bottom of the screen (picker, Lock In).
watch(
  view,
  (v) => {
    if (v === 'champselect') nav.tab = 'home'
  },
  { immediate: true },
)
// A player's profile sits over the game screen and replaces it (Champion Select stays mounted under it, hidden). It
// closes when the game moves on (lobby → champion select → game), and in champion select for anything that needs your
// answer there: your turn, a swap or trade a teammate offers you, an auto pick counting down.
watch(view, () => closePlayers())
watch(
  () => {
    const cs = live.value?.champSelect
    return !!cs && (myTurnNow(cs) || receivedSwaps(cs).length > 0 || !!liveAutoPick.value?.run?.pending)
  },
  (needsYou) => {
    if (needsYou) closePlayers()
  },
)

const showTabBar = computed(
  () => screen.value !== 'pair' && screen.value !== 'champselect' && !pushed.value && sheetStack.modal === 0,
)
// Swipe from the left edge = the Back button, while a pushed view (a conversation, a game) is open.
useEdgeSwipeBack(
  computed(() => pushed.value && sheetStack.modal === 0),
  goBack,
)
// The tab bar and the action bar live in <body>; this class lets the action bar and the content clear the tab bar.
watch(showTabBar, (on) => document.documentElement.classList.toggle('lr-has-tabbar', on), { immediate: true })

const scroller = ref<HTMLElement | null>(null)
onMounted(() => (scroller.value = document.getElementById('app')))
useNavScroller(scroller)
// Each screen starts at the top (a conversation scrolls itself to its newest message).
watch(
  [
    screen,
    () => openChat.value?.id,
    () => openGame.value?.gameId,
    openSettings,
    () => openPlayer.value?.puuid,
    () => openPlayer.value?.gameId,
  ],
  () => {
    if (!openChat.value) requestAnimationFrame(scrollToTop)
  },
)
</script>

<template>
  <div class="lr-app">
    <NavBar v-if="screen !== 'pair'" :title="navTitle" :inline="!!openChat">
      <template v-if="pushed" #leading>
        <LrButton variant="glass" :icon="ChevronLeftIcon" aria-label="Back" @click="goBack" />
      </template>
    </NavBar>

    <Transition name="lr-screen">
      <PairScreen v-if="screen === 'pair'" key="pair" />
      <PlayerProfileScreen
        v-else-if="openPlayer"
        :key="`player-${openPlayer.puuid}`"
        :puuid="openPlayer.puuid"
        :name="openPlayer.name"
        :game-id="openPlayer.gameId"
      />
      <ProfileScreen v-else-if="screen === 'profile'" key="profile" />
      <ChatScreen v-else-if="screen === 'chat'" key="chat" />
      <FriendsScreen v-else-if="screen === 'friends'" key="friends" />
      <SettingsScreen v-else-if="screen === 'settings'" key="settings" />
      <PlayScreen v-else-if="screen === 'play'" key="play" />
      <LobbyScreen v-else-if="screen === 'lobby' && live?.lobby" key="lobby" :lobby="live.lobby" />
      <InGameScreen v-else-if="screen === 'game'" key="game" :queue-id="live?.lobby?.queueId ?? null" />
      <PostGameScreen v-else-if="screen === 'postgame'" key="postgame" />
    </Transition>
    <!--
      Champion Select stays mounted under a pushed player profile (hidden, `covered`), so its search, optimistic hover
      and the planning / finalization ticks carry on and it is still there on Back. Its own Transition cross-fades it
      in and out with the screens above.
    -->
    <Transition name="lr-screen">
      <ChampSelectScreen
        v-if="screen === 'champselect' && live?.champSelect"
        v-show="!openPlayer"
        key="champselect"
        :cs="live.champSelect"
        :rune-pages="live.runePages ?? []"
        :queue-id="live.lobby?.queueId ?? null"
        :covered="!!openPlayer"
      />
    </Transition>
  </div>

  <!-- Outside #app's inert subtree: these stay usable while a modal sheet is up. -->
  <Teleport to="body">
    <Transition name="lr-cover">
      <ReadyCheckCover v-if="readyCheck" :ready-check="readyCheck" :queue-id="live?.lobby?.queueId ?? null" />
    </Transition>
    <BannerHost />
  </Teleport>
  <InviteBanners v-if="screen !== 'pair'" />
  <TabBar v-if="showTabBar" />
</template>
