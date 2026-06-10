// Speak viz — CRT-phosphor oscilloscope trace driven by raw 8 kHz mono int16
// PCM (base64) forwarded from Bishop.Cli's Piper TTS via the bishop-life-speak
// named pipe. The centre column maps to the 150 ms look-ahead boundary and the
// edges map to current playback time, so peaks emerge at centre and slide
// outward. The left half is the horizontal mirror of the right so the trace
// stays symmetric about the vertical centre. PCM sign drives vertical
// deflection. Persistence trail (destination-out translucent fill) + additive
// glow underlay give the trace its CRT feel. No playhead — the trace is the cue.
//
// Visibility is owned by the stand-up module (revealResting on pane open,
// hide on close); between TTS chunks the panel shows a flat accent line so it
// never goes black.

const VIZ_WINDOW_MS = 300; // total window — 150 ms each side of "now"

interface VizSession {
  pcm: Int16Array;
  sampleRateHz: number;
  durationMs: number;
  startedAt: number;
  raf: number;
  decayStartedAt: number;
}

let vizPanel: HTMLElement | null = null;
let vizCanvas: HTMLCanvasElement | null = null;
let vizSession: VizSession | null = null;

export function initSpeakViz(panelEl: HTMLElement, canvasEl: HTMLCanvasElement): void {
  vizPanel = panelEl;
  vizCanvas = canvasEl;
  window.addEventListener("resize", () => {
    if (vizSession) resizeSpeakViz();
    else if (vizPanel && !vizPanel.hidden) drawFlatLine();
  });
}

// Reveal the panel with a resting flat line (card #1063) — first TTS chunk
// then animates in-place instead of popping the panel into existence mid-turn.
export function revealResting(): void {
  if (!vizPanel) return;
  vizPanel.hidden = false;
  drawFlatLine();
}

export function hideSpeakViz(): void {
  cancelSpeakViz();
  if (vizPanel) vizPanel.hidden = true;
}

export function startSpeakViz(pcmBase64: string, pcmSampleRateHz: number, durationMs: number): void {
  if (!vizCanvas) return;
  cancelSpeakViz();
  vizSession = {
    pcm: decodePcmBase64(pcmBase64 || ""),
    sampleRateHz: pcmSampleRateHz || 8000,
    durationMs: durationMs || 0,
    startedAt: performance.now(),
    raf: 0,
    decayStartedAt: 0,
  };
  resizeSpeakViz();
  // Wipe any leftover trail from the previous utterance so the new trace
  // doesn't ghost atop stale phosphor.
  const ctx = vizCanvas.getContext("2d");
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, vizCanvas.clientWidth, vizCanvas.clientHeight);
  drawSpeakViz();
}

export function stopSpeakViz(): void {
  cancelSpeakViz();
  drawFlatLine();
}

function cancelSpeakViz(): void {
  if (vizSession && vizSession.raf) cancelAnimationFrame(vizSession.raf);
  vizSession = null;
}

// Decode base64 → Int16Array (little-endian). Returns an empty array when the
// input is empty or malformed so the rest of the viz can stay branchless.
function decodePcmBase64(b64: string): Int16Array {
  if (!b64) return new Int16Array(0);
  try {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    // Copy into a fresh buffer so the Int16 view is guaranteed aligned.
    const buf = new ArrayBuffer(bytes.length);
    new Uint8Array(buf).set(bytes);
    return new Int16Array(buf);
  } catch (_) {
    return new Int16Array(0);
  }
}

function resizeSpeakViz(): void {
  if (!vizCanvas) return;
  const dpr = window.devicePixelRatio || 1;
  const w = vizCanvas.clientWidth;
  const h = vizCanvas.clientHeight;
  vizCanvas.width = Math.max(1, Math.round(w * dpr));
  vizCanvas.height = Math.max(1, Math.round(h * dpr));
}

function accentColor(): string {
  return getComputedStyle(document.documentElement).getPropertyValue("--accent").trim() || "#00FF41";
}

// Parse `--accent` to {r,g,b} so we can build phosphor-trail rgba() strings
// without baking the hex into the JS.
function accentRgb(): { r: number; g: number; b: number } {
  const hex = accentColor().replace("#", "");
  if (hex.length !== 6) return { r: 0, g: 255, b: 65 };
  return {
    r: parseInt(hex.slice(0, 2), 16),
    g: parseInt(hex.slice(2, 4), 16),
    b: parseInt(hex.slice(4, 6), 16),
  };
}

function drawFlatLine(): void {
  if (!vizPanel || !vizCanvas || vizPanel.hidden) return;
  resizeSpeakViz();
  const ctx = vizCanvas.getContext("2d");
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const w = vizCanvas.clientWidth;
  const h = vizCanvas.clientHeight;
  ctx.clearRect(0, 0, w, h);
  ctx.fillStyle = accentColor();
  const y = Math.round(h / 2) - 1;
  ctx.fillRect(0, y, w, 2);
}

