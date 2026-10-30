import { installAudioUnlock } from './index'
import { installMusic } from './music'
import { installAudioSession } from './session'

/** Wire the sound engines to the page once, at startup (main.ts): importing them alone has no effects. */
export function installSound() {
  installAudioUnlock()
  installMusic()
  installAudioSession()
}
