// Map view — areas with per-horizon action sections. Each area block carries
// the area colour as a left border + tinted heading row so areas read as
// distinct slabs at a glance (card #1029). Per-goal horizon chips (`.g-pill`)
// and per-action horizon pills (`.h-pill`) are click targets handled by the
// inline bootstrap.

import { state, ACTION_HORIZONS, ACTION_HORIZON_KEYS, actionHorizonKey, bucket } from "../plan-state.js";
import { esc, attr } from "../dom-utils.js";

// Convert #RRGGBB → rgba() for tinting Map area heading backgrounds without
// baking opacity into the source colour. Falls back to a neutral tint when
// the hex is malformed.
function tint(hex, alpha) {
  const h = String(hex || "").replace("#", "");
  if (h.length !== 6) return `rgba(255,255,255,${alpha})`;
  const r = parseInt(h.slice(0, 2), 16);
  const g = parseInt(h.slice(2, 4), 16);
  const b = parseInt(h.slice(4, 6), 16);
  if ([r, g, b].some(n => Number.isNaN(n))) return `rgba(255,255,255,${alpha})`;
  return `rgba(${r},${g},${b},${alpha})`;
}

function goalHorizonChip(goal) {
  const h = goal.horizon;
  const isYm = /^\d{4}-(0[1-9]|1[0-2])$/.test(String(h || ""));
  const cls = isYm ? bucket(h) : (h ? "beyond" : "empty");
  const label = isYm ? esc(h) : (h ? esc(h) : "—");
  return `<span class="g-pill ${cls}" data-goal-id="${esc(goal.id)}" title="Click to set goal horizon (YYYY-MM)">${label}</span>`;
}

export function mapView() {
  let h = `<div class="view"><div class="sect">The map</div><div class="sect-sub">Actions grouped by horizon within each life area. The urgency at a glance.</div>`;
  h += (state.plan.areas || []).map(area => {
    const goals = area.goals || [];
    const flat = goals.flatMap(g => (g.actions || []).map(a => ({ a, goal: g })));
    const openCount = flat.filter(x => !x.a.done).length;
    let block = `<div class="area-block" style="border-left-color:${esc(area.color)}">
      <div class="area-head" style="background:${esc(tint(area.color, 0.10))}">
      <span class="dot" style="background:${esc(area.color)}"></span>
      <span class="area-name">${esc(area.name)}</span>
      <span class="area-meta">${goals.length} goal${goals.length !== 1 ? "s" : ""} &middot; ${openCount} open</span>
    </div>`;

    for (const g of goals) {
      block += `<div class="goal-block">
        <div class="goal-head">
          <span class="goal-title">${esc(g.name)}</span>
          ${goalHorizonChip(g)}
        </div>
      </div>`;
    }

    // Group actions by horizon section; empty sections collapse (not rendered).
    // Within a section, starred sorts above unstarred so a starred Today action leads its bucket.
    for (const hkey of ACTION_HORIZON_KEYS) {
      const items = flat.filter(x => actionHorizonKey(x.a) === hkey);
      if (!items.length) continue;
      items.sort((x, y) => Number(!!y.a.starred) - Number(!!x.a.starred));
      block += `<div class="h-sect ${hkey}">${ACTION_HORIZONS[hkey]} &middot; ${items.length}</div>`;
      block += items.map(({ a, goal }) => {
        const hk = actionHorizonKey(a);
        return `<div class="act ${a.done ? "done" : ""}" ${attr(a.id)}>
          <div class="check ${a.done ? "on" : ""}"></div>
          <div style="flex:1">
            <span class="act-text" contenteditable="true" spellcheck="false">${esc(a.title)}</span>
            <div class="lineage" style="margin-top:2px">${esc(goal.name)}</div>
          </div>
          <span class="h-pill ${hk}" title="Click to cycle horizon">${ACTION_HORIZONS[hk]}</span>
          <span class="star ${a.starred ? "on" : ""}">${a.starred ? "&#9733;" : "&#9734;"}</span>
        </div>`;
      }).join("");
    }
    if (!flat.length) {
      block += `<div class="empty" style="padding:12px;font-size:12px">No actions in this area.</div>`;
    }
    return block + "</div>";
  }).join("");
  return h + "</div>";
}
