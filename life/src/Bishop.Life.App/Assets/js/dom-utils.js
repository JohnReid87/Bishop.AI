// Shared HTML-escape helpers used across tab render modules. Kept tiny on
// purpose — the tab views build their HTML as strings, and every interpolated
// value flows through esc() so user content can't inject markup.

export const esc = s => String(s ?? "").replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

export function attr(id) { return `data-action-id="${esc(id)}"`; }
