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

export function installDispatcher({ applyEnvelope, onSpeakStarted, onSpeakStopped }) {
  onHostMessage(env => {
    if (env && typeof env.type === "string" && env.type.startsWith("speak.")) {
      if (env.type === "speak.started") onSpeakStarted(env.pcmBase64 || "", env.pcmSampleRateHz || 8000, env.durationMs || 0);
      else if (env.type === "speak.stopped") onSpeakStopped();
      return;
    }
    if (env && typeof env.type === "string" && env.type.startsWith("terminal:")) {
      if (env.type === "terminal:show") showStandupTerminal();
      else if (env.type === "terminal:hide") hideStandupTerminal();
      else if (env.type === "terminal:systemNote") appendSystemNote(env.text || "");
      return;
    }
    if (env && env.type === "transcript:event") {
      appendTranscriptEvent(env.kind, env.text || "");
      return;
    }
    applyEnvelope(env);
  });
}
