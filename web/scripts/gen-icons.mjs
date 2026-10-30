// Generates the PWA icons and the connector's app.ico (original artwork: the "signal L" mark — a geometric L whose inner
// corner radiates a remote signal) without any dependencies.
// Usage: node scripts/gen-icons.mjs
import { writeFileSync } from 'node:fs'
import { deflateSync } from 'node:zlib'
import { fileURLToPath } from 'node:url'

const out = fileURLToPath(new URL('../public/', import.meta.url))
const BG = [0x0e, 0x10, 0x13]
const FG = [0xda, 0xb9, 0x75] // the app's gold

const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
  return c >>> 0
})
function crc32(buf) {
  let c = 0xffffffff
  for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8)
  return (c ^ 0xffffffff) >>> 0
}
function chunk(type, data) {
  const len = Buffer.alloc(4)
  len.writeUInt32BE(data.length)
  const td = Buffer.concat([Buffer.from(type, 'ascii'), data])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(td))
  return Buffer.concat([len, td, crc])
}

/**
 * The mark on a 256 grid (bounding box x 48..208, y 40..208): an L (stem 40 wide, foot 40 tall) and, nested in its inner
 * corner around (104, 152), a quarter disc (r 36) and a quarter ring (r 56..84). MARK_SVG is the same geometry as paths.
 */
function mark(X, Y) {
  if ((X >= 48 && X <= 88 && Y >= 40 && Y <= 208) || (X >= 48 && X <= 208 && Y >= 168 && Y <= 208)) return true
  if (X < 104 || Y > 152) return false
  const d = Math.hypot(X - 104, Y - 152)
  return d <= 36 || (d >= 56 && d <= 84)
}
const MARK_SVG =
  '<path d="M48 40h40v128h120v40H48zM104 152v-36a36 36 0 0 1 36 36zM104 96V68a84 84 0 0 1 84 84h-28a56 56 0 0 0-56-56z"/>'
const MARK_CENTER = [128, 124]

/** Coverage of the glyph at (x, y) in a unit square; `span` = how many mark units the square covers (bigger = smaller mark). */
function glyph(x, y, span = 360) {
  return mark((x - 0.5) * span + MARK_CENTER[0], (y - 0.5) * span + MARK_CENTER[1])
}

function png(size) {
  const SS = 4
  const raw = Buffer.alloc(size * (size * 3 + 1))
  for (let y = 0; y < size; y++) {
    raw[y * (size * 3 + 1)] = 0
    for (let x = 0; x < size; x++) {
      let hit = 0
      for (let sy = 0; sy < SS; sy++)
        for (let sx = 0; sx < SS; sx++) if (glyph((x + (sx + 0.5) / SS) / size, (y + (sy + 0.5) / SS) / size)) hit++
      const a = hit / (SS * SS)
      const o = y * (size * 3 + 1) + 1 + x * 3
      for (let i = 0; i < 3; i++) raw[o + i] = Math.round(BG[i] * (1 - a) + FG[i] * a)
    }
  }
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(size, 0)
  ihdr.writeUInt32BE(size, 4)
  ihdr[8] = 8 // bit depth
  ihdr[9] = 2 // RGB
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

for (const [name, size] of [['icon-192.png', 192], ['icon-512.png', 512], ['apple-touch-icon.png', 180]]) {
  writeFileSync(out + name, png(size))
  console.log('wrote', name)
}

/** RGBA PNG: rounded-square background (transparent corners) + glyph, for the Windows .ico. */
function roundedPng(size) {
  const SS = 4
  const radius = 0.22
  const inRoundedSquare = (x, y) => {
    const cx = Math.min(Math.max(x, radius), 1 - radius)
    const cy = Math.min(Math.max(y, radius), 1 - radius)
    return Math.hypot(x - cx, y - cy) <= radius
  }
  const raw = Buffer.alloc(size * (size * 4 + 1))
  for (let y = 0; y < size; y++) {
    raw[y * (size * 4 + 1)] = 0
    for (let x = 0; x < size; x++) {
      let bg = 0
      let fg = 0
      for (let sy = 0; sy < SS; sy++)
        for (let sx = 0; sx < SS; sx++) {
          const u = (x + (sx + 0.5) / SS) / size
          const v = (y + (sy + 0.5) / SS) / size
          if (!inRoundedSquare(u, v)) continue
          bg++
          if (glyph(u, v, 300)) fg++
        }
      const o = y * (size * 4 + 1) + 1 + x * 4
      const f = bg ? fg / bg : 0
      for (let i = 0; i < 3; i++) raw[o + i] = Math.round(BG[i] * (1 - f) + FG[i] * f)
      raw[o + 3] = Math.round((255 * bg) / (SS * SS))
    }
  }
  const ihdr = Buffer.alloc(13)
  ihdr.writeUInt32BE(size, 0)
  ihdr.writeUInt32BE(size, 4)
  ihdr[8] = 8 // bit depth
  ihdr[9] = 6 // RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ])
}

/** .ico with PNG-compressed entries (supported since Windows Vista). */
function ico(sizes) {
  const images = sizes.map(roundedPng)
  const header = Buffer.alloc(6)
  header.writeUInt16LE(1, 2) // type: icon
  header.writeUInt16LE(sizes.length, 4)
  const entries = Buffer.alloc(16 * sizes.length)
  let offset = header.length + entries.length
  sizes.forEach((size, i) => {
    const e = i * 16
    entries[e] = size >= 256 ? 0 : size
    entries[e + 1] = size >= 256 ? 0 : size
    entries.writeUInt16LE(1, e + 4) // planes
    entries.writeUInt16LE(32, e + 6) // bits per pixel
    entries.writeUInt32LE(images[i].length, e + 8)
    entries.writeUInt32LE(offset, e + 12)
    offset += images[i].length
  })
  return Buffer.concat([header, entries, ...images])
}

const connectorIcon = fileURLToPath(new URL('../../src/LeagueRemote.Connector/Assets/app.ico', import.meta.url))
writeFileSync(connectorIcon, ico([16, 20, 24, 32, 40, 48, 64, 256]))
console.log('wrote', connectorIcon)

// Favicon: rounded tile + the mark, scaled like the .ico (300 units across the tile).
const k = 64 / 300
writeFileSync(
  out + 'favicon.svg',
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="14" fill="#0e1013"/><g fill="#dab975" transform="translate(${32 - MARK_CENTER[0] * k} ${32 - MARK_CENTER[1] * k}) scale(${k})">${MARK_SVG}</g></svg>
`,
)
console.log('wrote favicon.svg')
