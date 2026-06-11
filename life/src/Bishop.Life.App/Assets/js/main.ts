/* SPA bootstrap. Receives whole-file envelopes from the .NET host via the
   message-bus module, dispatches inbound events to apply-envelope and the
   stand-up module, and routes container events to inline mutators that
   post {type:"mutate", plan:…} back to the host.

   Per-tab render functions live in ./tabs/, the oscilloscope viz in
   ./speak-viz.ts, and stand-up modal/transcript orchestration in
   ./standup.ts. This file only owns global state wiring, the title /
   goal-horizon inline editors, container event delegation, and tab
   switching. */

import {
  state,
  ACTION_HORIZONS,
  ACTION_HORIZON_KEYS,
  DEFAULT_ACTION_HORIZON,
  allActions,
  findAction,
  type ViewName,
} from "./plan-state.js";
import { postToHost } from "./message-bus.js";
import { installDispatcher } from "./dispatcher.js";
import { initStandup } from "./standup.js";
import { initDebugConsole } from "./debug-console.js";
import { initSpeakViz, startSpeakViz, stopSpeakViz, revealResting, hideSpeakViz } from "./speak-viz.js";
import { focusView } from "./tabs/focus.js";
import { mapView } from "./tabs/map.js";
import { horizonsView } from "./tabs/horizons.js";
import { balanceView } from "./tabs/balance.js";
import { inboxView } from "./tabs/inbox.js";
import { openGoalHorizonEditor } from "./goal-horizon-editor.js";
import { esc } from "./dom-utils.js";
import type { PlanStateEnvelope } from "./envelopes.js";

let titleDebounce: number | null = null;
let titleDebounceTarget: { id: string; el: HTMLElement } | null = null;

function setSaved(text: string): void {
  const el = document.getElementById("saved");
  if (el) el.textContent = text;
}

function setFilePath(p: string): void {
  const el = document.getElementById("filePath");
  if (el) el.textContent = p || "";
}

function postMutation(): void {
  if (!state.plan) return;
  // Pass the object directly — JSON.stringify on our side would make WebView2
  // deliver it as a JSON-encoded String (ValueKind.String on the host), which
  // the host's string branch only recognizes for the bare "standup"/"init"
  // commands and silently drops everything else. As an object it arrives as
  // ValueKind.Object and the {type:"mutate",plan} handler runs.
  postToHost({ type: "mutate", plan: state.plan });
  setSaved("saving…");
}

function flushPendingTitleEdit(): void {
  if (!titleDebounceTarget) return;
  if (titleDebounce !== null) clearTimeout(titleDebounce);
  const { id, el } = titleDebounceTarget;
  titleDebounce = null;
  titleDebounceTarget = null;
  commitTitle(id, el.textContent ?? "");
}

function commitTitle(id: string, value: string): void {
  const a = findAction(id);
  if (!a) return;
  const trimmed = (value || "").replace(/\s+/g, " ").trim();
  if (!trimmed || trimmed === a.title) return;
  a.title = trimmed;
  postMutation();
}

const VIEW_RENDERERS: Record<ViewName, () => string> = {
  focus: focusView,
  map: mapView,
  horizons: horizonsView,
  balance: balanceView,
  inbox: inboxView,
};

function render(): void {
  const nFocus = document.getElementById("nFocus");
  if (nFocus) {
    nFocus.textContent =
      String(allActions().filter(x => x.action.starred && !x.action.done).length || "");
  }
  const nInbox = document.getElementById("nInbox");
  if (nInbox) {
    nInbox.textContent = String((state.plan && state.plan.inbox ? state.plan.inbox.length : 0) || "");
  }

  const c = document.getElementById("container");
  if (!c) return;
  if (state.status === "missing") {
    c.innerHTML = `<div class="empty" style="margin-top:24px">No bishop.life file at <b>${esc(state.filePath)}</b>. Click <b>Initialize Life</b> above to seed it.</div>`;
    return;
  }
  if (state.status === "error") {
    c.innerHTML = `<div class="empty" style="margin-top:24px;color:var(--danger)">Couldn't read <b>${esc(state.filePath)}</b>.</div>`;
    return;
  }
  if (!state.plan) {
    c.innerHTML = `<div class="empty" style="margin-top:24px">Waiting for state from host&hellip;</div>`;
    return;
  }

  c.innerHTML = VIEW_RENDERERS[state.currentView]();
}

document.querySelector(".tabs")?.addEventListener("click", e => {
  const target = e.target as HTMLElement | null;
  const tab = target?.closest<HTMLElement>(".tab");
  if (!tab) return;
  document.querySelectorAll(".tab").forEach(x => x.classList.remove("active"));
  tab.classList.add("active");
  const view = tab.dataset["view"] as ViewName | undefined;
  if (view) state.currentView = view;
  render();
});

