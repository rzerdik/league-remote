import { computed } from 'vue'
import { useAnswers } from '../../composables/useAnswers'
import type { ChampSelectState, SwapOffer } from '../../contracts'
import { championName } from '../../ddragon'
import { receivedSwaps } from '../../draft'
import { haptic, tapFeedback } from '../../haptics'
import { positionLabel } from '../../labels'
import { sendCommand } from '../../hub'
import { playerShortName } from '../../riotId'
import { toast } from '../../store'
import type { SwapCommand } from './swaps'

const swapKey = (s: SwapOffer) => `${s.kind}:${s.id}`

/**
 * Pick-order swaps, role swaps and champion trades with teammates: what each teammate row's menu offers, the requests
 * waiting for your answer, and one swap command at a time (requests from the menus, answers from the banners).
 */
export function useSwaps(cs: () => ChampSelectState) {
  /** Offers to show on a teammate row: ones League lets us send, or we already sent. */
  function rowOffers(cellId: number): SwapOffer[] {
    return (cs().swaps ?? []).filter(
      (s) => s.id > 0 && s.cellId === cellId && (s.state === 'AVAILABLE' || s.state === 'SENT'),
    )
  }

  const requests = computed(() => receivedSwaps(cs()))

  /** A request banner's message ("Kestrel", "Bot", "Jinx ⇄ Ahri"): its title already says what kind of swap it is. */
  function requestText(s: SwapOffer): string {
    const team = cs().myTeam
    const other = team.find((m) => m.cellId === s.cellId)
    const me = team.find((m) => m.isLocalPlayer)
    const who = playerShortName(other?.riotId ?? '') || positionLabel(other?.assignedPosition) || 'Teammate'
    if (s.kind === 'PickOrder') return who
    const [theirs, mine] =
      s.kind === 'Position'
        ? [positionLabel(other?.assignedPosition), positionLabel(me?.assignedPosition)]
        : [championName(other?.championId ?? 0), championName(me?.championId ?? 0)]
    return theirs && mine ? `${theirs} ⇄ ${mine}` : who
  }

  const answers = useAnswers<SwapCommand>()
  const busy = computed(() => !!answers.inFlight.value)

  function send(type: SwapCommand, s: SwapOffer) {
    if (type === 'AcceptSwap' || type === 'DeclineSwap') tapFeedback()
    void answers.answer(swapKey(s), type, async () => {
      const r = await sendCommand({ type, swapKind: s.kind, swapId: s.id })
      if (!r?.success) return
      if (type === 'RequestSwap') toast('Request sent', 'success')
      else if (type === 'AcceptSwap') haptic('success')
    })
  }

  /** Which of a request's two buttons shows the spinner. */
  function answerBusy(s: SwapOffer): 'accept' | 'decline' | null {
    const kind = answers.busyFor(swapKey(s))
    return kind === 'AcceptSwap' ? 'accept' : kind === 'DeclineSwap' ? 'decline' : null
  }

  return { rowOffers, requests, requestText, send, busy, answerBusy, swapKey }
}
