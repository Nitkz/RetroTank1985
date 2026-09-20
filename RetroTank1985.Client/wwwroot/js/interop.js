export function isStandalone() {
    return !!window.__IS_STANDALONE_WASM__ || !!document.querySelector('script[src*="blazor.webassembly.js"]');
}
