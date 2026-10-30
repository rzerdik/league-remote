/** The demo's people. Every name is invented; puuids are `demo-<name>` (Vexa and Tarn: the friends `friend-1` / `-2`). */

export interface DemoPerson {
  gameName: string
  tagLine: string
  puuid: string
}

const person = (gameName: string, tagLine: string, puuid = `demo-${gameName.toLowerCase()}`): DemoPerson => ({
  gameName,
  tagLine,
  puuid,
})

/** "Name#TAG", as the connector names players. */
export const riotIdOf = (p: DemoPerson) => `${p.gameName}#${p.tagLine}`

/** You. */
export const ME = person('Nova', 'EUW')

/** Your party (lobby, champion select): NightOwl and Kestrel are friends, Lumen isn't. */
export const NIGHTOWL = person('NightOwl', 'EUW')
export const KESTREL = person('Kestrel', 'EUW')
export const LUMEN = person('Lumen', '7788')

/** Friends with direct messages in the demo chat. */
export const VEXA = person('Vexa', 'EUW', 'friend-1')
export const TARN = person('Tarn', 'EUNE', 'friend-2')

/**
 * The party as the connector lists it ("Name#TAG"). NightOwl and Kestrel are demo friends (their sheet is the friend
 * profile); Lumen isn't (the party sheet's View Profile still opens her: she's in your party).
 */
export const lobbyMembers = [
  { riotId: riotIdOf(ME), isLeader: true, firstPositionPreference: 'MIDDLE', secondPositionPreference: 'TOP', isLocalPlayer: true, puuid: ME.puuid },
  { riotId: riotIdOf(NIGHTOWL), isLeader: false, firstPositionPreference: 'JUNGLE', secondPositionPreference: 'UTILITY', isLocalPlayer: false, puuid: NIGHTOWL.puuid },
  { riotId: riotIdOf(KESTREL), isLeader: false, firstPositionPreference: 'BOTTOM', secondPositionPreference: 'UTILITY', isLocalPlayer: false, puuid: KESTREL.puuid },
  { riotId: riotIdOf(LUMEN), isLeader: false, firstPositionPreference: 'UTILITY', secondPositionPreference: 'FILL', isLocalPlayer: false, puuid: LUMEN.puuid },
]
