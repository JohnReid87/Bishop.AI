// Thin wrapper around WebView2's window.chrome.webview transport. Every other
// module imports from here instead of feature-detecting `window.chrome?.webview`
// at every call site — and a future test harness can swap the implementation.

export function postToHost(payload) {
  if (!window.chrome || !window.chrome.webview) return;
  window.chrome.webview.postMessage(payload);
}

export function onHostMessage(handler) {
  if (!window.chrome || !window.chrome.webview) return;
  window.chrome.webview.addEventListener("message", evt => {
    const env = typeof evt.data === "string" ? JSON.parse(evt.data) : evt.data;
    handler(env);
  });
}
