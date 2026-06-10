// Balance view — one row per area showing the count of open actions as a
// horizontal bar tinted with the area colour. Areas with zero goals render
// as "untended" rather than a zero-width bar.

import { state } from "../plan-state.js";
import { esc } from "../dom-utils.js";

export function balanceView() {
  const rows = (state.plan.areas || []).map(area => {
    const goals = area.goals || [];
    const open = goals.reduce((n, g) => n + (g.actions || []).filter(a => !a.done).length, 0);
    return { area, goalCount: goals.length, open };
  });
  const max = Math.max(1, ...rows.map(r => r.open));
  let h = `<div class="view"><div class="sect">Balance</div><div class="sect-sub">Open actions per area. Not a leaderboard &mdash; a way to notice what's gone quiet.</div>`;
  h += rows.map(r => `<div class="bal-row">
    <div class="bal-name"><span class="dot" style="background:${esc(r.area.color)}"></span>${esc(r.area.name)}</div>
    <div class="bar-track"><div class="bar-fill" style="width:${(r.open / max * 100) || 2}%;background:${esc(r.area.color)}"></div></div>
    <div class="bal-count">${r.goalCount ? `${r.goalCount}g &middot; ${r.open} open` : `<span class="untended">untended</span>`}</div>
  </div>`).join("");
  return h + "</div>";
}
