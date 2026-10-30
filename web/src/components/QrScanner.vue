<script setup lang="ts">
import { FlashlightIcon, XIcon } from '@lucide/vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { useModalLayer } from '../composables/useModalLayer'
import { haptic } from '../haptics'
import { within } from '../lib/timeout'
import { extractPairCode } from '../pairing'
import { DURATION } from '../styles/tokens'
import LrButton from './lr/LrButton.vue'

/**
 * Full-screen QR scanner cover (render it in <body>): camera feed, viewfinder, close and flashlight in clear glass,
 * the hint capsule (or the error) and Enter Code. A modal layer: Close has focus, Escape closes.
 */
const emit = defineEmits<{ close: []; code: [code: string] }>()

/** The subset of the Barcode Detection API we use (native or ponyfill). */
interface QrDetector {
  detect(source: HTMLVideoElement): Promise<Array<{ rawValue: string }>>
}
interface DetectorCtor {
  new (opts: { formats: string[] }): QrDetector
  getSupportedFormats?(): Promise<string[]>
}

const root = ref<HTMLElement | null>(null)
const closeButton = ref<InstanceType<typeof LrButton> | null>(null)
useModalLayer(root, () => (closeButton.value?.$el as HTMLElement | undefined) ?? null)
const video = ref<HTMLVideoElement | null>(null)
const status = ref<'starting' | 'scanning' | 'error'>('starting')
const error = ref('')
const hint = ref('')
const torchSupported = ref(false)
const torchOn = ref(false)

let stream: MediaStream | null = null
let timer: ReturnType<typeof setTimeout> | undefined
let hintTimer: ReturnType<typeof setTimeout> | undefined
let disposed = false

/** How often a frame is checked for a code (ms). */
const SCAN_EVERY_MS = 200
/** A found code freezes the frame this long before the cover fades out (ms), so you see what was read. */
const FREEZE_MS = 150

function stopCamera() {
  clearTimeout(timer)
  stream?.getTracks().forEach((t) => t.stop())
  stream = null
  if (video.value) video.value.srcObject = null
}

function fail(msg: string) {
  stopCamera()
  status.value = 'error'
  error.value = msg
}

function cameraErrorText(e: unknown): string {
  const name = e instanceof DOMException || e instanceof Error ? e.name : ''
  switch (name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'Camera access denied.'
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'No camera found.'
    case 'NotReadableError':
    case 'AbortError':
      return 'Camera in use by another app.'
    default:
      return 'Camera unavailable.'
  }
}

/** The bundled decoder's code couldn't be fetched (offline, or an update replaced it). */
class ScannerLoadError extends Error {}
/** How long fetching the bundled decoder may take. */
const FALLBACK_TIMEOUT_MS = 15_000

async function getDetector(): Promise<QrDetector> {
  const Native = (globalThis as { BarcodeDetector?: DetectorCtor }).BarcodeDetector
  if (Native) {
    try {
      const formats = (await Native.getSupportedFormats?.()) ?? []
      if (formats.includes('qr_code')) return new Native({ formats: ['qr_code'] })
    } catch {
      /* fall through to the bundled decoder */
    }
  }
  // Bounded, and a chunk Vite couldn't fetch resolves to nothing (main.ts handles vite:preloadError): say that, not
  // "not supported".
  const fallback = await within(
    import('../lib/qrDetector').catch(() => undefined),
    FALLBACK_TIMEOUT_MS,
    undefined,
  )
  if (!fallback?.BarcodeDetector) throw new ScannerLoadError()
  return new fallback.BarcodeDetector({ formats: ['qr_code'] })
}

function showHint(text: string) {
  hint.value = text
  clearTimeout(hintTimer)
  hintTimer = setTimeout(() => (hint.value = ''), DURATION.banner)
}

function scanLoop(detector: QrDetector) {
  const s = stream
  const tick = async () => {
    if (disposed || !stream || stream !== s) return // stopped, or restarted with its own loop
    const v = video.value
    if (v && v.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && v.videoWidth > 0) {
      try {
        const found = await detector.detect(v)
        if (disposed || !stream || stream !== s) return
        for (const b of found) {
          const code = extractPairCode(b.rawValue)
          if (code) {
            v.pause()
            setTimeout(() => {
              stopCamera()
              emit('code', code)
            }, FREEZE_MS)
            return
          }
        }
        if (found.length) showHint('Not a League Remote code.')
      } catch {
        /* transient decode error: keep scanning */
      }
    }
    timer = setTimeout(tick, SCAN_EVERY_MS)
  }
  void tick()
}

