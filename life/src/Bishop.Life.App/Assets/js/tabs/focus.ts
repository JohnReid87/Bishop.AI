// Focus view — today's reflection from the latest stand-up, starred-and-open
// actions with goal/area lineage, and a mini-list of goals on the month horizon.
// Pure render: returns an HTML string built from `state`. Click handlers live
// in main.ts so all event wiring stays in one place.

import { state, allActions, allGoals, bucket, type GoalLineage } from "../plan-state.js";
import { esc, attr } from "../dom-utils.js";

export function focusView(): string {
  const plan = state.plan;
  if (!plan) return "";
  let h = '<div class="view">';
  const lastStandup = (plan.standups || []).slice(-1)[0];
  if (lastStandup) {
    const focusIds = new Set(lastStandup.focusToday || []);
    const focusTitles = allActions().filter(x => focusIds.has(x.action.id)).map(x => x.action.title);
    h += `<div class="today"><h4>Today &middot; from latest stand-up</h4>
      <div class="refl">${esc(lastStandup.reflection || "")}</div>
      ${focusTitles.length ? `<ul>${focusTitles.map(t => `<li>${esc(t)}</li>`).join("")}</ul>` : ""}
    </div>`;
  }

  const starred = allActions().filter(x => x.action.starred && !x.action.done);
  h += `<div class="sect">What matters now</div><div class="sect-sub">Starred actions, each tied to the goal it serves.</div>`;
  if (!starred.length) {
    h += `<div class="empty">Nothing starred. Run a stand-up to set today's focus.</div>`;
  } else {
    h += starred.map(({ action, goal, area }) => `
      <div class="card" style="display:flex;align-items:center;gap:11px" ${attr(action.id)}>
        <div class="check ${action.done ? "on" : ""}"></div>
        <div style="flex:1">
          <div class="act-text" style="font-size:13.5px" contenteditable="true" spellcheck="false">${esc(action.title)}</div>
          <div class="lineage" style="margin-top:3px"><span class="dot" style="background:${esc(area.color)}"></span> <b>${esc(area.name)}</b> &#9656; ${esc(goal.name)}</div>
        </div>
        <span class="star on">&#9733;</span>
      </div>`).join("");
  }

  const monthGoals = allGoals().filter(x => bucket(x.goal.horizon) === "month");
  h += `<div class="sect">This month's targets</div><div class="sect-sub">Goals on the nearest horizon.</div>`;
  if (!monthGoals.length) {
    h += `<div class="empty">No goals on the month horizon.</div>`;
  } else {
    h += monthGoals.map(goalMini).join("");
  }
  return h + "</div>";
}

function goalMini({ goal, area }: GoalLineage): string {
  const acts = goal.actions || [];
  const done = acts.filter(a => a.done).length;
  return `<div class="card" style="display:flex;align-items:center;gap:10px">
    <span class="dot" style="background:${esc(area.color)}"></span>
    <div style="flex:1">
      <div style="font-weight:500;font-size:13.5px">${esc(goal.name)}</div>
      <div class="lineage" style="margin-top:2px">${esc(area.name)}</div>
    </div>
    <span class="progress">${done}/${acts.length}</span>
  </div>`;
}
