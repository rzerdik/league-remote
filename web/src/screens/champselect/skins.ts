import type { ChampionSkin, SkinChroma, SkinSelection } from '../../contracts'

/** The skin a skin or chroma id belongs to, and the chroma when it is one. */
export function findSkin(selection: SkinSelection | null | undefined, id: number): { skin: ChampionSkin; chroma: SkinChroma | null } | null {
  if (!selection || id <= 0) return null
  for (const skin of selection.skins) {
    if (skin.id === id) return { skin, chroma: null }
    const chroma = skin.chromas.find((c) => c.id === id)
    if (chroma) return { skin, chroma }
  }
  return null
}

/** What the Skin row and VoiceOver call the equipped choice: the chroma's name, else the skin's. */
export function choiceName(selection: SkinSelection | null | undefined, id: number): string {
  const found = findSkin(selection, id)
  return found ? (found.chroma?.name || found.skin.name) : ''
}

/** A chroma's own part of its name under its skin's: "Rose Quartz" for "Star Guardian Ahri (Rose Quartz)". */
export function chromaLabel(skin: ChampionSkin, chroma: SkinChroma): string {
  const prefix = `${skin.name} (`
  return chroma.name.startsWith(prefix) && chroma.name.endsWith(')') ? chroma.name.slice(prefix.length, -1) : chroma.name
}

/**
 * A chroma's swatch as the client paints it: one colour, or a 135° split of the first two. The connector only passes
 * `#RRGGBB` values; anything else is ignored here too, since it ends up in a style. Null: no colours (tiered forms).
 */
export function swatchBackground(colors: readonly string[]): string | null {
  const [a, b] = colors.filter((c) => /^#[0-9A-Fa-f]{6}$/.test(c))
  if (!a) return null
  return b ? `linear-gradient(135deg, ${a} 0%, ${a} 50%, ${b} 50%, ${b} 100%)` : a
}

/** A random skin or chroma other than `currentId` (every equippable choice counts once); null when there is no other. */
export function randomChoice(selection: SkinSelection, currentId: number, random = Math.random): number | null {
  const ids = selection.skins.flatMap((s) => [s.id, ...s.chromas.map((c) => c.id)]).filter((id) => id !== currentId)
  return ids.length ? ids[Math.floor(random() * ids.length)]! : null
}
