/**
 * Synthesized sounds for the two moments League has no sound of its own for (`ClientSoundCatalog.cs`, "No client
 * sound"). Each recipe schedules a few oscillators / noise bursts on `out` starting at `t` and returns its length in
 * seconds. No audio files are shipped.
 */

type Recipe = (ctx: AudioContext, out: AudioNode, t: number) => number

interface ToneOpts {
  type?: OscillatorType
  /** Peak gain (0..1). */
  gain?: number
  attack?: number
  /** Glide the frequency to this value over the tone's duration. */
  to?: number
}

function tone(ctx: AudioContext, out: AudioNode, freq: number, start: number, dur: number, o: ToneOpts = {}) {
  const osc = ctx.createOscillator()
  const g = ctx.createGain()
  osc.type = o.type ?? 'sine'
  osc.frequency.setValueAtTime(freq, start)
  if (o.to) osc.frequency.exponentialRampToValueAtTime(o.to, start + dur)
  const peak = o.gain ?? 0.3
  const attack = Math.min(o.attack ?? 0.005, dur / 2)
  g.gain.setValueAtTime(0.0001, start)
  g.gain.linearRampToValueAtTime(peak, start + attack)
  g.gain.exponentialRampToValueAtTime(0.0001, start + dur)
  osc.connect(g).connect(out)
  osc.start(start)
  osc.stop(start + dur + 0.02)
}

let noiseBuffer: AudioBuffer | null = null
function noise(
  ctx: AudioContext,
  out: AudioNode,
  start: number,
  dur: number,
  o: { gain?: number; filter?: BiquadFilterType; freq?: number; q?: number } = {},
) {
  if (!noiseBuffer || noiseBuffer.sampleRate !== ctx.sampleRate) {
    noiseBuffer = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.5), ctx.sampleRate)
    const d = noiseBuffer.getChannelData(0)
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1
  }
  const src = ctx.createBufferSource()
  src.buffer = noiseBuffer
  const f = ctx.createBiquadFilter()
  f.type = o.filter ?? 'lowpass'
  f.frequency.value = o.freq ?? 1200
  f.Q.value = o.q ?? 0.7
  const g = ctx.createGain()
  g.gain.setValueAtTime(o.gain ?? 0.2, start)
  g.gain.exponentialRampToValueAtTime(0.0001, start + dur)
  src.connect(f).connect(g).connect(out)
  src.start(start)
  src.stop(start + dur + 0.02)
}

/** A bell-ish note: fundamental plus a quiet inharmonic partial. */
function bell(ctx: AudioContext, out: AudioNode, freq: number, start: number, dur: number, gain = 0.22) {
  tone(ctx, out, freq, start, dur, { gain, type: 'sine' })
  tone(ctx, out, freq * 2.76, start, dur * 0.45, { gain: gain * 0.18, type: 'sine' })
  tone(ctx, out, freq * 2, start, dur * 0.6, { gain: gain * 0.25, type: 'triangle' })
}

const N = (semitonesFromA4: number) => 440 * 2 ** (semitonesFromA4 / 12)
const C5 = N(3), E5 = N(7), G5 = N(10), C6 = N(15)

export const synthRecipes = {
  // A short airy swoosh: your message went out.
  chatSent(ctx, out, t) {
    noise(ctx, out, t, 0.16, { filter: 'bandpass', freq: 2400, q: 1.2, gain: 0.07 })
    tone(ctx, out, 700, t, 0.12, { to: 1400, gain: 0.05 })
    return 0.2
  },
  // Fanfare-ish arpeggio with a sustained chord.
  gameStart(ctx, out, t) {
    const arp = [C5, E5, G5, C6]
    arp.forEach((f, i) => tone(ctx, out, f, t + i * 0.1, 0.25, { type: 'sawtooth', gain: 0.045, attack: 0.01 }))
    arp.forEach((f, i) => bell(ctx, out, f, t + i * 0.1, 0.3, 0.07))
    arp.forEach((f) => tone(ctx, out, f, t + 0.4, 0.9, { type: 'triangle', gain: 0.06, attack: 0.04 }))
    return 1.35
  },
} satisfies Record<string, Recipe>

/** The keys League has no sound for. */
export type SynthKey = keyof typeof synthRecipes
