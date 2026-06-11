// Debug-console overlay (card #1086). Owns a hidden xterm.js instance fed
// continuously by `terminal:data` envelopes from session start, so toggling
// the overlay on with Ctrl+Shift+T shows the current TUI screen rather than
// a fresh re-attach. While the overlay is open, keystrokes go straight to
// the PTY via `terminal:raw-input`, bypassing PtyInputSequencer's body/Enter
// split — needed for interactive TUI prompts (login expiry, update dialogs).
//
// Sized to the PTY spawn cols/rows (80x30); no FitAddon. The previous xterm
// path was deleted in card #1075 because a fit-on-reveal nudge was timing-
// sensitive and showed torn screens — this implementation keeps the feed
// continuous instead.

import { postToHost } from "./message-bus.js";

// xterm.js is loaded by a <script> tag in index.html and exposes window.Terminal.
// We avoid pulling @xterm/xterm into the TS toolchain to keep node_modules small.
interface XtermInstance {
  open(el: HTMLElement): void;
  write(data: string): void;
  focus(): void;
  onData(cb: (data: string) => void): void;
}
interface XtermCtor {
  new (opts: {
    cols: number;
    rows: number;
    cursorBlink?: boolean;
    convertEol?: boolean;
    fontFamily?: string;
    fontSize?: number;
    theme?: { background?: string; foreground?: string };
  }): XtermInstance;
}
declare global {
  interface Window { Terminal?: XtermCtor; }
}

// PTY spawn defaults — must match StandupController.DefaultCols/Rows.
const TERMINAL_COLS = 80;
const TERMINAL_ROWS = 30;

let term: XtermInstance | null = null;
let overlayEl: HTMLElement | null = null;
let pendingBuffer = "";
let isOpen = false;

export function initDebugConsole(): void {
  overlayEl = document.getElementById("debugConsole");
  const xtermHost = document.getElementById("debugConsoleXterm");
  if (!overlayEl || !xtermHost) return;
  const Ctor = window.Terminal;
  if (!Ctor) {
    // xterm.js failed to load — leave the overlay disabled. The transcript
    // path still works; only Ctrl+Shift+T becomes a no-op.
    console.warn("debug-console: xterm.js (window.Terminal) not available");
    return;
  }

  term = new Ctor({
    cols: TERMINAL_COLS,
    rows: TERMINAL_ROWS,
    cursorBlink: true,
    convertEol: false,
    fontFamily: '"Cascadia Code", Consolas, monospace',
    fontSize: 13,
    theme: { background: "#000000", foreground: "#e6e6e6" },
  });
  term.open(xtermHost);
  // Drain any bytes that arrived before xterm was ready.
  if (pendingBuffer) {
    term.write(pendingBuffer);
    pendingBuffer = "";
  }
  // Raw-input channel: every keystroke (including control bytes) goes straight
  // to the PTY without sequencing. xterm's onData fires once per logical input
  // event already, so we don't need to coalesce.
  term.onData(data => {
    postToHost({ type: "terminal:raw-input", data });
  });

  document.addEventListener("keydown", onKeydown);
}

// Card #1086: dispatcher routes every `terminal:data` envelope here. Bytes
// stream from session start — even while hidden — so the buffer is always
// current when the overlay is toggled on.
export function feedTerminalData(data: string): void {
  if (!data) return;
  if (term) {
    term.write(data);
  } else {
    // xterm not constructed yet — buffer and flush on init.
    pendingBuffer += data;
  }
}

function onKeydown(ev: KeyboardEvent): void {
  if (!ev.ctrlKey || !ev.shiftKey) return;
  if (ev.key !== "T" && ev.key !== "t") return;
  ev.preventDefault();
  toggle();
}

function toggle(): void {
  if (!overlayEl) return;
  if (isOpen) {
    overlayEl.hidden = true;
    isOpen = false;
    // Return focus to the transcript textarea so typing resumes the normal
    // sequenced-input path without an extra click.
    const input = document.getElementById("standupInput") as HTMLTextAreaElement | null;
    input?.focus();
  } else {
    overlayEl.hidden = false;
    isOpen = true;
    term?.focus();
  }
}
