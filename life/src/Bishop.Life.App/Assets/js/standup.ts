// Owns the stand-up modal surface: transcript bubbles, host-side system notes,
// the dedicated input textarea, and the viz panel reveal/hide. Driven by the
// dispatcher (terminal:show/hide, transcript:event, terminal:systemNote) and
// posts user input back to the host as {type:"terminal:input", data}.
//
// Viz panel visibility belongs here but its rendering (drawFlatLine, speak
// playback) stays in main.ts — the caller passes onVizReveal/Hide callbacks to
// initStandup so this module can stay independent of the canvas.

import { postToHost } from "./message-bus.js";

const STANDUP_INPUT_BASE_PX = 36;   // one visible row + border + padding
const STANDUP_INPUT_MAX_PX = 116;   // ~5 rows of 13px @ line-height 1.5 + chrome

let standupTerminalEl: HTMLElement | null = null;
let standupInputEl: HTMLTextAreaElement | null = null;
let transcriptThinkingEl: HTMLElement | null = null;
let lastTranscriptKind: string | null = null;
let onVizReveal: () => void = () => {};
let onVizHide: () => void = () => {};

export interface StandupOptions {
  onVizReveal?: () => void;
  onVizHide?: () => void;
}

export function initStandup(opts: StandupOptions = {}): void {
  standupTerminalEl = document.getElementById("standupTerminal");
  standupInputEl = document.getElementById("standupInput") as HTMLTextAreaElement | null;
  onVizReveal = opts.onVizReveal ?? (() => {});
  onVizHide = opts.onVizHide ?? (() => {});

  if (!standupInputEl) return;

  standupInputEl.addEventListener("focus", resizeStandupInput);
  standupInputEl.addEventListener("blur", resizeStandupInput);
  standupInputEl.addEventListener("input", resizeStandupInput);
  standupInputEl.addEventListener("keydown", ev => {
    if (ev.key !== "Enter" || ev.shiftKey) return; // Shift+Enter inserts a newline
    ev.preventDefault();
    if (!standupInputEl) return;
    const text = standupInputEl.value;
    if (!text) return;
    // Card #1076: the body-then-Enter split with the 50ms inter-write delay
    // (originally added here in #1065) now lives in C# in PtyInputSequencer.
    // JS sends a single envelope with submit:true; the sequencer handles the
    // two writes and the wall-clock gap that Claude's raw-mode TUI requires.
    postToHost({ type: "terminal:input", data: text, submit: true });
    standupInputEl.value = "";
    resizeStandupInput();
  });
}

export function showStandupTerminal(): void {
  if (!standupTerminalEl) return;
  standupTerminalEl.hidden = false;
  document.body.classList.add("standup-session-active");
  setStandupBtnMode(true);
  clearTranscript();
  showThinking();
  showStandupInput();
  // Card #1063: reveal the visualizer with a resting flat line the moment the
  // pane opens, so the first TTS chunk animates in-place instead of popping the
  // panel into existence and shifting layout mid-conversation.
  onVizReveal();
}

export function hideStandupTerminal(): void {
  if (!standupTerminalEl) return;
  standupTerminalEl.hidden = true;
  document.body.classList.remove("standup-session-active");
  setStandupBtnMode(false);
  clearTranscript();
  hideStandupInput();
  onVizHide();
}

// Card #1081: the single topbar button toggles between launching a stand-up
// and ending the live session — keeps the topbar to three slots and matches
// the conversational "initiate ↔ end" pairing.
function setStandupBtnMode(sessionActive: boolean): void {
  const btn = document.getElementById("standupBtn");
  if (!btn) return;
  btn.classList.toggle("danger", sessionActive);
  btn.innerHTML = sessionActive
    ? '<span class="tri">&#9633;</span> End Stand-Up'
    : '<span class="tri">&#9655;</span> Initiate Stand-Up';
}

