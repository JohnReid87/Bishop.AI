---
name: bish-write-skill
description: Author a new Bishop skill — interview to pick a category (Conversational / Review / Setup-Execute / Bishop-level), emit a skeleton `SKILL.md` to `skills/<name>/`, and check the result against the family's canonical patterns. Use when adding a new `bish-*` skill or rewriting one from scratch.
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion, Bash(bishop:*)
bishop.category: meta
---

> Recommended model: Sonnet 4.6 — structured authoring procedure; extended reasoning not required.

## What this skill is

A **Bishop-level meta-skill**. It operates on `skills/` in the Bishop.AI
repository (or any directory the user nominates that already holds
`bish-*` skill directories), not on a workspace's application code.

The soul is the **authoring contract**: every Bishop skill belongs to one
of four categories, and the category determines which content leads the
`SKILL.md`, what may be extracted to shared surfaces, and which canonical
sections it may reference rather than restate. Codifying the contract
here is the whole point — new skills must not re-derive it, and existing
skills must not drift from it.

For the reasoning behind the categories and extraction taxonomy, see
[`docs/SKILL_FAMILY.md`](../../docs/SKILL_FAMILY.md). This skill is the
authoring counterpart to that document; if the two disagree,
`SKILL_FAMILY.md` is the source of truth and this skill is stale —
flag it and fix it before continuing.

Inspiration: Matt Pocock's `write-a-skill` —
<https://github.com/mattpocock/skills/blob/main/skills/productivity/write-a-skill/SKILL.md>.

---

## Skill category triage

Every Bishop skill maps to exactly one category. Pick the category
**first** — it determines the skeleton, the leading content, and what
the skill may extract.

| Category | Existing members | What the skill *is*, fundamentally | SKILL.md leads with |
|---|---|---|---|
| **Conversational** | `bish-grill-cards`, `bish-grill-docs`, `bish-chat`, `bish-scripts` | An interview style — relentless interrogation, drift-tolerant chat, or a bug-skeleton walk. The soul is the *quality bar of the conversation*. | The contract / quality bar ("relentless means…", "non-mutating single-card chat", "bug-skeleton walk"). |
| **Review** | `bish-arch`, `bish-security`, `bish-tests`, `bish-coverage`, `bish-audit-docs`, `bish-triage` | A body of heuristics applied to the codebase, then walked with the user finding-by-finding. The soul is the *heuristic catalogue*. | The heuristic catalogue (SOLID checks, CVE patterns, test-quality dimensions, coverage thresholds, doc-drift classifications). |
| **Setup-Execute** | `bish-onboard`, `bish-auto-card`, `bish-work-on-card` | A deterministic procedure that mutates state (filesystem, board, git). The soul is the *procedure itself*. | The procedure, top to bottom. No "purpose-first" reordering — the agent must read steps in order. |
| **Bishop-level / meta** | `bish-write-skill` (this file), `bish-audit-skills` | Skills *about* the skill family. Operate on `skills/` directly, not on a workspace's code. | The authoring contract / audit checklist. |

**Rule of thumb:** if a paragraph could be in any skill in the family, it
is plumbing — extract it (see §"Canonical references" below). If it
would lose meaning outside this skill, it is soul — keep it inline and
prominent.

---

## Canonical references — never restate, always link

Five named sections live in `BISHOP_CONTEXT.md` (auto-generated from
`src/Bishop.App/Terminal/BishopContext.static.md`). Skills point at them
by heading. A new skill **must not** restate any of these — the agent
already loads `BISHOP_CONTEXT.md` on orientation, and duplicated wording
drifts.

| Section | Label | Use when your skill… |
|---|---|---|
| `## Card Push Procedure` | STABLE | Pushes one or more cards via `bishop card add`. |
| `## Task List Preview Format` | STABLE | Emits a multi-card preview the user must confirm before push. |
| `## Source Card Closing Prompt` | STABLE | Was launched against a source card and spawned children from it. |
| `## Card Granularity Rules` | TUNABLE | Decides whether to merge / split proposed cards before push. |
| `## Per-finding Walk Pattern` | TUNABLE | Walks a list of findings one at a time (review skills). |

