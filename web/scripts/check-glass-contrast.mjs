// Worst-case text contrast on glass, from design/tokens.json:   node scripts/check-glass-contrast.mjs
// Each fill is composited over a white and a black backdrop (with the theme's glass-brightness), then label and
// label-secondary-glass are measured on the result. Prints a table and exits 1 under 4.5:1. Increase Contrast themes
// are only measured on glass-tinted: the glass amount doesn't apply there.
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const tokens = JSON.parse(readFileSync(resolve(root, 'design/tokens.json'), 'utf8'))
const color = (name) => tokens.color.tokens.find((t) => t.name === name).value
const material = (name) => tokens.material.tokens.find((t) => t.name === name).value

function parse(css) {
  const rgba = css.match(/rgba\((\d+),\s*(\d+),\s*(\d+),\s*([\d.]+)\)/)
  if (rgba) return { rgb: [+rgba[1], +rgba[2], +rgba[3]], a: +rgba[4] }
  const hex = css.replace('#', '')
  return { rgb: [0, 2, 4].map((i) => parseInt(hex.slice(i, i + 2), 16)), a: 1 }
}
const over = (top, under) => top.rgb.map((c, i) => c * top.a + under[i] * (1 - top.a))
function luminance(rgb) {
  const [r, g, b] = rgb.map((c) => {
    const s = c / 255
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}
function contrast(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

let failed = false
console.log('| Theme | Fill | Backdrop | label | label-secondary-glass |\n|---|---|---|---|---|')
for (const theme of ['dark', 'light', 'dark-hc', 'light-hc']) {
  const fills = theme.endsWith('-hc') ? ['glass-tinted'] : ['glass-ultraclear', 'glass', 'glass-thick', 'glass-tinted']
  const dim = parseFloat(material('glass-brightness')[theme]) / 100
  for (const fill of fills) {
    for (const [name, backdrop] of [['white', [255, 255, 255]], ['black', [0, 0, 0]]]) {
      const ground = over(parse(color(fill)[theme]), backdrop.map((c) => c * dim))
      const cells = ['label', 'label-secondary-glass'].map((text) => contrast(over(parse(color(text)[theme]), ground), ground))
      if (cells.some((c) => c < 4.5)) failed = true
      console.log(`| ${theme} | ${fill} | ${name} | ${cells[0].toFixed(2)} | ${cells[1].toFixed(2)} |`)
    }
  }
}
process.exit(failed ? 1 : 0)