// Card #1060: the stand-up's first user turn is Claude Code's slash-command
// expansion of `/bish-life-standup` — the full SKILL.md prompt prefixed with
// "Base directory for this skill: …". It's pure agent scaffolding, not
// anything the user wrote, and it dwarfs the rest of the transcript when
// rendered as a bubble. Drop it. The prefix is a Claude Code convention
// for every skill expansion, so the check is robust and specific.
// (The JSON envelope from `bishop context-pack life-standup` arrives as a
// tool_result and is already filtered upstream by the tailer.)
function isContextPackEnvelope(text: string): boolean {
  if (!text) return false;
  return text.trimStart().startsWith("Base directory for this skill:");
}

function clearTranscript(): void {
  if (!standupTerminalEl) return;
  standupTerminalEl.innerHTML = "";
  transcriptThinkingEl = null;
  lastTranscriptKind = null;
}

export function appendTranscriptEvent(kind: string, text: string): void {
  if (!standupTerminalEl) return;
  // Card #1060: drop the context-pack envelope so the first user bubble in
  // a stand-up isn't a wall of JSON.
  if (kind === "user" && isContextPackEnvelope(text)) return;

  const wasAtBottom = isScrolledToBottom(standupTerminalEl);
  removeThinking();

  const entry = document.createElement("div");
  entry.className = "tx-entry";
  if (kind === "user") {
    const bubble = document.createElement("div");
    bubble.className = "tx-user";
    bubble.textContent = text;
    entry.appendChild(bubble);
    standupTerminalEl.appendChild(entry);
    showThinking();
  } else if (kind === "assistant") {
    const body = document.createElement("div");
    body.className = "tx-assistant";
    // Card #1060: drop the `<!-- no-speak -->`/`<!-- /no-speak -->` markers
    // that the SKILL wraps around the agenda — they're TTS hints only and
    // would otherwise survive into the rendered HTML, breaking the
    // line-by-line bullet-list handling in renderMarkdown.
    const stripped = text.replace(/<!--\s*\/?no-speak\s*-->/g, "");
    body.innerHTML = renderMarkdown(stripped);
    entry.appendChild(body);
    standupTerminalEl.appendChild(entry);
  } else if (kind === "tool") {
    const line = document.createElement("div");
    line.className = "tx-tool";
    line.textContent = text;
    entry.appendChild(line);
    standupTerminalEl.appendChild(entry);
    // Tool calls happen during the agent turn — keep showing the thinking
    // indicator until the next assistant text lands.
    showThinking();
  } else {
    return;
  }
  lastTranscriptKind = kind;
  if (wasAtBottom) scrollToBottom(standupTerminalEl);
}

export function appendSystemNote(text: string): void {
  if (!standupTerminalEl || !text) return;
  const wasAtBottom = isScrolledToBottom(standupTerminalEl);
  const entry = document.createElement("div");
  entry.className = "tx-entry tx-system";
  entry.textContent = text;
  standupTerminalEl.appendChild(entry);
  if (wasAtBottom) scrollToBottom(standupTerminalEl);
}

function showThinking(): void {
  if (!standupTerminalEl || transcriptThinkingEl) return;
  const el = document.createElement("div");
  el.className = "tx-thinking";
  el.textContent = "Bishop is thinking…";
  standupTerminalEl.appendChild(el);
  transcriptThinkingEl = el;
}

function removeThinking(): void {
  if (transcriptThinkingEl) {
    transcriptThinkingEl.remove();
    transcriptThinkingEl = null;
  }
}

function isScrolledToBottom(el: HTMLElement): boolean {
  return el.scrollHeight - el.scrollTop - el.clientHeight < 24;
}

function scrollToBottom(el: HTMLElement): void {
  el.scrollTop = el.scrollHeight;
}

/* Minimal markdown → HTML. Covers paragraphs, ATX headings (#–###),
   fenced code blocks, inline code, **bold**, *italic*, bullet/numbered
   lists, and [text](url) links. All output passes through escapeHtml
   first so user content can't inject markup. Deliberately tiny — claude
   prose in stand-ups is short and doesn't need a full CommonMark engine. */