**STABLE vs TUNABLE.** STABLE sections are behaviour contracts — the
agent must not paraphrase, reorder, or skip steps. TUNABLE sections are
voice / copy — paraphrase to fit context. Both are referenced the same
way (by heading); the label tells the agent how much rope it has.

Reference syntax inside a skill body — use the `conventions` dict key:

```markdown
…per Card Push Procedure (STABLE) (in `conventions`).
…apply Card Granularity Rules (TUNABLE) (in `conventions`).
```

The deterministic mechanics that *every* workspace-level skill needs
(workspace + tag + lane resolution) are bundled by `bishop context-pack <skill-name>`.
Every Conversational / Review / Setup-Execute skill calls it first; the pack's
`conventions` key carries the STABLE/TUNABLE sections. Bishop-level / meta
skills (this one) do **not** call it — they operate on `skills/` and
don't need workspace context.

**Anti-pattern:** lifting heuristic content (SOLID checks, CVE patterns,
test-quality dimensions) into `BISHOP_CONTEXT.md`. Heuristics are
skill-specific — each review skill's catalogue *is* its differentiator.
Keep heuristics inline in the review skill.

---

<what-to-do>

1. **Locate the `skills/` root.** Glob `skills/bish-*` from the current
   working directory. If at least one match comes back, capture the
   parent directory as `skillsRoot` and continue. If nothing matches,
   STOP and tell the user:

   > No `skills/bish-*` directories found under the current working
   > directory. Run `/bish-write-skill` from the Bishop.AI repo root
   > (or any directory containing a `skills/` folder with existing
   > `bish-*` skills).

2. **Gather inputs via `AskUserQuestion`.** One question at a time.

   - **Skill name** — free text. Must match `^bish-[a-z][a-z0-9-]+$`.
     Reject names that already exist under `skillsRoot` (`Glob`
     check). Reject names without the `bish-` prefix — the family
     convention is non-negotiable.
   - **One-line description** — free text. This becomes the
     frontmatter `description` and the entry the agent sees when
     choosing between skills, so be specific. End with a trigger
     phrase ("Use when…" / "Use when the user invokes …").
   - **Category** — one of the four. Use `AskUserQuestion` with the
     triage table above; the first option is your recommended fit
     based on the description, suffixed `" (Recommended)"`.
   - **Slash-command surface** — does the skill get a button on each
     card, on the workspace header, both, or neither?
     - `card` — `bishop.scope: card`, command template uses
       `{{card_number}}`.
     - `workspace` — `bishop.scope: workspace`, template uses
       `{{workspace_path}}` (or no placeholder).
     - `card,workspace` — both buttons, both templates.
     - `none` — no button; user invokes via `/<name>` only. Always
       the answer for Bishop-level / meta skills.
   - **Staging dialog** — only if `scope` is `card` or `workspace`.
     `bishop.stage: true` when the click should open a staging
     dialog (e.g. "what should I grill you on?") before launch;
     `false` otherwise. Provide `bishop.stage_prompt` only when
     `stage: true`.
   - **Tag the skill writes** — applies to skills that push cards.
     Must be one of the workspace's existing tag names — use
     `bishop tag list` (no `-w` flag — runs in the current
     workspace) to enumerate. Skip if the skill does not push.

3. **Emit the skeleton.** Pick the matching skeleton from §"Skeletons"
   below, fill in the captured values, and `Write` it to
   `<skillsRoot>/<name>/SKILL.md`. Create the directory first; do not
   overwrite an existing one.

