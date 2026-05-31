---
name: bish-grill-docs
description: Interview the user relentlessly about a doc that needs writing — purpose, audience, structure — then write the markdown file in-session at the agreed path. Use when the follow-up to a grill is a document rather than code.
allowed-tools: Read, Glob, Grep, Write, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-grill-docs
bishop.stage: true
bishop.stage_prompt: "Doc path (optional) or what should I grill you about?"
bishop.category: discuss
---

## What this skill is

A **relentless interview** that ends in a markdown file written in-session.
The sibling of `bish-grill-cards`: same conversation quality bar, different
closing action. When the follow-up to a grill is a document rather than code,
pushing cards loses the conversation context (which *is* the spec). Writing the
doc while the context is hot keeps it.

Relentless means:

- **Walk every branch.** When a decision has dependent sub-decisions, resolve
  each one before moving on. Don't paper over forks with "we can decide that
  later."
- **One question at a time.** No batched lists of questions. The user steers;
  the agent narrows.
- **Always offer a recommended answer.** Every question carries your best
  guess as the first option — the user confirms or overrides, never picks
  blind. Use `AskUserQuestion` whenever the answer set is discrete; free-text
  only when genuinely open-ended (naming, novel design, free-form
  constraints).
- **Explore the codebase, don't read it.** When a question is answerable
  from the repo, delegate to the `Agent` tool with
  `subagent_type: "Explore"` and ask for `file:line` excerpts. Only fall
  back to direct Read/Grep for tight follow-ups on a file/line range Explore
  already surfaced.

The file lands at the agreed path after a **section-granularity pass** —
merge or split sections so each one stands on its own. The agent writes the
file directly; the interview surfaces the design and the file write is its
materialisation. No preview-and-confirm step on the rendered markdown — the
user edits in-tree afterwards if anything needs polishing.

This skill never calls `bishop card create`. If the interview surfaces follow-up
code work, invoke `bish-grill-cards` separately.

The context-pack below bundles workspace metadata, recent git history, and the
`Shell selection` convention — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack grill-docs
```

If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.path` — used to resolve the doc-path to an absolute path
- `conventions` — the `Shell selection` STABLE section

> **Workspace:** \<workspace.name\>

---

## Resolve the doc-path and seed

`$ARGUMENTS` carries optional input from the launch.

- **Looks like a path** — contains `/` or `\`, or ends in `.md`: treat as the
  doc-path. Resolve relative to `workspace.path`. Confirm with the user
  before continuing:

  > Writing to `<resolved-path>`. Proceed?

- **Non-empty, not a path**: treat as the **seed topic** for the grill. Ask
  for the doc-path early in the interview (first or second question).

- **Empty**: no seed and no path yet. Open the interview with: "What should
  I grill you on?" Then ask for the doc-path as the next early question.

In all cases, the doc-path must be resolved before the interview closes.
If the path points at an existing file, ask the user whether to overwrite,
append, or pick a different path before continuing.

---

## The interview

Interview the user relentlessly until shared understanding is reached. Walk
each branch of the design tree, resolving dependencies between decisions
one by one. For each question, provide your recommended answer as the first
option in `AskUserQuestion`, suffixed with " (Recommended)".

At minimum the interview walks these three axes:

1. **Audience** — who reads this doc? Future-you? A new contributor? An LLM
   loading project context? The audience shapes voice, depth, and what can
   be assumed.
2. **Genre / shape** — is this a reference (CONTEXT-style), a narrative
   (OVERVIEW-style), a decision log (DIRECTION-style), a how-to / runbook,
   a spec, or something else? Look at the existing docs in the repo for
   precedent before recommending.
3. **Section list** — the H2 (or H3) outline. Each section must justify its
   existence; the granularity pass below trims and merges.

Beyond those, follow whatever sub-branches the seed opens up — terminology,
scope boundaries, open questions to mark as deferred, examples to include,
diagrams to leave for later, etc.

When a question is answerable from the codebase, delegate to the **Explore
subagent** (via the `Agent` tool with `subagent_type: "Explore"` and
`model: "haiku"`). Brief it with the specific question and ask for `file:line`
excerpts — never pull whole files into this conversation.

---

## Section-granularity pass

Before writing the file, walk the agreed section list and apply these
heuristics:

- **Merge** sections that say the same thing for two audiences, or that
  cover the same topic with overlapping content.
- **Split** sections that bundle independent ideas — a reader scanning the
  TOC should find one topic per heading.
- **Cut** sections that exist only to fill the outline. A section with no
  content yet is a TODO, not a section.
- **Reorder** so dependent concepts read in dependency order — define
  before you reference.

Print the trimmed outline back to the user and ask for explicit confirmation
before writing. Wait for "write" (or equivalent) — do not write
automatically.

---

## Write the file

When the user confirms, use the `Write` tool to create the file at the
resolved doc-path. Populate each agreed section with content drawn from the
interview transcript. Backtick file paths, identifiers, and CLI commands.
Cross-link to related docs in the repo where relevant.

After the write succeeds, print a single confirmation line:

```
wrote <resolved-path>
```

The user reviews the file in-tree and edits by hand. There is no
section-level revise loop inside this skill — see the source card's "Out of
scope".

ARGUMENTS: $ARGUMENTS
