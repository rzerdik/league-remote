import { ref } from 'vue'
import { haptic } from '../../haptics'
import { forgetPushPairing } from '../../push'
import { redeemPairingCode } from '../../hub'
import { store } from '../../store'

/**
 * Pairing flow shared by the Pair screen and Settings › Pair Again: the 6-digit code (submitted at 6 digits) and the
 * QR scanner. Errors are shown by hub.ts as banners; a rejected code also shakes the field (`errorKey`).
 */
export function usePairing(onPaired?: () => void) {
  const code = ref('')
  const errorKey = ref(0)
  const scanning = ref(false)
  /** The page isn't a secure context (plain http): the in-app camera can't run, the Camera app can. */
  const cameraUnavailable = ref(false)

  async function submit(c = code.value) {
    if (c.length !== 6 || store.pairing) return
    // Pairing again: the old pairing must stop pushing to this phone (a failed redeem re-registers on the next sign-in).
    await forgetPushPairing()
    const ok = await redeemPairingCode(c)
    code.value = ''
    if (ok) {
      haptic('success')
      onPaired?.()
    } else {
      errorKey.value++
      haptic('error')
    }
  }

  function openScanner() {
    haptic('selection')
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      cameraUnavailable.value = true
      return
    }
    cameraUnavailable.value = false
    scanning.value = true
  }

  function onScanned(c: string) {
    haptic('success')
    scanning.value = false
    code.value = c
    void submit(c)
  }

  return { code, errorKey, scanning, cameraUnavailable, submit, openScanner, onScanned }
}

export type Pairing = ReturnType<typeof usePairing>
