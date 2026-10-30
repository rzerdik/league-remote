/**
 * Lazy-loaded (dynamic import) only when the browser has no native BarcodeDetector with QR support (e.g. iOS Safari,
 * Firefox). ZXing-C++ compiled to WebAssembly; the .wasm is self-hosted, not fetched from a CDN. package.json pins
 * `zxing-wasm` to the exact version `barcode-detector` depends on: bump both together, or the scanner breaks silently.
 */
import { BarcodeDetector, prepareZXingModule } from 'barcode-detector/ponyfill'
import wasmUrl from 'zxing-wasm/reader/zxing_reader.wasm?url'

prepareZXingModule({
  overrides: {
    locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasmUrl : prefix + path),
  },
})

export { BarcodeDetector }
