# bishop.life — Design Spec

Status: post-grill, source-of-truth for the initial build. This is the only `bishop.life`-related design doc in the repo — implementation cards (#1003–#1008) reference this file rather than restating decisions.

bishop.life is a sister product to bishop.dev (the kanban-for-coding tool this repo ships today). Same chassis, different surface: bishop.dev tracks code work, bishop.life tracks the rest of life. The two stay decoupled at the project level so the shared chassis can be lifted into a future `Bishop.Shared` without a refactor.

## 1. Purpose & domain

A daily reflection tool — short, fixed, same every day — backed by a single JSON file that captures the user's current picture of life as a small set of **areas**, each containing **goals**, each containing **actions**.

Default seed areas (created on first init, freely renameable thereafter):

- Finances
- Side projects
- Career
- Home
- Health
- Relationships

Design tenets, in priority order:

1. **No shame.** The tool never scores, streaks, or guilts. A missed day is silent.
2. **Externalised memory.** The user should be able to forget everything between stand-ups; the file is the brain.
3. **Predictable ritual.** The stand-up is the same shape every day — context pack, brain-dump, per-item walk, reflection, ≤3 focus items. The walk is what turns a vent into a board update; without it the ritual collapses into data entry.
4. **At most three starred actions** at any time. Forcing scarcity is the feature.

## 2. The daily stand-up

The headline interaction. One ritual, run from Claude Code via the `/bish-life-standup` skill (card #1004):

1. Skill reads `bishop.life.json` and assembles a context pack: time since last stand-up, count of open actions, currently-starred actions (with `area ▸ goal` lineage), areas with no activity recently, inbox count.
2. Single open brain-dump prompt to the user (collected in one go, no follow-ups in this step).
3. Per-item walk: the skill identifies distinct threads in the dump and questions each one in turn — does it belong on the board, is it one action or several, is it done or blocked or actionable, which area/goal, and so on. "Drop / don't record" is an explicit option, not an assumption. Autopilot things stay off the board. One thread per message. Ends with "anything I missed?".
4. After the walk, the skill produces a reflection block, picks 1–3 focus items for today, and shows the proposed mutations for confirmation before writing.
5. Pre-write: copy current file to `bishop.life.json.prev` (single-step undo).
6. Atomic write: temp file + rename, so a kill mid-write can never leave a partial file.
7. Append the new stand-up to `standups[]`, trim to the last 10 inline.
8. Any items in `inbox` are triaged into the area/goal tree as part of the same write (resolution happens during the walk; the write just persists it).

The stand-up is also the only path that adds or deletes areas/goals/actions. Inline UI interactions (§7) cover edits, stars, and check-offs only.

### Why whole-file rewrite over an op grammar

Claude reads and writes JSON natively. An op-applier would be extra machinery for no concurrency benefit — atomic write plus `.prev` already covers every failure mode.

## 3. Project structure

```
src/
  Bishop.Life.Core/      net9.0 class library — schema, file I/O, watcher
  Bishop.Life.App/       WinUI 3 unpackaged — single-window shell
tests/
  Bishop.Life.Tests/     xUnit + FluentAssertions
skills/
  bish-life-init/        bootstraps the seed JSON
  bish-life-standup/     the daily ritual
```

**Hard rule:** `Bishop.Life.*` projects do **not** reference `Bishop.App` or `Bishop.Core`, and vice versa. The two product surfaces share a chassis (MVVM, hosting, terminal launch) only by future extraction into `Bishop.Shared`. Until that lift, small duplication (e.g. porting a single `TerminalLauncher` method) is preferred over a cross-product reference.

Bishop.dev cards that touch the Life work use existing tags (`feature`, `docs`, etc.) — no new `life` tag is introduced.

## 4. Data file & paths

- **Canonical path:** `%APPDATA%/Bishop/life/bishop.life.json`
- **Override (tests, portable configs):** environment variable, absolute path
- **Pre-state snapshot:** `bishop.life.json.prev` (one level of undo, overwritten each stand-up)
- **Atomic write target:** `bishop.life.json.tmp` (renamed to final on success)

The file is **not** git-tracked by the product. Users may version it manually if they choose — `JsonSerializerOptions` preserves property order and indentation so diffs read cleanly when they do.

## 5. Concurrency model

The WinUI shell and the Claude Code skill both write the same file. The model is:

- **One writer at a time.** The shell disables mutations and shows a transient banner while a stand-up is mid-write (in-flight flag / file-lock).
- **Atomic writes only.** Both surfaces go through `Bishop.Life.Core.LifePlanFileService`, which writes to `.tmp` and renames.
- **Filesystem-watched refresh.** `LifePlanWatcher` debounces `FileSystemWatcher` events and raises `Reloaded`; the shell reposts state to its WebView2 view on each tick. No restart required after a stand-up.

## 6. Schema (canonical)

The **nested** shape below is canonical. The prototype's flat shape is rejected.

```jsonc
{
  "schema": "bishop.life/v1",
  "meta": {
    "createdAt": "2026-06-08T08:00:00Z",
    "lastStandupAt": "2026-06-08T08:30:00Z"
  },
  "areas": [
    {
      "id": "area-finances",
      "name": "Finances",
      "color": "#a8b3c4",
      "goals": [
        {
          "id": "goal-emergency-fund",
          "name": "Build 6-month emergency fund",
          "horizon": "2026-12",
          "actions": [
            {
              "id": "act-…",
              "title": "Move £500 to savings",
              "starred": true,
              "done": false,
              "createdAt": "…",
              "completedAt": null
            }
          ]
        }
      ]
    }
  ],
  "inbox": [
    { "id": "ibx-…", "text": "Look into ISA limits", "capturedAt": "…" }
  ],
  "standups": [
    {
      "id": "su-…",
      "at": "2026-06-08T08:30:00Z",
      "reflection": "…",
      "focusToday": ["act-…", "act-…"]
    }
  ]
}
```

Notable shapes:

- **`horizon` is per-goal**, not per-area or per-action. Format is a coarse month string (`YYYY-MM`) or `null` for "no horizon".
- **Inbox is a flat list** of strings + capture time. Triage happens during a stand-up, never in the inline UI.
- **`standups[]` is capped at 10** entries inline. Older entries are dropped on write (no archive — the *file* is the memory, not the history of files).
- **Starred actions** are surfaced explicitly in the context pack; the stand-up enforces the "≤3 starred" ceiling.

## 7. UI surface

### Phase 0 scope (delivered with the initial build)

A minimal WinUI 3 shell — **not** deferred:

- Single window hosting a WebView2.
- Embeds the prototype's HTML/CSS verbatim, minus `localStorage` and the direct Anthropic API call. The visual design is already done; rebuilding it in XAML is not where time should go.
- Four-tab navigation kept from the prototype.
- **"Initiate Stand-Up" button** — the accent-coloured CTA. Launches Windows Terminal running `claude` with `/bish-life-standup` ready to invoke, using the same `wt.exe` + `cmd /k claude` pattern bishop.dev's `TerminalLauncher` already uses (copied, not referenced — see §3).
- **Inline interactions:** star toggle, check-off, inline title edit. Each mutation builds the new whole-file state in JS, posts it to the .NET host, which writes `.prev` and then atomic-saves through `Bishop.Life.Core`.
- File-watch driven refresh: external writes (stand-ups, hand edits) cause the shell to repost state to the view without a restart.

### Out of scope for Phase 0 (and the inline UI generally)

- Add/delete of areas, goals, or actions — handled only by stand-up or hand-edit.
- Horizon changes — stand-up only.
- Inbox triage — stand-up only.
- Any login, sync, or remote surface.

### Palette

Areas use a muted palette assigned at init from a fixed six-colour list (one per default area). Users can rename areas freely; colours stay sticky to the area `id`. The accent colour for the stand-up CTA matches bishop.dev's accent.

## 8. Claude Code session surface

The stand-up is a Claude Code session, not an in-app chat. The shell launches a terminal with `claude` already running; the user invokes `/bish-life-standup`; the skill drives the ritual; the file changes; the shell's file-watch picks up the new state.

This keeps bishop.life's chassis tiny: it is a viewer + a launcher, with all language-model logic owned by the skill running in the user's Claude Code session.

## 9. Implementation plan

Cards on the bishop.dev board (in suggested order):

1. **#1003** — `bish-life-init` skill (seed JSON, refuse to overwrite).
2. **#1005** — `Bishop.Life.Core` + `Bishop.Life.Tests` (schema POCOs, file service, watcher).
3. **#1004** — `bish-life-standup` skill (the daily ritual, whole-file rewrite, `.prev`, trim to 10).
4. **#1006** — `Bishop.Life.App` viewer-only WinUI shell (WebView2 hosts adapted prototype, no edit handlers yet).
5. **#1007** — Initiate Stand-Up button (terminal launch).
6. **#1008** — Inline interactions (star, check-off, title edit).

Each card's `### Decided` section records the option chosen over its alternative; this doc summarises but does not duplicate them.
