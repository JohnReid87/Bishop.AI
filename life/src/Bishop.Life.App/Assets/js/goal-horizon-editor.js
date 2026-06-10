// Inline editor for the goal-level horizon chip (`.g-pill`). Click swaps the
// chip for an <input type="month"> focused for edit; commit on blur/Enter,
// revert on Escape. Empty input clears the horizon to null; otherwise the
// value must match YYYY-MM (01–12) — anything else is rejected silently and
// the chip re-renders to its prior state.
//
// On a successful commit the caller is asked to re-render (via the `render`
// callback) and the mutated plan is pushed back to the host through
// `postMutation`. The editor itself is stateless — every invocation creates a
// fresh input element bound to the goal id encoded on the chip.

import { findGoal } from "./plan-state.js";

export function openGoalHorizonEditor(pill, { render, postMutation }) {
  const id = pill.dataset.goalId;
  const goal = findGoal(id); if (!goal) return;
  const current = /^\d{4}-(0[1-9]|1[0-2])$/.test(String(goal.horizon || "")) ? goal.horizon : "";
  const input = document.createElement("input");
  input.type = "month";
  input.className = "g-pill-edit";
  input.value = current;
  input.dataset.goalId = id;
  pill.replaceWith(input);
  input.focus();
  let done = false;
  const finish = (commit) => {
    if (done) return; done = true;
    if (commit) commitGoalHorizon(id, input.value, postMutation);
    render();
  };
  input.addEventListener("blur", () => finish(true));
  input.addEventListener("keydown", ev => {
    if (ev.key === "Enter") { ev.preventDefault(); finish(true); }
    else if (ev.key === "Escape") { ev.preventDefault(); finish(false); }
  });
}

function commitGoalHorizon(id, value, postMutation) {
  const g = findGoal(id); if (!g) return false;
  const v = (value || "").trim();
  if (v === "") {
    if (g.horizon == null) return false;
    g.horizon = null;
  } else if (/^\d{4}-(0[1-9]|1[0-2])$/.test(v)) {
    if (v === g.horizon) return false;
    g.horizon = v;
  } else {
    return false;
  }
  postMutation();
  return true;
}
