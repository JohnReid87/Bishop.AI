// Discriminated unions for inbound and outbound IPC envelopes between the host
// (Bishop.Life.App C#) and the SPA. The generated schema (schema.d.ts) declares
// each envelope's payload shape with `type: string`; the unions below pin each
// `type` to a string literal so the dispatcher and outbound sites get
// compile-time exhaustiveness — adding a new envelope kind without handling it
// is a type error.

import type {
  LifePlan,
  PlanStateEnvelope as SchemaPlanStateEnvelope,
} from "./schema.js";

// ── Inbound: host → SPA ───────────────────────────────────────────────────

export interface SpeakStartedEnvelope {
  type: "speak.started";
  pcmBase64?: string | null;
  pcmSampleRateHz?: number;
  durationMs?: number;
}

export interface SpeakStoppedEnvelope {
  type: "speak.stopped";
}

export interface TerminalShowEnvelope {
  type: "terminal:show";
}

export interface TerminalHideEnvelope {
  type: "terminal:hide";
}

export interface TerminalSystemNoteEnvelope {
  type: "terminal:systemNote";
  text?: string;
}

export interface TranscriptEventEnvelope {
  type: "transcript:event";
  kind: string;
  text?: string;
}

// Dispatcher-routed envelopes — anything else from the host is treated as a
// whole-plan envelope (which has no `type` field).
export type DispatchedInboundEnvelope =
  | SpeakStartedEnvelope
  | SpeakStoppedEnvelope
  | TerminalShowEnvelope
  | TerminalHideEnvelope
  | TerminalSystemNoteEnvelope
  | TranscriptEventEnvelope;

// The whole-plan envelope has no `type` field, so it can't be distinguished by
// `env.type` alone. The dispatcher uses isDispatched() below as the type guard.
export type PlanStateEnvelope = SchemaPlanStateEnvelope;

export type InboundEnvelope = DispatchedInboundEnvelope | PlanStateEnvelope;

const DISPATCHED_TYPES: ReadonlySet<DispatchedInboundEnvelope["type"]> = new Set([
  "speak.started",
  "speak.stopped",
  "terminal:show",
  "terminal:hide",
  "terminal:systemNote",
  "transcript:event",
]);

export function isDispatchedInbound(env: unknown): env is DispatchedInboundEnvelope {
  if (!env || typeof env !== "object") return false;
  const t = (env as { type?: unknown }).type;
  return typeof t === "string" && DISPATCHED_TYPES.has(t as DispatchedInboundEnvelope["type"]);
}

// ── Outbound: SPA → host ──────────────────────────────────────────────────

export interface MutateOutboundEnvelope {
  type: "mutate";
  plan: LifePlan;
}

export interface TerminalInputOutboundEnvelope {
  type: "terminal:input";
  data: string;
  submit: boolean;
}

export interface StandupEndOutboundEnvelope {
  type: "standup:end";
}

export type OutboundEnvelope =
  | MutateOutboundEnvelope
  | TerminalInputOutboundEnvelope
  | StandupEndOutboundEnvelope;

// Bare-string commands recognised by the host on the JsonValueKind.String branch.
export type OutboundCommand = "standup" | "init" | "add";

export type OutboundPayload = OutboundEnvelope | OutboundCommand;
