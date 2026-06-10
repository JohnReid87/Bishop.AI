// Shared HTML-escape helpers used across tab render modules. Kept tiny on
// purpose — the tab views build their HTML as strings, and every interpolated
// value flows through esc() so user content can't inject markup.

const ESC_MAP: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" };

export const esc = (s: unknown): string =>
  String(s ?? "").replace(/[&<>"]/g, c => ESC_MAP[c] ?? c);

export function attr(id: string): string {
  return `data-action-id="${esc(id)}"`;
}
