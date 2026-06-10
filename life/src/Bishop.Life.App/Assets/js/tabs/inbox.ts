// Inbox view — read-only list of items captured by `/bish-life-add`.
// Triaged into areas during the next stand-up; rendered newest-first.

import { state } from "../plan-state.js";
import { esc } from "../dom-utils.js";

export function inboxView(): string {
  const plan = state.plan;
  if (!plan) return "";
  const items = (plan.inbox || []).slice().sort((a, b) => {
    const ta = new Date(a.capturedAt || 0).getTime();
    const tb = new Date(b.capturedAt || 0).getTime();
    return tb - ta;
  });
  let h = `<div class="view"><div class="sect">Inbox</div><div class="sect-sub">Captured via <code>/bish-life-add</code>. Triaged into areas during the next stand-up.</div>`;
  if (!items.length) {
    h += `<div class="empty">Inbox is empty. Capture a thought with <code>/bish-life-add</code>.</div>`;
  } else {
    h += items.map(item => `<div class="card" style="display:flex;align-items:flex-start;gap:11px">
      <div style="flex:1">
        <div class="act-text" style="font-size:13.5px">${esc(item.text)}</div>
        <div class="lineage" style="margin-top:3px">${esc(formatCapturedAt(item.capturedAt))}</div>
      </div>
    </div>`).join("");
  }
  return h + "</div>";
}

function formatCapturedAt(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return String(iso);
  return d.toLocaleString([], { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}
