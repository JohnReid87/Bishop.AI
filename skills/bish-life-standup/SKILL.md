---
name: bish-life-standup
description: The daily bishop.life stand-up ritual. Reads `bishop.life.json`, surfaces a context pack (time since last stand-up, open action count, starred actions with `area ▸ goal` lineage, untended areas, inbox count), runs one brain-dump prompt, then rewrites the whole file in a single atomic pass — `.prev` backup, `.tmp` + rename, append to `standups[]` and trim to the last 10, triage `inbox[]` into areas/goals as part of the same write, enforce ≤3 starred actions. Use when the user wants to do their stand-up, or invokes `/bish-life-standup`.
allowed-tools: Read, Write, PowerShell, Bash
bishop.category: setup
---

> Exempt from `bishop context-pack` (per `docs/SKILL_FAMILY.md` §4): this skill operates on bishop.life's data file, not on a Bishop workspace. No workspace context is relevant.

Shell tool selection (Bash vs PowerShell) — this skill targets a Windows-only path (`%APPDATA%`). Use `PowerShell` throughout.

Design tenets carried from `docs/bishop-life-spec.md` §1 — observe them in tone and structure:

1. **No shame.** Never score, streak, or guilt. A missed day is silent — do not comment on gaps in `lastStandupAt`, do not chide.
2. **Externalised memory.** The file is the brain — assume the user has forgotten everything since the last stand-up. The context pack does the remembering for them.
3. **Predictable ritual.** Same shape every day: context pack → brain-dump → reflection → ≤3 focus items → whole-file write.
4. **≤3 starred actions** at any time, total across all areas/goals. Forcing scarcity is the feature.

<what-to-do>

### Step 1 — Resolve the canonical path

- If `$env:BISHOP_LIFE_FILE` is set and non-empty, use that absolute path verbatim.
- Otherwise, the canonical path is `Join-Path $env:APPDATA 'Bishop\life\bishop.life.json'`.

Capture the resolved path as `$path`.

### Step 2 — Refuse-if-missing check

Run `Test-Path -LiteralPath $path`.

- If it returns `False` → print exactly:

  > `bishop.life is not initialised at <path>. Run /bish-life-init first.`

  Then STOP. Do not create the file, do not prompt.

### Step 3 — Read and parse

Use the `Read` tool on `$path`. Parse the JSON. The schema is `bishop.life/v1` per `docs/bishop-life-spec.md` §6:

- `schema` — must equal `"bishop.life/v1"`.
- `meta.createdAt`, `meta.lastStandupAt` — ISO 8601 UTC strings (`lastStandupAt` may be `null` on first stand-up).
- `areas[]` — `{ id, name, color, goals[] }`. Each goal: `{ id, name, horizon, actions[] }`. Each action: `{ id, title, starred, done, createdAt, completedAt }`.
- `inbox[]` — `{ id, text, capturedAt }`.
- `standups[]` — `{ id, at, reflection, focusToday[] }`. Capped at 10 entries; oldest dropped on write.

If `schema` is anything other than `"bishop.life/v1"`, STOP and surface:

> `bishop.life schema mismatch — expected "bishop.life/v1", got "<actual>". Refusing to proceed.`

### Step 4 — Assemble the context pack

From the parsed file, compute:

- **Time since last stand-up** — `meta.lastStandupAt` → human phrase (e.g. "yesterday", "3 days ago", "first stand-up"). No shame on long gaps; just state the fact.
- **Open action count** — total actions where `done` is `false`, across all areas/goals.
- **Starred actions** — every action where `starred` is `true`, with full `area ▸ goal ▸ action title` lineage. Note if the count is already at or above 3.
- **Untended areas** — areas with zero open (non-`done`) actions, OR areas whose most-recently-created action predates the last stand-up by a long stretch. Surface by name; don't editorialise.
- **Inbox count** — `inbox.Count`, with the raw text of each item if non-empty (triage happens later in this same pass).

Display the pack to the user verbatim — a calm, scannable block. No advice yet. Example shape:

```
**Last stand-up:** 2 days ago (2026-06-06)
**Open actions:** 7
**Starred (2/3):**
  • Finances ▸ Build 6-month emergency fund ▸ Move £500 to savings
  • Health ▸ Sleep before midnight ▸ Phone out of bedroom
**Untended areas:** Career, Relationships
**Inbox (2):**
  • Look into ISA limits
  • Book dentist
```

### Step 5 — Brain-dump prompt

Ask exactly one open prompt. Phrasing along the lines of:

> What's on your mind? Anything from the last few days — wins, worries, things you've been putting off, things you want to start. Dump it all; I'll sort it.

Read the user's response. Do not interrupt with follow-up questions — the brain-dump is single-pass by design (spec §2).

### Step 6 — Compose the new file in memory

From the brain-dump plus the existing state, build a single new whole-file JSON object. Decisions you make here:

