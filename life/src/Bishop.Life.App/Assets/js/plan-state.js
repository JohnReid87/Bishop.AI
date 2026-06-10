// Single source of truth for the SPA's view of the LifePlan. The host posts
// whole-file envelopes; applyEnvelope (inline in index.html) writes back into
// `state`, then inline mutators (star/check/title/horizon) read and mutate
// `state.plan` in place and post the mutated plan back as
// {type:"mutate", plan:…}. Every module that needs to read the current plan
// imports `state` and the helpers below so all paths share one live object.

export const HORIZONS = { month: "This month", year: "This year", beyond: "Beyond" };

// Action-level horizon enum (matches Bishop.Life.Core.Schema.Horizon serialized
// via JsonStringEnumConverter w/ camelCase). Order is the cycle order on click.
export const ACTION_HORIZONS = { today: "Today", thisWeek: "This week", thisMonth: "This month", someday: "Someday" };
export const ACTION_HORIZON_KEYS = Object.keys(ACTION_HORIZONS);
export const DEFAULT_ACTION_HORIZON = "thisWeek";

export const state = {
  plan: null,
  status: "waiting",
  filePath: "",
  currentView: "focus",
  standupInFlight: false,
  addInFlight: false,
};

export function bucket(horizon) {
  if (!horizon) return "beyond";
  const parts = String(horizon).split("-");
  const y = Number(parts[0]), m = Number(parts[1]);
  if (!y || !m) return "beyond";
  const now = new Date();
  const diff = (y - now.getFullYear()) * 12 + (m - (now.getMonth() + 1));
  if (diff <= 3) return "month";
  if (y === now.getFullYear()) return "year";
  return "beyond";
}

export function allGoals() {
  if (!state.plan) return [];
  return (state.plan.areas || []).flatMap(a => (a.goals || []).map(g => ({ goal: g, area: a })));
}

export function allActions() {
  if (!state.plan) return [];
  return (state.plan.areas || []).flatMap(a => (a.goals || []).flatMap(g => (g.actions || []).map(act => ({ action: act, goal: g, area: a }))));
}

export function findAction(id) {
  if (!state.plan || !id) return null;
  for (const a of state.plan.areas || [])
    for (const g of a.goals || [])
      for (const act of g.actions || [])
        if (act.id === id) return act;
  return null;
}

export function findGoal(id) {
  if (!state.plan || !id) return null;
  for (const a of state.plan.areas || [])
    for (const g of a.goals || [])
      if (g.id === id) return g;
  return null;
}

export function actionHorizonKey(a) {
  return ACTION_HORIZONS[a.horizon] ? a.horizon : DEFAULT_ACTION_HORIZON;
}
