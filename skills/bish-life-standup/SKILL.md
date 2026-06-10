---
name: bish-life-standup
description: The daily bishop.life stand-up ritual. Reads `bishop.life.json`, surfaces a context pack (time since last stand-up, open action count, starred actions with `area ▸ goal` lineage, untended areas, inbox count), opens by resuming the last live thread (or a neutral grounded open when nothing carried over) which doubles as the brain-dump, then walks each raised thread interactively (is it actionable, is it done, does it even belong on the board, which area/goal), then rewrites the whole file in a single atomic pass — `.prev` backup, `.tmp` + rename, append to `standups[]` and trim to the last 10, triage `inbox[]` into areas/goals as part of the same write, enforce ≤3 starred actions. Use when the user wants to do their stand-up, or invokes `/bish-life-standup`.
allowed-tools: Read, Write, PowerShell, Bash
bishop.category: setup
---

> Uses `bishop context-pack life-standup` for the surfaced context (per `docs/SKILL_FAMILY.md` §4). Workspace context is not relevant — this skill operates on bishop.life's data file, not a Bishop workspace.

> **TTS hook is user-scoped.** The `Stop` → `bishop hook speak-on-stop` hook lives in `~/.claude/settings.json` (not the Bishop.AI repo's project settings), so it follows the user across any working directory. `BishLifeTranscriptScanner` gates it on `<command-name>/bish-life-standup</command-name>` (or `/bish-life-add`) in the transcript, so it stays silent for every other skill.

Shell tool selection (Bash vs PowerShell) — this skill targets a Windows-only path (`%APPDATA%`). Use `PowerShell` throughout.

**TTS hook discipline.** The `Stop` → `bishop hook speak-on-stop` hook speaks the last assistant message minus `<!-- no-speak -->…<!-- /no-speak -->` blocks (see `BishLifeTranscriptScanner.StripForSpeech`). The user dictates while doing other things — what should be spoken is:

- The single direct question to the user (the opening / resume question, the per-thread question, the "Write this stand-up?" confirm),
- The drafted reflection prose (it's the synthesis the user benefits from hearing),
- Short conversational acknowledgements.

Wrap everything else in `<!-- no-speak -->…<!-- /no-speak -->`: the Step 2 context block (already wrapped below), option enumerations (`(y / n / …)`), focus-list and mutations bullets, section headings, file paths, JSON keys, schema strings, and the `area ▸ goal ▸ action` lineage triplets used during the walk. The prompt templates below already show this — preserve the wrapping when you emit them.

Design tenets carried from `docs/bishop-life-spec.md` §1 — observe them in tone and structure:

1. **No shame.** Never score, streak, or guilt. A missed day is silent — do not comment on gaps in `lastStandupAt`, do not chide.
2. **Externalised memory.** The file is the brain — assume the user has forgotten everything since the last stand-up. The context pack does the remembering for them.
3. **Predictable ritual.** Same shape every day: context pack → brain-dump → per-item walk → drafted reflection + ≤3 focus items → confirm → whole-file write. The walk is what turns a vent into a board update — without it, the skill is just data entry.
4. **≤3 starred actions** at any time, total across all areas/goals. Forcing scarcity is the feature.
5. **Resume, don't greet.** The stand-up opens by picking up the live thread from last time, not with a fixed greeting. The opening line *is* the brain-dump invitation, voiced as the question a person who remembered would ask. Predictability lives in the shape (tenet 3), not in the wording — vary the content, hold the register.

<what-to-do>

### Step 1 — Load the context pack via the CLI

Run:

```
bishop context-pack life-standup
```

Parse the JSON from stdout. The shape is:

- `filePath` — the resolved path to `bishop.life.json` (respects `$env:BISHOP_LIFE_FILE`).
- `exists` — `false` if the file is missing. If so → print exactly:

  > bishop.life is not initialised. <!-- no-speak -->Path: `<filePath>`. Run `/bish-life-init` first.<!-- /no-speak -->

  Then STOP.
- `schemaOk` — `false` if the file's `schema` field isn't `"bishop.life/v1"`. If so → STOP and surface:

  > bishop.life schema mismatch. Refusing to proceed. <!-- no-speak -->Expected `"bishop.life/v1"`, got `"<schema>"`.<!-- /no-speak -->

- `lastStandupAt`, `lastStandupPhrase` — last stand-up timestamp + human phrase ("yesterday", "3 days ago", "first stand-up"). No shame on long gaps; just state the fact.
- `openActionCount` — total actions where `done` is `false`, across all areas/goals.
- `starred[]` — `{ actionId, area, goal, title, horizon }` for each non-done starred action. `starredCeiling` is `3`; note when at/above it.
- `untendedAreas[]` — area names with zero open actions.
- `inbox[]` — `{ id, text, capturedAt }`. Triage happens later in this same pass.
- `calendar[]` — `{ id, summary, start, end, allDay, status }` for primary-calendar events in the next 14 days. Hidden from the Step 2 summary; surfaced one-by-one in Step 4 instead (see "Calendar items" below). Empty when Google auth is not set up.
- `calendarUnavailable` — `true` when Google was configured but the fetch failed (timeout, expired token, scope revoked, etc.). When `true`, note it once during the walk so the user knows the stand-up is running blind on calendar.
- `plan` — the full `LifePlan` (schema `bishop.life/v1`): `meta`, `areas[]` with goals/actions, `inbox[]`, `standups[]`. Action `horizon` is one of `"today"`, `"thisWeek"`, `"thisMonth"`, `"someday"` — orthogonal to goal `horizon` (the `YYYY-MM` target month). Use this for the Step 7 composition; do not re-read `bishop.life.json` directly.

If the CLI exits non-zero, surface the stderr message and STOP.

### Step 2 — Display the surfaced context

Display the pack to the user verbatim — a calm, scannable block. No advice yet. Calendar items are **deliberately omitted** from this summary so they don't pre-empt the brain-dump; they get walked in Step 4.

Wrap the entire context block in `<!-- no-speak -->...<!-- /no-speak -->` markers so the TTS hook skips it (the opening question in Step 3 is what should be spoken, not the surfaced data). Example shape:

```
<!-- no-speak -->
**Last stand-up:** 2 days ago (2026-06-06)
**Open actions:** 7
**Starred (2/3):**
  • Finances ▸ Build 6-month emergency fund ▸ Move £500 to savings
  • Health ▸ Sleep before midnight ▸ Phone out of bedroom
**Untended areas:** Career, Relationships
**Inbox (2):**
  • Look into ISA limits
  • Book dentist
<!-- /no-speak -->
```

### Step 3 — Open by resuming the thread

The opening line is generated from state, never fixed (tenet 5). It is the one spoken line that starts the conversation and it doubles as the brain-dump invitation — there is no separate generic prompt. Vary the *content* (which thread, how it's phrased); hold the *register* constant: one line, one question, calm and understated, no perky or exclamatory energy. Because the line is grounded in whatever is actually live, it differs every stand-up on its own — do not manufacture variety for its own sake.

Pick the **single** most salient carried-over thread to open on — never a list (a list is a status readout, not a resumption). Selection order:

1. A previous-stand-up `focusToday[]` item that is **still open** — resolve its id to `area ▸ goal ▸ action title` via the `plan` returned in Step 1 (the most recent `standups[]` entry's `focusToday[]`, cross-referenced against the tree). These were explicitly the focus last time; they are what a person who remembered would ask about. *(If the CLI is later extended to surface a resolved `lastFocus[]` with `done`/`blocked` flags, prefer it — it removes the cross-referencing and makes the already-resolved case below reliable.)*
2. Failing that, a currently-`starred[]` item that is clearly mid-flight.

Open with the question that person would ask, then leave the floor open so the user can carry on into anything else:

> Morning. How did *{last focus item}* go? — and whatever else has come up since.

Read the user's response. It **is** the brain-dump — do not start follow-up questions here (the walk is Step 4). The trailing "...and whatever else has come up" keeps the floor open so the opener does not narrow the dump to that one thread.

**No-shame on carry-over (tenet 1, applied here).** The honest answer to "how did *X* go?" is often "I didn't get to it." Treat that as completely normal — acknowledge it and roll it forward; no comment on the gap, no disappointment, no "still?". A warm opener that flinches at "didn't happen" becomes a daily guilt-trip, which is the exact failure the ritual exists to avoid.

**Do not ask about what is already resolved.** If the carried-over focus item is already `done` (completed in a prior session or via the inline UI), do not ask how it went — acknowledge it in one clause and open on the next live thread instead, or fall through to the neutral open below.

**Fallback — nothing to resume.** When there is no live carried-over thread:

- First-ever stand-up (`lastStandupPhrase` is `"first stand-up"`): a calm first-time open — nothing on the board yet, what's on your mind to start.
- Quiet board, or everything carried over is done or dropped: grounded-but-neutral — e.g. "Quiet board, nothing carried over — what's on your mind?" Do not invent a callback that doesn't exist.

The visual context block (Step 2) is unchanged: it remains the silent, at-a-glance state on screen. This step changes only the *spoken* open — from a fixed prompt to a state-grounded resumption.

### Step 4 — Walk the raised threads

Identify the distinct threads in the brain-dump (each project, money item, life thing, person, worry — anything mentionable). Then walk them one at a time, in order. For each thread, ask the questions that are actually open — not a fixed checklist. Common shapes:

- **Does this even belong on the board?** Some things mentioned are just context or one-off plans (e.g. "I'm going fishing this weekend"). The board is for things the user would otherwise lose track of. If it's not that, the right move is to drop it — make "drop / don't record" an explicit option, not an assumption that everything gets captured.
- **Is it actually one action, or several?** A vague "I'm working on X" often dissolves into one concrete next step plus a lot of background. Resist creating multiple actions where there's really one (and resist a fuzzy umbrella action when there are genuinely several concrete next steps).
- **Is it done, blocked, or actionable now?** Mark done things `done: true` (with `completedAt`). Flag blocked things in the action title so it's obvious at a glance (e.g. "(blocked on X)") and deliberately don't star them — starring blocked work is noise.
- **Which area and goal does it fit under?** Prefer an existing goal. Create a new goal under an existing area if needed. Only add a new area if the user explicitly asks.
- **When a new goal is created, ask its target month.** Phrasing along the lines of "when do you want this goal hit by? (YYYY-MM)". The answer goes into `goal.horizon` (e.g. `"2026-12"`). Without it the Map tab buckets the goal as "Beyond". Accept "no idea" / "someday" → leave `horizon` null and move on; don't force a month.
- **Revisit an existing goal's horizon only when the walk surfaces a clear shift in urgency** — e.g. "I really need to land this before the end of summer", or "this isn't going to happen this year, pushing it out". A goal merely being *mentioned* is not a reason to re-ask; horizon stays as-is by default.
- **What's the action horizon?** For any action you're adding or surfacing, ask whether it's `today`, `thisWeek`, `thisMonth`, or `someday`. Default to the bucket the user's own words imply ("I want to nail this today" → `today`, "in the next few weeks" → `thisWeek`, vague "at some point" → `someday`) and confirm rather than re-asking. Horizon is orthogonal to starring — a starred someday action is a contradiction; flag it.
- **Standing autopilot things stay off the board.** If the user describes something as "on autopilot" / "happens by itself" / "I'm not actively doing anything about it", don't add a goal for it. The board is for active threads.
- **Existing goals/actions affected.** If the dump implies an existing action is done, a goal is renamed, or a starred item should be unstarred, raise it during the walk — don't quietly mutate things in Step 5.

Walk one thread per assistant message — do not bundle multiple threads into one question. Keep tone calm and conversational; this isn't an interrogation, it's a sort.

**Calendar items** — after walking everything the user raised, walk through any `calendar[]` entries from the context pack. These are agent-driven prompts, not user threads. For each event, ask one focused question — e.g. "You have *{summary}* on {start} — anything you want to prep for, or capture as an action?" Apply the same rules as user-raised threads: it might belong on the board, it might just be context, or it might be drop-worthy. Don't bundle multiple events into one message. If `calendarUnavailable` is `true`, note it once at the start of this phase ("Calendar wasn't reachable — running blind on upcoming events") and skip event prompts entirely.

End the walk with: "Anything I missed?" Read the user's response. If they raise new threads, walk those too. If not, proceed to Step 5.

### Step 5 — Compose the new file in memory

From the walk in Step 4 plus the `plan` returned by the CLI in Step 1, build a single new whole-file JSON object. By this point the decisions are mostly made — this step is mechanical assembly, not fresh judgement. Specifically:

- **Reflection** — 2–6 sentences synthesising what came out of the walk. Honest, non-judgemental. Reference specific items the user mentioned. Do not invent feelings the user did not express. Reflect what was resolved during the walk (e.g. "X is parked because of Y", "Z stays off the board"), not just what was raised.
- **Focus today** — pick 1–3 actions to focus on today, drawn from what the walk surfaced as actionable now (not blocked). The selected action ids go into the new stand-up's `focusToday[]`.
- **Starred state** — adjust `starred` on existing actions to reflect what matters this week. Hard ceiling: total starred across the whole file MUST be ≤ 3 after this write. If the walk surfaced a new priority that would exceed 3, unstar the least-relevant existing one (and call that out in the proposed-mutations list in Step 6). Never star blocked actions.
- **Inbox triage** — every item in `inbox[]` should have been resolved during the walk (promoted or dropped). After this step, `inbox[]` is empty.
- **Adds/edits/removes of areas, goals, actions** — the stand-up is the only path that mutates the tree. Apply whatever the walk produced: new actions, completed actions (`done: true`, `completedAt` set to now), renamed goals, new goals under existing areas, etc. Adding entire new *areas* is rare but allowed — only if the user explicitly asked. Do not add areas speculatively.
- **Ids** — for any new entity, mint an id of the form `<prefix>-<short-slug-or-random>`: `act-…` for actions, `goal-…` for goals, `area-…` for areas, `ibx-…` for inbox items (none created here, but the prefix is reserved), `su-…` for the new stand-up entry. Stable existing ids are never renamed.
- **Timestamps** — capture `$now` as `Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC` (or equivalent). Set `meta.lastStandupAt = $now`. Set `completedAt = $now` on any action you just marked done. Set `createdAt = $now` on any new action.
- **Horizon on actions** — for every new action, write the horizon agreed in the walk (default `"thisWeek"` if it never came up). For any existing action whose urgency shifted during the walk, update its horizon as part of this same write.
- **Horizon on goals** — for every new goal, write `goal.horizon` as the `YYYY-MM` agreed in the walk (or leave `null` if the user explicitly said they don't know). For an existing goal, only overwrite `horizon` when the walk surfaced a clear urgency shift — never silently rewrite an existing horizon just because the goal came up.
- **New stand-up entry** — append `{ id: "su-…", at: $now, reflection: "<reflection>", focusToday: ["act-…", …] }` to `standups[]`.
- **Trim** — after the append, if `standups.Count > 10`, drop the oldest entries until exactly 10 remain.
- **Colours, schema, area order** — leave untouched.

Preserve property order to match the seed shape (`schema`, `meta`, `areas`, `inbox`, `standups`) and indent with two spaces. This keeps hand-edit diffs readable (spec §4).

### Step 6 — Show the proposed change and confirm

Before any write, show the user:

- The drafted **reflection** (the prose, verbatim). This stays spoken — it's the synthesis the user benefits from hearing.
- The drafted **focusToday** as `area ▸ goal ▸ action title` lines, wrapped in `<!-- no-speak -->...<!-- /no-speak -->` so the TTS hook skips the list.
- A short bullet list of **tree mutations** you intend to apply, also wrapped in `<!-- no-speak -->...<!-- /no-speak -->`: actions added, actions marked done, stars added/removed, inbox items triaged or dropped, anything else.

Then ask:

> Write this stand-up? <!-- no-speak -->(`y` to commit / `n` to abandon — nothing has been written yet)<!-- /no-speak -->

- On `n` → STOP. The file on disk is unchanged. Do not write `.prev`, do not write `.tmp`.
- On `y` → proceed to Step 7.

### Step 7 — Atomic write

Use the `filePath` from the Step 1 context pack as `$path`. In this exact order (each step gated on the previous succeeding):

1. **Snapshot the pre-state** to `.prev` (single-step undo, spec §4):

   ```
   Copy-Item -LiteralPath $path -Destination "$path.prev" -Force
   ```

2. **Write the new whole-file JSON** to a `.tmp` sibling using the `Write` tool:

   - Target path: `"$path.tmp"`.
   - Content: the JSON object composed in Step 5, two-space indented, UTF-8 without BOM (the `Write` tool's default).

3. **Rename** `.tmp` over the live file:

   ```
   Move-Item -LiteralPath "$path.tmp" -Destination $path -Force
   ```

   The rename is the commit point — a kill mid-write can never leave a partial `bishop.life.json` (spec §5).

If any step fails, surface the error and STOP. Do not attempt to roll back from `.prev` automatically — the user does that by hand if they need to (it's why `.prev` exists).

### Step 8 — Confirm

Print a single short line:

> Stand-up saved. <!-- no-speak -->Backup at `<path>.prev` — delete or restore by hand if you need to undo.<!-- /no-speak -->

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
- Do NOT skip the per-item walk (Step 4). Diffing straight from brain-dump to drafted mutations turns the ritual into data entry and was explicitly rejected during dogfooding. The walk *is* the ritual.
- Do NOT bundle multiple raised threads into a single assistant message during the walk. One thread per message keeps the conversation a sort, not an interrogation.
- Do NOT assume everything raised in the brain-dump belongs on the board. "Drop / don't record" must be an explicit option offered during the walk for things that are context, autopilot, or one-off plans.
- Do NOT star blocked actions. Reflect the blocker in the action title instead (e.g. "(blocked on X)").
- Do NOT re-read `bishop.life.json` directly. The CLI emits both the surfaced context and the full `plan` — use that for Step 5 composition.
- Do NOT surface `calendar[]` events in the Step 2 summary — calendar items are walked one-by-one in Step 4, *after* the brain-dump, so that real-life dates don't pre-empt what the user wanted to bring up themselves.
- Do NOT use a fixed opening line. The spoken open is generated from the carried-over thread; a static greeting goes stale (Step 3, tenet 5).
- Do NOT open by listing multiple threads — pick one. Listing is a status readout, not a resumption.
- Do NOT react to "didn't happen" with comment, disappointment, or "still?" — acknowledge and roll it forward (tenet 1).
- Do NOT ask "how did X go?" about an action already marked `done` — acknowledge in one clause and move to the next live thread, or use the neutral open.
- Do NOT let the resumed-thread opener narrow the dump — the floor stays open for anything else the user wants to raise.
- Do NOT fabricate a carried-over thread when none exists (first stand-up, quiet board) — use the neutral grounded open instead.

</guardrails>