async function toggleTorch() {
  const track = stream?.getVideoTracks()[0]
  if (!track) return
  const next = !torchOn.value
  try {
    await track.applyConstraints({ advanced: [{ torch: next } as MediaTrackConstraintSet] })
    torchOn.value = next
  } catch {
    torchSupported.value = false
  }
}

/** Open the camera and attach it to the <video>. False (and the error shown) when it could not be started. */
async function startCamera(): Promise<boolean> {
  let s: MediaStream
  try {
    s = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
      audio: false,
    })
  } catch (e) {
    if (!disposed) fail(cameraErrorText(e))
    return false
  }
  if (disposed) {
    s.getTracks().forEach((t) => t.stop())
    return false
  }
  stream = s
  const track = s.getVideoTracks()[0]
  const caps = (track?.getCapabilities?.() ?? {}) as MediaTrackCapabilities & { torch?: boolean }
  torchSupported.value = caps.torch === true
  torchOn.value = false

  const v = video.value
  if (!v) return false
  v.srcObject = s
  try {
    await v.play()
  } catch {
    /* a muted inline video should autoplay; scanning still starts once frames arrive */
  }
  return true
}

let detector: QrDetector | null = null

onMounted(async () => {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    fail("Use your phone's Camera app to scan.")
    return
  }
  if (!(await startCamera())) return

  try {
    detector = await getDetector()
  } catch (e) {
    if (!disposed)
      fail(
        e instanceof ScannerLoadError
          ? 'Couldn’t load the scanner. Try again.'
          : 'QR scanning is not supported in this browser. Enter the code instead.',
      )
    return
  }
  if (disposed || !stream) return
  status.value = 'scanning'
  scanLoop(detector)
})

/**
 * iOS pauses the <video> (and may end the camera track) while the app is in the background: on return, resume the
 * preview, or reopen the camera when the track has ended.
 */
async function onVisibilityChange() {
  if (document.visibilityState !== 'visible' || disposed || status.value !== 'scanning' || !detector) return
  const track = stream?.getVideoTracks()[0]
  if (track && track.readyState === 'live') {
    void video.value?.play().catch(() => {})
    return
  }
  stopCamera()
  if ((await startCamera()) && !disposed) scanLoop(detector)
}
document.addEventListener('visibilitychange', onVisibilityChange)

onBeforeUnmount(() => {
  disposed = true
  document.removeEventListener('visibilitychange', onVisibilityChange)
  clearTimeout(hintTimer)
  stopCamera()
})
</script>

<template>
  <div
    ref="root"
    class="lr-scanner"
    role="dialog"
    aria-modal="true"
    aria-label="Scan Pairing QR Code"
    tabindex="-1"
    @keydown.esc="emit('close')"
  >
    <video ref="video" muted playsinline autoplay />
    <div v-if="status !== 'error'" class="lr-viewfinder" aria-hidden="true">
      <svg viewBox="0 0 100 100" fill="none" stroke="currentColor" stroke-width="3.2" stroke-linecap="round">
        <path d="M2 24V14A12 12 0 0 1 14 2h10M76 2h10a12 12 0 0 1 12 12v10M98 76v10a12 12 0 0 1-12 12H76M24 98H14A12 12 0 0 1 2 86V76" />
      </svg>
    </div>

    <div class="lr-scanner-top">
      <LrButton ref="closeButton" variant="clear" :icon="XIcon" aria-label="Close Scanner" @click="emit('close')" />
      <LrButton
        v-if="torchSupported"
        variant="clear"
        :icon="FlashlightIcon"
        :pressed="torchOn"
        aria-label="Flashlight"
        @click="haptic('selection'), toggleTorch()"
      />
    </div>

    <div class="lr-scanner-bottom">
      <p class="lr-hint lr-glass-clear subheadline" :role="status === 'error' || hint ? 'alert' : 'status'">
        {{
          status === 'error'
            ? error
            : hint || (status === 'starting' ? 'Starting camera…' : 'Point at the QR code')
        }}
      </p>
      <LrButton variant="clear" size="lg" @click="emit('close')">Enter Code</LrButton>
    </div>
  </div>
</template>