function applyEnvelope(env: PlanStateEnvelope): void {
  state.status = env.status || "ok";
  state.filePath = env.filePath || "";
  state.plan = env.plan ?? null;
  state.standupInFlight = !!env.standupInFlight;
  state.addInFlight = !!env.addInFlight;
  const anyInFlight = state.standupInFlight || state.addInFlight;
  setFilePath(state.filePath);
  setSaved(state.status === "ok"
    ? "loaded · " + new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    : state.status);
  // Stand-up and add both run even when no file exists yet (stand-up seeds
  // one, add appends to inbox), so enable both buttons on any envelope from
  // the host rather than gating on plan data. While either skill is in flight,
  // both CTAs are disabled to prevent overlapping launches.
  const standupBtn = document.getElementById("standupBtn") as HTMLButtonElement | null;
  const addBtn = document.getElementById("addBtn") as HTMLButtonElement | null;
  const initBtn = document.getElementById("initBtn") as HTMLButtonElement | null;
  // Card #1081: while a stand-up is live the button switches to "End Stand-Up"
  // (relabel happens in standup.ts on terminal:show/hide) — it must stay
  // clickable so the user can wrap the session, so only an add-in-flight gates
  // it. When idle, both in-flights still disable it.
  if (standupBtn) standupBtn.disabled = state.standupInFlight ? false : anyInFlight;
  if (addBtn) addBtn.disabled = anyInFlight;
  if (initBtn) initBtn.hidden = state.status !== "missing" || anyInFlight;
  const standupBanner = document.getElementById("standupBanner");
  const addBanner = document.getElementById("addBanner");
  if (standupBanner) standupBanner.hidden = !state.standupInFlight;
  if (addBanner) addBanner.hidden = !state.addInFlight;
  document.body.classList.toggle("standup-disabled", anyInFlight);
  // Drop any pending edit — its target node is about to be replaced by render().
  titleDebounce = null;
  titleDebounceTarget = null;
  render();
}

const container = document.getElementById("container");

container?.addEventListener("click", e => {
  const target = e.target as HTMLElement | null;
  if (!target) return;
  const gPill = target.closest<HTMLElement>(".g-pill[data-goal-id]");
  if (gPill) {
    if (state.standupInFlight || state.addInFlight) return;
    openGoalHorizonEditor(gPill, { render, postMutation });
    return;
  }
  const host = target.closest<HTMLElement>("[data-action-id]");
  if (!host) return;
  if (state.standupInFlight || state.addInFlight) return;
  const id = host.dataset["actionId"];
  if (!id) return;
  const action = findAction(id);
  if (!action) return;

  if (target.closest(".check")) {
    flushPendingTitleEdit();
    action.done = !action.done;
    action.completedAt = action.done ? new Date().toISOString() : null;
    postMutation();
    render();
  } else if (target.closest(".star")) {
    flushPendingTitleEdit();
    action.starred = !action.starred;
    postMutation();
    render();
  } else if (target.closest(".h-pill")) {
    flushPendingTitleEdit();
    const cur = ACTION_HORIZONS[action.horizon] ? action.horizon : DEFAULT_ACTION_HORIZON;
    const idx = ACTION_HORIZON_KEYS.indexOf(cur);
    const next = ACTION_HORIZON_KEYS[(idx + 1) % ACTION_HORIZON_KEYS.length];
    if (next) action.horizon = next;
    postMutation();
    render();
  }
});

container?.addEventListener("input", e => {
  const target = e.target as HTMLElement | null;
  const text = target?.closest<HTMLElement>(".act-text[contenteditable='true']");
  if (!text) return;
  const host = text.closest<HTMLElement>("[data-action-id]");
  if (!host) return;
  const id = host.dataset["actionId"];
  if (!id) return;
  if (state.standupInFlight || state.addInFlight) {
    text.textContent = findAction(id)?.title || "";
    return;
  }
  if (titleDebounce !== null) clearTimeout(titleDebounce);
  titleDebounceTarget = { id, el: text };
  titleDebounce = window.setTimeout(flushPendingTitleEdit, 500);
});

container?.addEventListener("blur", e => {
  const target = e.target as HTMLElement | null;
  const text = target?.closest && target.closest<HTMLElement>(".act-text[contenteditable='true']");
  if (!text) return;
  flushPendingTitleEdit();
}, true);

container?.addEventListener("keydown", e => {
  const target = e.target as HTMLElement | null;
  const text = target?.closest && target.closest<HTMLElement>(".act-text[contenteditable='true']");
  if (!text) return;
  if (e.key === "Enter") {
    e.preventDefault();
    text.blur();
  } else if (e.key === "Escape") {
    const host = text.closest<HTMLElement>("[data-action-id]");
    const id = host?.dataset["actionId"];
    const action = id ? findAction(id) : null;
    if (action) text.textContent = action.title;
    if (titleDebounce !== null) clearTimeout(titleDebounce);
    titleDebounce = null;
    titleDebounceTarget = null;
    text.blur();
  }
});

const vizPanel = document.getElementById("vizPanel");
const vizCanvas = document.getElementById("vizCanvas") as HTMLCanvasElement | null;
if (vizPanel && vizCanvas) initSpeakViz(vizPanel, vizCanvas);

// Stand-up owns viz panel visibility but delegates the actual draw call back
// here via callbacks so all canvas work stays inside speak-viz.
initStandup({
  onVizReveal: () => revealResting(),
  onVizHide: () => hideSpeakViz(),
});
initDebugConsole();

installDispatcher({
  applyEnvelope,
  onSpeakStarted: startSpeakViz,
  onSpeakStopped: stopSpeakViz,
});

document.getElementById("standupBtn")?.addEventListener("click", () => {
  // Card #1081: during a live session the same button ends the stand-up;
  // body.standup-session-active tracks show/hide envelopes from the host.
  if (document.body.classList.contains("standup-session-active")) {
    postToHost({ type: "standup:end" });
    return;
  }
  if (state.standupInFlight || state.addInFlight) return;
  flushPendingTitleEdit();
  postToHost("standup");
});

document.getElementById("addBtn")?.addEventListener("click", () => {
  if (state.standupInFlight || state.addInFlight) return;
  flushPendingTitleEdit();
  postToHost("add");
});

document.getElementById("initBtn")?.addEventListener("click", () => {
  postToHost("init");
});

render();