4. **Self-check against the audit checklist.** Walk these items once
   against the file you just wrote, before handing back to the user.
   Any "no" is a bug in the skeleton — fix it inline rather than
   shipping a known-broken file.

   1. **Category** — frontmatter `bishop.category` matches the chosen
      category (`discuss` / `review` / `setup` / `execute` / `meta`).
   2. **Leading content** — does the file lead with the
      category-appropriate content (per the triage table)? If a
      Conversational skill leads with workspace-detection, fix it.
   3. **Workspace detection** — does it call `bishop skill bootstrap` (or, for `bish-onboard`, carry a one-line comment explaining why bespoke detection is required)? If inline workspace-detection prose remains, flag it.
   4. **Card push** — if the skill pushes cards, the body references
      `## Card Push Procedure (STABLE)` rather than restating the
      write→push→remove temp-file procedure.
   5. **Task-list previews** — if the skill emits a multi-card
      preview, it references `## Task List Preview Format (STABLE)`.
   6. **Source-card closing** — if the skill spawns child cards from
      a source card, it references `## Source Card Closing Prompt (STABLE)`.
   7. **STABLE sections** — the body does not paraphrase any STABLE
      section anywhere.
   8. **Heuristic content** — for review skills: are the heuristics
      still the dominant portion of the file? If extraction has
      eroded them, restore.
   9. **Procedural flow** — for setup/execute skills: is the
      procedure readable top-to-bottom as imperative steps?
      Reordering for "purpose-first" is a bug here.
   10. **Frontmatter completeness** — `name`, `description`,
       `allowed-tools` present and accurate. `bishop.scope`,
       `bishop.command`, `bishop.stage`, `bishop.stage_prompt`,
       `bishop.category` set per the inputs gathered in step 2.

5. **Tell the user what to do next.** Print a short summary:

   > ## Created `<name>`
   >
   > - File: `<skillsRoot>/<name>/SKILL.md`
   > - Category: `<category>`
   > - Surface: `<scope or "none">`
   >
   > **Next steps:**
   > 1. Review the skeleton and replace the `TODO:` markers with the
   >    real interview / catalogue / procedure body.
   > 2. Run `dotnet build` and `bishop install-skills` so the new
   >    skill ships with the next `bishop.exe` rebuild.
   > 3. If the skill takes a slash-command surface, add it to the
   >    bundled-skills list in `README.md`, `CONTEXT.md`,
   >    `DIRECTION.md`, and the `Workflow` section of
   >    `src/Bishop.App/Terminal/BishopContext.static.md` (the static
   >    seed for `BISHOP_CONTEXT.md`).

   Do **not** push a card, edit `BISHOP_CONTEXT.static.md`, or modify
   any list-of-skills file automatically — the user owns those
   decisions. This skill writes the skeleton; the user wires it up.

</what-to-do>

---

## Skeletons

Each skeleton is a complete `SKILL.md`. Drop unused sections; do not
add sections that aren't in the skeleton without a reason that would
hold up at audit.

`TODO:` markers point at the spots the author must fill in. Plain
`<placeholder>` markers indicate substitutions the agent fills from
the step-2 inputs.

### Skeleton — Conversational

For skills whose core is an interview or chat. Modelled on
`bish-grill-cards`, `bish-grill-docs`, `bish-chat`, and `bish-scripts`.

````markdown
---
name: <name>
description: <one-line description>
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: <scope>
bishop.command: /<name> {{card_number}}
bishop.stage: <true|false>
bishop.stage_prompt: "<staging prompt if stage:true>"
bishop.category: discuss
---

> Recommended model: <tier> — <one-line reason>

## What this skill is

