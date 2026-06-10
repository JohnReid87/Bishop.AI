// Horizons view — the same goals as the Map, but resorted by how far out
// they sit. One lane per horizon bucket (month / year / beyond).

import { state, HORIZONS, allGoals, bucket } from "../plan-state.js";
import { esc } from "../dom-utils.js";

export function horizonsView() {
  let h = `<div class="view"><div class="sect">Horizons</div><div class="sect-sub">The same goals, sorted by how far out they sit.</div><div class="lanes">`;
  for (const [k, label] of Object.entries(HORIZONS)) {
    const items = allGoals().filter(x => bucket(x.goal.horizon) === k);
    h += `<div class="lane ${k}"><div class="lane-title">${label} &middot; ${items.length}</div>`;
    h += items.length
      ? items.map(({ goal, area }) => {
          const acts = goal.actions || [];
          const done = acts.filter(a => a.done).length;
          return `<div class="mini"><div class="t">${esc(goal.name)}</div>
            <div class="m"><span class="dot" style="background:${esc(area.color)}"></span>
              <span class="lineage">${esc(area.name)}</span>
              <span class="progress" style="margin-left:auto">${done}/${acts.length}</span>
            </div></div>`;
        }).join("")
      : `<div class="empty" style="padding:13px;font-size:12px">empty</div>`;
    h += "</div>";
  }
  return h + "</div></div>";
}
