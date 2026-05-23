---
name: bish-audit-docs
description: Audit Markdown docs in the current repo for drift against the code, then walk the user through per-finding confirmations and edit the docs in place. Use when the user wants to check that README/CONTEXT/etc. still match what is actually shipped.
allowed-tools: Read, Glob, Grep, Edit, Write, Agent, AskUserQuestion, Bash(bishop:*), Bash(git:*)
bishop.scope: workspace
bishop.command: /bish-audit-docs
bishop.stage: false
bishop.category: review
---

> Recommended model: Sonnet 4.6 — doc-drift detection is pattern-matching; extended reasoning not required.

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

---

Audit the repo's Markdown documentation for drift against the current code,
confirm each finding with the user, and apply the agreed edits in place.

<what-to-do>

**1. Soft workspace detect.**

Run `bishop skill bootstrap`.

- On success, echo the workspace name:

  > **Workspace:** <name>

- On non-zero exit, fall back to the git repo name
  (`git rev-parse --show-toplevel`, then take the leaf directory). Echo:

  > **Not in a Bishop workspace — auditing repo:** <leaf-dir>

  Do **not** refuse to run. This skill is useful outside Bishop workspaces too.

---

**2. Discover candidate Markdown files.**

Use `Glob` to enumerate `**/*.md` under the repo root. Exclude any path
containing these segments:

- `/_archive/` (preserved historical notes — never edit)
- `/.github/` (issue/PR templates, not project docs)
- `/node_modules/`
- `/bin/`, `/obj/` (build output)

Also exclude these filenames anywhere in the tree:

- `.bishop/BISHOP_CONTEXT.md` (auto-generated on workspace launch)
- `.bishop/BISHOP_NOTES.md` (agent scratchpad)

Present the candidates via `AskUserQuestion` as a **multi-select** list so the
user can narrow the audit scope. Pre-select the obvious top-level docs
(`README.md`, `CONTEXT.md`, `DIRECTION.md`, `CONTRIBUTING.md`) if present.
Recommend the full default set as the first option.

---

**3. Drift extraction — delegate to the Explore subagent.**

For the selected files, spawn the `Explore` subagent (via the `Agent` tool with
`subagent_type: "Explore"`) with a structured prompt. The subagent must
classify every concrete factual claim in the docs as one of:

- `ACCURATE` — claim matches the code as shipped (do not surface these).
- `STALE` — claim is contradicted by the code (e.g. command name changed,
  flag removed, file moved, behaviour different).
- `AMBIGUOUS` — claim is vague or partially right; flag for human judgement.
- `MISSING` — code ships a user-facing behaviour the docs do not mention.

For every non-`ACCURATE` finding, the subagent must return:

- `doc_path` and `doc_line` (the line in the Markdown file the claim is on).
- `code_evidence` — file path + line number(s) in the source that the
  classification rests on, with a short excerpt.
- `current_wording` — the exact current text in the Markdown.
- `proposed_wording` — a concrete replacement, or `null` if the finding is
  `MISSING` and needs human input.
- `classification` — one of the four labels above.

Ask the subagent to return findings as a compact list (one per finding), not
prose. No paragraphs of analysis.

---

**4. Per-finding confirmation.**

For each finding, present an `AskUserQuestion` with options:

- **Accept** — apply `proposed_wording` as-is. (Recommended when classification
  is `STALE` and the evidence is concrete.)
- **Edit** — user supplies their own replacement (free-text path).
- **Reject** — leave the doc untouched.
- **Skip** — defer; treat the same as Reject for this run.

Show the user the file path, the line, the current wording, the proposed
wording, and the code evidence. Keep each prompt focused on one finding.

For `MISSING` findings, the options collapse to:

- **Add wording here** — user types where in the file to add it and what to
  add.
- **Surface in summary only** — defer; do not edit any file.
- **Skip**.

---

**5. Apply confirmed edits — existing files only.**

For each accepted finding, use `Edit` to apply the change to the existing
Markdown file. Do **not** create new files, even if a `MISSING` finding
arguably warrants one — surface those gaps in the closing summary instead and
let the user decide.

If an `Edit` fails because the `current_wording` is not unique on the page,
re-read the surrounding context, expand the `old_string`, and retry once. If
it still fails, surface the failure and move on — never silently skip an
accepted edit.

---

**6. Closing summary.**

Print a concise summary:

## Audit complete — <N> edits applied
**Files changed:**
- `path/to/file.md` — <one-line description of the edits>

**Findings surfaced but not applied:**
- <doc path + line> — <reason: rejected / MISSING gap / edit failed>

**Gaps worth documenting (no file edited):**
- <brief description + suggested location>

---

**7. Commit prompt.**

If any edits were applied, propose a Conventional Commits message:

> Proposed: `docs: audit — <N> fixes` — confirm, edit, or skip?

Stage and commit only after explicit confirmation. Do **not** push, and do
**not** create any Bishop cards — gaps stay in the summary so the user can
decide whether to file them later.

If no edits were applied, skip this step.

</what-to-do>

<guardrails>

- Do NOT create new Markdown files. `MISSING` findings go in the summary.
- Do NOT edit files under `/_archive/`, `/.github/`, `/node_modules/`,
  `/bin/`, or `/obj/`.
- Do NOT apply an edit without per-finding user confirmation. Batch-accept
  is intentionally not offered — drift audits are high-stakes for docs.
- Do NOT push or open a PR. Commit only on explicit confirmation; pushing is
  the user's call.
- Do NOT create Bishop cards from findings. This skill edits docs in place;
  card creation is `bish-grill-me`'s job.
- If the Explore subagent returns no findings, say so plainly and stop —
  do not invent drift to justify the run.

</guardrails>