function renderMarkdown(src: string): string {
  if (!src) return "";
  // Pull out fenced code blocks first so their contents bypass inline rules.
  const codeStash: string[] = [];
  let s = String(src).replace(/```([a-z0-9_+-]*)\n([\s\S]*?)```/gi, (_m, _lang, body: string) => {
    const i = codeStash.length;
    codeStash.push(`<pre><code>${escapeHtml(body.replace(/\n$/, ""))}</code></pre>`);
    return ` CODE${i} `;
  });
  s = escapeHtml(s);
  // Inline code (`x`).
  s = s.replace(/`([^`\n]+)`/g, (_m, c: string) => `<code>${c}</code>`);
  // Bold + italic. Bold first so **x** inside *x* doesn't double-wrap.
  s = s.replace(/\*\*([^*\n]+)\*\*/g, "<strong>$1</strong>");
  s = s.replace(/(^|[^*])\*([^*\n]+)\*/g, "$1<em>$2</em>");
  // Links.
  s = s.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');

  // Block transform line-by-line.
  const lines = s.split("\n");
  const out: string[] = [];
  let listKind: "ul" | "ol" | null = null;
  let para: string[] = [];
  const flushPara = (): void => {
    if (para.length) { out.push(`<p>${para.join(" ")}</p>`); para = []; }
  };
  const closeList = (): void => {
    if (listKind) { out.push(`</${listKind}>`); listKind = null; }
  };
  for (const raw of lines) {
    const line = raw.trim();
    if (!line) { flushPara(); closeList(); continue; }
    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      flushPara(); closeList();
      const level = heading[1]!.length;
      out.push(`<h${level}>${heading[2]}</h${level}>`);
      continue;
    }
    const bullet = line.match(/^[-*]\s+(.+)$/);
    if (bullet) {
      flushPara();
      if (listKind !== "ul") { closeList(); out.push("<ul>"); listKind = "ul"; }
      out.push(`<li>${bullet[1]}</li>`);
      continue;
    }
    const numbered = line.match(/^\d+\.\s+(.+)$/);
    if (numbered) {
      flushPara();
      if (listKind !== "ol") { closeList(); out.push("<ol>"); listKind = "ol"; }
      out.push(`<li>${numbered[1]}</li>`);
      continue;
    }
    closeList();
    para.push(line);
  }
  flushPara();
  closeList();

  // Restore code blocks.
  return out.join("").replace(/ CODE(\d+) /g, (_m, i: string) => codeStash[Number(i)] ?? "");
}

function escapeHtml(s: string): string {
  const map: Record<string, string> = { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" };
  return String(s).replace(/[&<>"']/g, c => map[c] ?? c);
}

/* Stand-up input box (card #1057). Routes keystrokes/dictation to the PTY
   through a dedicated textarea so Wispr Flow's keystroke simulation lands
   in a normal input element (no duplication) rather than xterm's helper
   textarea. Single visible row until focused, grows up to 5 rows on focus
   or when content wraps. */
function showStandupInput(): void {
  if (!standupInputEl) return;
  standupInputEl.hidden = false;
  standupInputEl.value = "";
  resizeStandupInput();
  standupInputEl.focus();
}

function hideStandupInput(): void {
  if (!standupInputEl) return;
  standupInputEl.hidden = true;
  standupInputEl.value = "";
  standupInputEl.style.height = STANDUP_INPUT_BASE_PX + "px";
}

function resizeStandupInput(): void {
  if (!standupInputEl) return;
  const focused = document.activeElement === standupInputEl;
  if (!focused && !standupInputEl.value) {
    standupInputEl.style.height = STANDUP_INPUT_BASE_PX + "px";
    return;
  }
  // Measure natural content height, then clamp into [base, max].
  standupInputEl.style.height = "auto";
  const natural = standupInputEl.scrollHeight;
  const target = focused
    ? Math.max(natural, STANDUP_INPUT_MAX_PX)
    : Math.min(natural, STANDUP_INPUT_MAX_PX);
  standupInputEl.style.height = Math.min(target, STANDUP_INPUT_MAX_PX) + "px";
}