- **Reflection** — 2–6 sentences synthesising the brain-dump. Honest, non-judgemental. Reference specific items the user mentioned. Do not invent feelings the user did not express.
- **Focus today** — pick 1–3 actions to focus on today. Prefer actions the user explicitly mentioned. The selected action ids go into the new stand-up's `focusToday[]`.
- **Starred state** — adjust `starred` on existing actions to reflect what matters this week, not just today. Hard ceiling: total starred across the whole file MUST be ≤ 3 after this write. If the user's brain-dump implies a new priority that would exceed 3, unstar the least-relevant existing one.
- **Inbox triage** — every item in `inbox[]` must either be (a) promoted into an existing or new `area ▸ goal ▸ action`, or (b) explicitly dropped because the user implied it no longer matters. After this step, `inbox[]` is empty.
- **Adds/edits/removes of areas, goals, actions** — the stand-up is the only path that mutates the tree (spec §2 / §7). Apply whatever the brain-dump implies: new actions, completed actions (`done: true`, `completedAt` set to now), renamed goals, new goals under existing areas, etc. Adding entire new *areas* is rare but allowed — only if the user explicitly asked. Do not add areas speculatively.
- **Ids** — for any new entity, mint an id of the form `<prefix>-<short-slug-or-random>`: `act-…` for actions, `goal-…` for goals, `area-…` for areas, `ibx-…` for inbox items (none created here, but the prefix is reserved), `su-…` for the new stand-up entry. Stable existing ids are never renamed.
- **Timestamps** — capture `$now` as `Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC` (or equivalent). Set `meta.lastStandupAt = $now`. Set `completedAt = $now` on any action you just marked done. Set `createdAt = $now` on any new action.
- **New stand-up entry** — append `{ id: "su-…", at: $now, reflection: "<reflection>", focusToday: ["act-…", …] }` to `standups[]`.
- **Trim** — after the append, if `standups.Count > 10`, drop the oldest entries until exactly 10 remain.
- **Colours, schema, area order** — leave untouched.

Preserve property order to match the seed shape (`schema`, `meta`, `areas`, `inbox`, `standups`) and indent with two spaces. This keeps hand-edit diffs readable (spec §4).

### Step 7 — Show the proposed change and confirm

Before any write, show the user:

- The drafted **reflection** (the prose, verbatim).
- The drafted **focusToday** as `area ▸ goal ▸ action title` lines.
- A short bullet list of **tree mutations** you intend to apply: actions added, actions marked done, stars added/removed, inbox items triaged or dropped, anything else.

Then ask:

> Write this stand-up? (`y` to commit / `n` to abandon — nothing has been written yet)

- On `n` → STOP. The file on disk is unchanged. Do not write `.prev`, do not write `.tmp`.
- On `y` → proceed to Step 8.

### Step 8 — Atomic write

In this exact order (each step gated on the previous succeeding):

1. **Snapshot the pre-state** to `.prev` (single-step undo, spec §4):

   ```
   Copy-Item -LiteralPath $path -Destination "$path.prev" -Force
   ```

2. **Write the new whole-file JSON** to a `.tmp` sibling using the `Write` tool:

   - Target path: `"$path.tmp"`.
   - Content: the JSON object composed in Step 6, two-space indented, UTF-8 without BOM (the `Write` tool's default).

3. **Rename** `.tmp` over the live file:

   ```
   Move-Item -LiteralPath "$path.tmp" -Destination $path -Force
   ```

   The rename is the commit point — a kill mid-write can never leave a partial `bishop.life.json` (spec §5).

If any step fails, surface the error and STOP. Do not attempt to roll back from `.prev` automatically — the user does that by hand if they need to (it's why `.prev` exists).

### Step 9 — Confirm

Print a single short line:

> `Stand-up saved. Backup at <path>.prev — delete or restore by hand if you need to undo.`

Do not editorialise further. The ritual is done.

</what-to-do>

<guardrails>

- Do NOT skip the `.prev` snapshot. The single-step undo is a hard contract from spec §4.
- Do NOT write directly to `$path`. Always write `.tmp` and rename — partial writes are how this file gets corrupted (spec §5).
- Do NOT exceed 3 starred actions across the whole file after a stand-up. If the brain-dump implies a new starred item, unstar an existing one. The ceiling is the point (spec §1, §6).
- Do NOT leave items in `inbox[]` after the write. Every item is triaged into the tree or explicitly dropped. The inline UI never triages — the stand-up is the only path (spec §7).
- Do NOT exceed 10 entries in `standups[]`. Trim oldest after appending. The file is the memory, not the history of files (spec §6).
- Do NOT add or rename **areas** speculatively — only when the user explicitly asks. Adding goals and actions inside existing areas is fine and expected.
- Do NOT change `schema`, area `id`s, area `color`s, or `meta.createdAt`. These are stable across stand-ups.
- Do NOT score, streak, or guilt. No "you skipped 4 days" framing. State `lastStandupAt` as a fact and move on (tenet 1).
- Do NOT prompt for more than the single brain-dump. A multi-question interrogation breaks the predictable-ritual tenet (spec §2, tenet 3).
- Do NOT call `bishop` CLI. This skill has no Bishop workspace dependency.

</guardrails>