function drawSpeakViz(): void {
  if (!vizSession || !vizCanvas) return;
  const ctx = vizCanvas.getContext("2d");
  if (!ctx) return;
  const dpr = window.devicePixelRatio || 1;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const w = vizCanvas.clientWidth;
  const h = vizCanvas.clientHeight;
  const mid = h / 2;

  // CRT phosphor persistence: translucent clear (~100 ms full fade at 60 fps)
  // via destination-out so the previous frame ghosts under the new trace.
  // destination-out keeps the effect theme-agnostic — pixels fade to fully
  // transparent rather than to black.
  ctx.globalCompositeOperation = "destination-out";
  ctx.fillStyle = "rgba(0,0,0,0.18)";
  ctx.fillRect(0, 0, w, h);
  ctx.globalCompositeOperation = "source-over";

  const pcm = vizSession.pcm;
  const n = pcm.length;
  const rate = vizSession.sampleRateHz;
  const totalMs = vizSession.durationMs > 0
    ? vizSession.durationMs
    : (rate > 0 ? (n / rate) * 1000 : 0);
  const elapsedMs = performance.now() - vizSession.startedAt;
  const nowMs = Math.min(elapsedMs, totalMs > 0 ? totalMs : elapsedMs);

  const halfMs = VIZ_WINDOW_MS / 2;
  const halfW = w / 2;

  // End-of-utterance ramp: linearly fade to silence over the final 100 ms so
  // the trace doesn't snap to flat when audio ends.
  const remainingMs = totalMs > 0 ? totalMs - nowMs : Infinity;
  const eouGain = Math.max(0, Math.min(1, remainingMs / 100));

  const accent = accentColor();
  const { r, g, b } = accentRgb();

  if (n > 0 && rate > 0) {
    // First pass: additive glow underlay (wider, semi-transparent).
    ctx.globalCompositeOperation = "lighter";
    ctx.strokeStyle = `rgba(${r},${g},${b},0.35)`;
    ctx.lineWidth = 3;
    drawTracePath(ctx, pcm, rate, nowMs, halfMs, halfW, mid, h, eouGain);

    // Second pass: sharp accent stroke on top.
    ctx.globalCompositeOperation = "source-over";
    ctx.strokeStyle = accent;
    ctx.lineWidth = 1.25;
    drawTracePath(ctx, pcm, rate, nowMs, halfMs, halfW, mid, h, eouGain);
  } else {
    // No waveform — keep the flat accent line so the panel never goes black.
    ctx.fillStyle = accent;
    const y = Math.round(mid) - 1;
    ctx.fillRect(0, y, w, 2);
  }

  // Continue while playback is live, then for ~200 ms after so the trail
  // visibly decays back to flat before we stop the loop.
  if (elapsedMs < (totalMs > 0 ? totalMs : 0) + 200) {
    vizSession.raf = requestAnimationFrame(drawSpeakViz);
  } else {
    cancelSpeakViz();
    drawFlatLine();
  }
}

// Build the right half as a polyline (one Y per column), then stroke it twice —
// once normally on the right, once mirrored across the vertical centre to the
// left — so the trace is symmetric about the centre line. Column index i = 0
// (centre) maps to the 150 ms look-ahead sample; i = cols (right edge) maps to
// "now". Peaks appear at centre and slide outward as playback advances.
function drawTracePath(
  ctx: CanvasRenderingContext2D,
  pcm: Int16Array,
  rate: number,
  nowMs: number,
  halfMs: number,
  halfW: number,
  mid: number,
  h: number,
  eouGain: number,
): void {
  const sampleAt = (ms: number): number => {
    if (ms < 0) return 0;
    const idx = Math.floor((ms / 1000) * rate);
    if (idx < 0 || idx >= pcm.length) return 0;
    return (pcm[idx] ?? 0) / 32768; // → -1..1
  };

  const amp = h * 0.45;
  const cols = Math.max(2, Math.floor(halfW));
  const ys = new Float32Array(cols + 1);
  for (let i = 0; i <= cols; i++) {
    const t = i / cols; // 0 at centre, 1 at right edge
    // Hann window: 1 at centre (t=0), 0 at edge (t=1). Peaks concentrate at
    // the centre and decay smoothly to zero at the L/R canvas edges.
    const fade = 0.5 * (1 + Math.cos(Math.PI * t));
    const v = sampleAt(nowMs + (1 - t) * halfMs) * fade * eouGain;
    ys[i] = mid - v * amp;
  }

  // Right half: centre → right edge.
  ctx.beginPath();
  ctx.moveTo(halfW, ys[0] ?? mid);
  for (let i = 1; i <= cols; i++) ctx.lineTo(halfW + i, ys[i] ?? mid);
  ctx.stroke();

  // Left half: horizontal mirror of the right.
  ctx.beginPath();
  ctx.moveTo(halfW, ys[0] ?? mid);
  for (let i = 1; i <= cols; i++) ctx.lineTo(halfW - i, ys[i] ?? mid);
  ctx.stroke();
}
