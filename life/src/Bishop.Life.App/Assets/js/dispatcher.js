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
      // terminal:data still flows from the host as a dormant fallback for the
      // legacy xterm path; the transcript:event stream is the live channel
      // (card #1059), so we no longer render raw PTY bytes here.
      return;
    }
    if (env && env.type === "transcript:event") {
      appendTranscriptEvent(env.kind, env.text || "");
      return;
    }
    if (env && env.type === "terminal:systemNote") {
      // Card #1065: host-side diagnostic note (dropped input, session-end).
      appendSystemNote(env.text || "");
      return;
    }
    applyEnvelope(env);
  });
}
