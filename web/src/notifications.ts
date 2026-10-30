import { computed, ref } from 'vue'
import { KEYS, readText, writeText } from './lib/storage'
import { disablePush, enablePush, pushPreferences, pushStatus, sendTestNotification, setPushPreference } from './push'
import { tapSound } from './sound'
import { toast } from './store'

/**
 * The notification controls shared by the Play tab's tip and Settings › Notifications (the UI side of push.ts). One
 * push state per phone, so the busy flags are shared too.
 */

/** Push needs the Home Screen app on iPhone: what to do from a Safari tab (Play tip, Settings footer). */
export const INSTALL_HINT = 'Add to Home Screen first (Share › Add to Home Screen), then pair again.'

/** Turning notifications on or off is in flight. */
const busy = ref(false)

/** Must run inside the tap: iOS only shows the permission prompt then. */
async function enable() {
  if (busy.value) return
  tapSound()
  busy.value = true
  try {
    const ok = await enablePush()
    if (ok) toast('Notifications on', 'success')
    else if (pushStatus.value === 'denied') toast('Notifications blocked in browser settings.')
  } finally {
    busy.value = false
  }
}

async function disable() {
  if (busy.value) return
  busy.value = true
  try {
    await disablePush()
  } finally {
    busy.value = false
  }
}

const hintDismissed = ref(readText(KEYS.notifyHintDismissed) === '1')
function dismissHint() {
  hintDismissed.value = true
  writeText(KEYS.notifyHintDismissed, '1') // storage unavailable: dismissed for this session only
}

/** Show the "Get Notified" tip: not dismissed and notifications could still be turned on. */
const showHint = computed(
  () => !hintDismissed.value && (pushStatus.value === 'default' || pushStatus.value === 'needs-install'),
)

const statusLabel = computed(() => {
  switch (pushStatus.value) {
    case 'enabled':
      return 'On'
    case 'denied':
      return 'Blocked'
    case 'needs-install':
      return 'Needs Home Screen'
    case 'unsupported':
      return 'Not Available'
    default:
      return 'Off'
  }
})

/** A test notification is on its way (the button shows a spinner meanwhile). */
const testing = ref(false)
async function sendTest() {
  if (testing.value) return
  testing.value = true
  try {
    await sendTestNotification()
  } finally {
    testing.value = false
  }
}

export const notifications = {
  pushStatus,
  busy,
  enable,
  disable,
  showHint,
  dismissHint,
  statusLabel,
  preferences: pushPreferences,
  setPreference: setPushPreference,
  testing,
  sendTest,
}
