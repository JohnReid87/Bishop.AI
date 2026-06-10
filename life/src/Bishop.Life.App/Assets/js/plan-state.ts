// Single source of truth for the SPA's view of the LifePlan. The host posts
// whole-file envelopes; applyEnvelope (in main.ts) writes back into `state`,
// then inline mutators (star/check/title/horizon) read and mutate `state.plan`
// in place and post the mutated plan back as {type:"mutate", plan:…}. Every
// module that needs to read the current plan imports `state` and the helpers
// below so all paths share one live object.

import type { Area, Goal, LifeAction, LifePlan, Horizon } from "./schema.js";

export const HORIZONS = { month: "This month", year: "This year", beyond: "Beyond" } as const;
export type HorizonBucket = keyof typeof HORIZONS;

// Action-level horizon enum (matches Bishop.Life.Core.Schema.Horizon serialized
// via JsonStringEnumConverter w/ camelCase). Order is the cycle order on click.
export const ACTION_HORIZONS = {
  today: "Today",
  thisWeek: "This week",
  thisMonth: "This month",
  someday: "Someday",
} as const satisfies Record<Horizon, string>;
export const ACTION_HORIZON_KEYS = Object.keys(ACTION_HORIZONS) as Horizon[];
export const DEFAULT_ACTION_HORIZON: Horizon = "thisWeek";

export type ViewName = "focus" | "map" | "horizons" | "balance" | "inbox";
export type PlanStatus = "waiting" | "ok" | "missing" | "error" | string;

export interface SpaState {
  plan: LifePlan | null;
  status: PlanStatus;
  filePath: string;
  currentView: ViewName;
  standupInFlight: boolean;
  addInFlight: boolean;
}

export const state: SpaState = {
  plan: null,
  status: "waiting",
  filePath: "",
  currentView: "focus",
  standupInFlight: false,
  addInFlight: false,
};

export function bucket(horizon: string | null | undefined): HorizonBucket {
  if (!horizon) return "beyond";
  const parts = String(horizon).split("-");
  const y = Number(parts[0]);
  const m = Number(parts[1]);
  if (!y || !m) return "beyond";
  const now = new Date();
  const diff = (y - now.getFullYear()) * 12 + (m - (now.getMonth() + 1));
  if (diff <= 3) return "month";
  if (y === now.getFullYear()) return "year";
  return "beyond";
}

export interface GoalLineage {
  goal: Goal;
  area: Area;
}

export interface ActionLineage {
  action: LifeAction;
  goal: Goal;
  area: Area;
}

export function allGoals(): GoalLineage[] {
  if (!state.plan) return [];
  return (state.plan.areas || []).flatMap(a => (a.goals || []).map(g => ({ goal: g, area: a })));
}

export function allActions(): ActionLineage[] {
  if (!state.plan) return [];
  return (state.plan.areas || []).flatMap(a =>
    (a.goals || []).flatMap(g =>
      (g.actions || []).map(act => ({ action: act, goal: g, area: a }))
    )
  );
}

export function findAction(id: string | null | undefined): LifeAction | null {
  if (!state.plan || !id) return null;
  for (const a of state.plan.areas || [])
    for (const g of a.goals || [])
      for (const act of g.actions || [])
        if (act.id === id) return act;
  return null;
}

export function findGoal(id: string | null | undefined): Goal | null {
  if (!state.plan || !id) return null;
  for (const a of state.plan.areas || [])
    for (const g of a.goals || [])
      if (g.id === id) return g;
  return null;
}

export function actionHorizonKey(a: LifeAction): Horizon {
  return ACTION_HORIZONS[a.horizon] ? a.horizon : DEFAULT_ACTION_HORIZON;
}
