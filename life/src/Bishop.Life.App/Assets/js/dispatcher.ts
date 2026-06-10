// Routes inbound envelopes from the host. Speak + terminal-lifecycle envelopes
// and transcript events go to the stand-up module; everything else is a
// whole-plan envelope handled by the inline applyEnvelope callback passed in
// at install time.

import { onHostMessage } from "./message-bus.js";
import {
  showStandupTerminal,
  hideStandupTerminal,
  appendTranscriptEvent,
  appendSystemNote,
} from "./standup.js";
import {
  isDispatchedInbound,
  type DispatchedInboundEnvelope,
  type InboundEnvelope,
  type PlanStateEnvelope,
} from "./envelopes.js";

export interface DispatcherCallbacks {
  applyEnvelope: (env: PlanStateEnvelope) => void;
  onSpeakStarted: (pcmBase64: string, pcmSampleRateHz: number, durationMs: number) => void;
  onSpeakStopped: () => void;
}

export function installDispatcher({ applyEnvelope, onSpeakStarted, onSpeakStopped }: DispatcherCallbacks): void {
  onHostMessage((env: InboundEnvelope) => {
    if (isDispatchedInbound(env)) {
      dispatch(env, { applyEnvelope, onSpeakStarted, onSpeakStopped });
      return;
    }
    applyEnvelope(env);
  });
}

function dispatch(env: DispatchedInboundEnvelope, cb: DispatcherCallbacks): void {
  switch (env.type) {
    case "speak.started":
      cb.onSpeakStarted(env.pcmBase64 ?? "", env.pcmSampleRateHz ?? 8000, env.durationMs ?? 0);
      return;
    case "speak.stopped":
      cb.onSpeakStopped();
      return;
    case "terminal:show":
      showStandupTerminal();
      return;
    case "terminal:hide":
      hideStandupTerminal();
      return;
    case "terminal:systemNote":
      appendSystemNote(env.text ?? "");
      return;
    case "transcript:event":
      appendTranscriptEvent(env.kind, env.text ?? "");
      return;
    default: {
      // Exhaustiveness check — adding a new DispatchedInboundEnvelope variant
      // without a case above becomes a compile error.
      const _exhaustive: never = env;
      throw new Error(`Unhandled inbound envelope: ${JSON.stringify(_exhaustive)}`);
    }
  }
}
