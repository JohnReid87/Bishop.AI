// Thin wrapper around WebView2's window.chrome.webview transport. Every other
// module imports from here instead of feature-detecting `window.chrome?.webview`
// at every call site — and a future test harness can swap the implementation.

import type { InboundEnvelope, OutboundPayload } from "./envelopes.js";

interface WebViewBridge {
  postMessage(payload: unknown): void;
  addEventListener(type: "message", handler: (evt: { data: unknown }) => void): void;
}

interface ChromeBridge {
  webview?: WebViewBridge;
}

declare global {
  interface Window {
    chrome?: ChromeBridge;
  }
}

export function postToHost(payload: OutboundPayload): void {
  const webview = window.chrome?.webview;
  if (!webview) return;
  webview.postMessage(payload);
}

export function onHostMessage(handler: (env: InboundEnvelope) => void): void {
  const webview = window.chrome?.webview;
  if (!webview) return;
  webview.addEventListener("message", evt => {
    const env = typeof evt.data === "string"
      ? (JSON.parse(evt.data) as InboundEnvelope)
      : (evt.data as InboundEnvelope);
    handler(env);
  });
}