A **<one-phrase contract>** (e.g. "relentless interview", "non-mutating
single-card chat", "bug-skeleton walk"). The soul of the skill is
<the quality bar / what the conversation refuses to do>.

The contract:

- **<rule 1>** — <one sentence>.
- **<rule 2>** — <one sentence>.
- **<rule 3>** — <one sentence>.

<!-- TODO: state the wrap-up shape — what the agent produces at the end
and what gate (preview + "push") it waits on before writing to the
board. -->

---

**Initialize from `bishop context-pack`.** Run `bishop context-pack <skill-name>`.
If it exits non-zero, surface the stderr line verbatim and STOP.
The pack provides `workspace.name`, `workspace.tags[].name`, `workspace.lanes[].name`,
and `conventions` sections for STABLE/TUNABLE references.

> **Workspace:** \<workspace.name\>

---

**Resolve the seed from `$ARGUMENTS`.** Three paths:

1. **`$ARGUMENTS` is a card Number** (matches `^#?\d+$`) — run
   `bishop card view <number> --json`, capture `number`, `title`,
   `description`, `tags`, `laneName`. Remember `number` as the
   **source card** for the closing prompt. Echo back:

   > **<verb>-ing card #N:** \<title\> *(lane: \<laneName\>, tags: \<comma-joined\>)*

2. **`$ARGUMENTS` is non-empty free text** — use verbatim as the seed.
   No source card; skip the closing prompt later.

3. **`$ARGUMENTS` is empty** — ask in chat what the user wants to
   discuss and wait for their reply. No source card.

---

## The conversation

<!-- TODO: describe the conversation core. One question at a time.
`AskUserQuestion` for discrete answer sets, free-text for genuinely open
prompts. Always offer a recommended answer as the first option suffixed
" (Recommended)". Delegate code questions to the Explore subagent
(`Agent` with `subagent_type: "Explore"` and `model: "haiku"`) for `file:line`
excerpts — do not Read whole files into the conversation. -->

---

## Wrap-up

<!-- TODO: state the wrap-up trigger (natural pause, user says "push"
/ "wrap up", checklist complete) and the proposal shape (cards to push,
source-card edits, etc.). -->

Apply Card Granularity Rules (TUNABLE) (in `conventions`) before listing follow-ups.
Print the preview per Task List Preview Format (STABLE) (in `conventions`). Each card body uses the
template below.

**Tag** defaults to `<your-tag>` (must be one of `tags[].name`).
**Lane** defaults to `To Do`; use another lane only when the user asks.

### Body template

```markdown
### Why
<what this task does, 1 sentence — do not restate the title>

### Acceptance
- <acceptance criterion>
```

After the preview:

> Please review the tasks above. Say **push** to create the Bishop cards.

Do NOT push automatically.

---

## Push

When the user confirms, add each card per Card Push Procedure (STABLE) (in `conventions`). Push with `--bottom` so cards
land in agreed order.

Print a summary table:

| Card | Title | Lane | Tag |
|------|-------|------|-----|
| #N   | …     | …    | …   |

---

## Closing card-action prompt

If a **source card** was captured at the start (Path 1), prompt per
Source Card Closing Prompt (STABLE) (in `conventions`). If
there is no source card (Paths 2 and 3), skip this prompt entirely.

ARGUMENTS: $ARGUMENTS
````

### Skeleton — Review

For skills that apply a heuristic catalogue to the codebase and walk
findings with the user. Modelled on `bish-arch`, `bish-security`,
`bish-tests`, `bish-coverage`, `bish-audit-docs`, `bish-triage`.

````markdown
---
name: <name>
description: <one-line description>
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /<name>
bishop.stage: false
bishop.category: review
---

> Recommended model: <tier> — <one-line reason>

**Before anything else — initialize from `bishop context-pack`:**

Run `bishop context-pack <skill-name>`. If it exits non-zero, surface the
stderr line verbatim and STOP. Capture `workspace.name`,
`workspace.path`, `workspace.tags[].name`, `workspace.lanes[].name` from the pack.

> **Workspace:** \<workspace.name\>

---

## What this skill does

<!-- TODO: 2–3 sentences. What is being reviewed, against what bar,
and how the output lands (cards in `To Do` tagged `<tag>`). State
explicitly that the skill is project-agnostic if it adapts to multiple
stacks. -->

## Review dimensions

The discovery subagent applies these dimensions. Each finding must
cite at least one `file:line` location.

### Universal — apply to every project in scope

<!-- TODO: the heuristic catalogue. This is the soul. Keep it inline,
detailed, and ordered by severity / frequency. Examples from the
family:

- `bish-arch` → SOLID, .NET idioms, DI lifetimes, layer hygiene, public
  API surface, test seams.
- `bish-security` → injection (SQL / command / LDAP / XPath / path),
  weak crypto, hardcoded secrets, unsafe deserialization, missing
  authn/authz, sensitive logging.
- `bish-tests` → shallow asserts, brittle mocks, missing edge cases,
  untested public methods.
- `bish-coverage` → classes below line threshold, branch coverage.
- `bish-audit-docs` → drift between docs and code (commands, flags,
  module names, file paths).
-->

### Stack-conditional — apply only when the relevant SDK / package is detected

<!-- TODO: dimensions that only apply when a specific framework is in
play (EF Core, ASP.NET Core, Blazor, WPF, etc.). Omit if the skill is
single-stack. -->

<what-to-do>

1. **Workspace detection** (above).

2. **Discovery phase.** Spawn one Explore subagent (via `Agent` with
   `subagent_type: "Explore"`). Brief it with `workspacePath`, the
   dimensions above, and: at most 15 findings, ranked by severity,
   each with `severity` / `dimension` / `location` (file:line) /
   `what` / `why_it_matters` / `suggested_action`.

3. **Echo summary.** One-line overview per finding, severity-ordered.

4. **Triage loop.** Walk findings per Per-finding Walk Pattern (TUNABLE) (in `conventions`). For each finding, use
   `AskUserQuestion`: **Card it (new)** (Recommended when high-sev),
   **Cluster with #N**, **Dismiss — context**, **Defer**.

5. **Ensure the `<tag>` tag exists.** If `workspace.tags[].name` from the pack
   doesn't include `<tag>`, tell the user to re-run
   `bishop workspace init` (which restores the canonical tag set) and
   STOP.

6. **Granularity pass.** Apply Card Granularity Rules (TUNABLE) (in `conventions`) before previewing.

7. **Print task list** per Task List Preview Format (STABLE) (in `conventions`). Ask:

   > Please review the tasks above. Say **push** to create the Bishop cards.

8. **Push confirmed cards** per Card Push Procedure (STABLE) (in `conventions`). Always `--lane "To Do"`,
   `--tag <tag>`, and `--bottom`.

9. **Print summary table:**

   | Card | Title | Lane | Severity |
   |------|-------|------|----------|
   | #N | … | To Do | high |

</what-to-do>

## Card body template

```markdown
### Why
<dimension> — <what's wrong, 1 sentence>. Locations: `<file:line[, file:line]...>`.

### Acceptance
- <how we'll know it's done>
```

<guardrails>

- Do NOT push cards before the user confirms each finding via triage
  AND says "push" at the task list stage.
- Do NOT propose findings without `file:line` citations.
- All `<tag>` cards land in `To Do`. Always pass `--bottom` —
  reviews are bulk pushes and must not jump ahead of manually
  prioritised work.
- <!-- TODO: any stack-detection invariants ("don't apply MediatR
  dimensions if MediatR isn't referenced"). -->

</guardrails>
````

### Skeleton — Setup-Execute

For skills whose value is a deterministic procedure mutating state.
Modelled on `bish-onboard` (setup) and `bish-work-on-card` /
`bish-auto-card` (execute). Lead with the procedure top-to-bottom;
do **not** open with a "What this skill is" header — the steps are
the story.

````markdown
---
name: <name>
description: <one-line description>
allowed-tools: Bash(bishop:*), Bash(dotnet:*), Bash(git:*), Read, Edit, Write, Glob, Grep, Agent
bishop.scope: <scope>
bishop.command: /<name> {{card_number}}
bishop.stage: false
bishop.category: <setup|execute>
---

> Recommended model: <tier> — <one-line reason>

**Goal:** <one sentence — what this procedure accomplishes and the
end state on success>.

<!-- TODO: only include this paragraph when the skill takes
arguments. Spell out what `$ARGUMENTS` may contain and any
multi-argument STOP behaviour. -->

<what-to-do>

**Before anything else — initialize from `bishop context-pack`:**

Run `bishop context-pack <skill-name> [--card N]`. If it exits non-zero, surface the stderr
line verbatim and STOP. Echo the workspace name back on its own line.

> **Workspace:** \<workspace.name\>

<!-- The `bish-onboard` exception: that skill runs *before* a
workspace exists, so it cannot use context-pack. Keep this exception
narrow — if your skill might run before context-pack can succeed,
document the reason inline. -->

---

1. **<Step 1 — one verb, one sentence>.**
   <!-- TODO: command(s) to run, JSON to parse, what to capture from
   the result. -->

   If <failure condition>, STOP and surface the stderr as-is. Do not
   improvise a recovery.

2. **<Step 2>.**
   <!-- TODO: -->

3. **<Step 3>.**
   <!-- TODO: -->

   <!-- For execute-style skills that touch production code: include a
   test-coverage check step here before any build/test step. -->

4. **Validate.**
   - Run `dotnet build` and confirm it succeeds.
   - Run `dotnet test` and confirm none are broken.

5. **Completion summary** — one short markdown block:

   ## Done — <subject>
   - <outcome 1>
   - <outcome 2>

6. **<Final state change>.** <!-- TODO: prompt for the user to confirm
   any irreversible state change (move card to Done, commit, etc.).
   Skill is `execute`-style if this step runs; `setup`-style skills
   usually end at the summary. -->

</what-to-do>

<guardrails>

- <!-- TODO: list invariants for this procedure. Examples:
  - One card per session — refuse multiple IDs.
  - Do NOT move to Done automatically — user reviews first.
  - If a CLI step fails, STOP and surface stderr rather than
    improvising a substitute.
  - Do NOT call `bishop workspace init` with non-default flags
    unless the user asks. -->

</guardrails>
````

### Skeleton — Bishop-level / meta

For skills *about* the skill family — authoring guides, audits. Operate
on `skills/` (or `docs/`) directly. Do **not** call
`bishop skill bootstrap` — no workspace context is needed.

````markdown
---
name: <name>
description: <one-line description>
allowed-tools: Read, Glob, Grep, Write, Edit, AskUserQuestion
bishop.category: meta
---

> Recommended model: <tier> — <one-line reason>

## What this skill is

A **Bishop-level meta-skill**. It operates on `skills/` (or wherever the
relevant skill family lives), not on a workspace's application code.

The soul is <the contract / checklist / heuristic this skill embodies>.
For the reasoning behind the categories and patterns, see
[`docs/SKILL_FAMILY.md`](../../docs/SKILL_FAMILY.md) — this skill is
its authoring / audit counterpart.

---

<!-- TODO: the contract or checklist. For an authoring skill, the
triage table + skeletons (this file's shape). For an audit skill, the
checklist items walked one at a time against each skill found in
`skills/`. -->

---

<what-to-do>

1. **Locate the `skills/` root.** Glob `skills/bish-*` from CWD. If
   nothing matches, STOP and tell the user to re-run from the
   Bishop.AI repo root.

2. <!-- TODO: gather inputs / walk the checklist. -->

3. <!-- TODO: produce the output (write a file, print a report). -->

4. **Self-check** — walk the audit checklist in
   [`docs/SKILL_FAMILY.md`](../../docs/SKILL_FAMILY.md) §7 against
   the file(s) produced or reviewed.

</what-to-do>

<guardrails>

- Do NOT modify any list-of-skills file (`README.md`, `CONTEXT.md`,
  `DIRECTION.md`, `BishopContext.static.md`) automatically — the user
  wires those up.
- Do NOT call `bishop skill bootstrap`; meta skills do not need
  workspace context.
- Do NOT push cards. Meta skills emit guidance / files / reports,
  not board state.

</guardrails>
````

---

## Worked example — `bish-arch` mapped onto the Review skeleton

The Review skeleton above reproduces, in skeleton form, what `bish-arch`
ships today. Cross-check by reading `skills/bish-arch/SKILL.md` against
the skeleton: the order of frontmatter keys, the context-pack preamble,
the "What this skill does" / "Review dimensions" pair, the discovery
→ triage → granularity → preview → push flow, the canonical-section
references, and the `<guardrails>` block all line up. If you write a
new review skill (e.g. `bish-perf`) by filling in the skeleton's
heuristic catalogue and tag, the resulting file should differ from
`bish-arch` only in the dimensions section and the tag name.

The other three skeletons map onto `bish-grill-cards`, `bish-onboard`, and
this file (`bish-write-skill`) respectively.

<guardrails>

- Do NOT modify any list-of-skills file (`README.md`, `CONTEXT.md`,
  `DIRECTION.md`, `src/Bishop.App/Terminal/BishopContext.static.md`)
  automatically. The user wires those up after reviewing the
  skeleton.
- Do NOT call `bishop skill bootstrap`; this is a meta skill and does
  not need workspace context.
- Do NOT push cards. The skeleton is the output; cards are the
  user's call.
- Do NOT overwrite an existing `skills/<name>/SKILL.md`. Refuse the
  name in step 2 if a match already exists under `skillsRoot`.
- If `docs/SKILL_FAMILY.md` and this file disagree on the triage /
  extraction taxonomy, `SKILL_FAMILY.md` is the source of truth.
  Flag the drift and STOP rather than emitting a skeleton based on
  the stale rules.

</guardrails>
