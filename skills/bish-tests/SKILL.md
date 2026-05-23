---
name: bish-tests
description: Audit test quality for well-covered classes in the current .NET Bishop workspace — read each test file, flag shallow asserts / missing edge cases / brittle mocks / untested public methods across four quality dimensions, and push agreed cards to the board tagged `test`. Use when the user wants to scope test-quality work, mentions "test review" / "test quality", or invokes `/bish-tests`. Targeted mode: pass a class name or folder path.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-tests
bishop.stage: false
bishop.category: review
---

> Recommended model: Opus 4.7 — test-quality heuristic catalogue requires sustained multi-step judgement.

**Orientation:** if `.bishop/BISHOP_CONTEXT.md` exists in the workspace, read it first — it documents this workspace's lanes, tags, and the safe `bishop` CLI subcommands. Bishop regenerates it on every launch so the content is current.

---

**Before anything else — initialize from `bishop skill bootstrap`:**

Run `bishop skill bootstrap --json`. If it exits non-zero, surface the stderr
line verbatim to the user and STOP — the helper already explains the
remediation. On success, parse the JSON and extract:

- `workspaceName` — echoed back as confirmation
- `tags[].name` — existing tag names (the skill needs a `test` tag; see step 6)
- `lanes[].name` — lane names (defaults to `To Do` when pushing)

Echo the workspace name back on its own line:

> **Workspace:** \<name\>

---

## What this skill expects

A .NET workspace with one or more test projects under `tests/` and source
files under `src/`. For the default (no-argument) flow, the skill also needs
a coverage summary at `TestResults/coverage-summary.json` in the schema
documented in the `bish-coverage` skill:

```json
{
  "schemaVersion": 1,
  "threshold": 80,
  "modules": [
    { "name": "<full class name>", "file": "<repo-relative path>", "lineCoverage": 0.0, "linesCoverable": 10 }
  ]
}
```

If the user passes a class name or folder path as the argument, the skill
skips the coverage requirement and audits the targeted scope directly.

Coverage tells you _whether_ each line was executed; this skill audits
_whether the executing tests would actually catch a bug_. Coverage and
quality are complementary — `/bish-coverage` files cards for under-covered
classes; `/bish-tests` files cards for well-covered classes whose tests
are shallow.

<what-to-do>

1. **Resolve scope.** Two paths:

   **Path A — no argument (default).** Read `TestResults/coverage-summary.json`.
   - If the file is missing, STOP and tell the user:

     > **No coverage summary found.** Run `/bish-coverage` first to produce
     > `TestResults/coverage-summary.json`, then re-run `/bish-tests`.

   - Validate `schemaVersion == 1`; if it's a future version the skill
     doesn't recognise, STOP and ask the user to update the skill.
   - From `modules[]`, keep entries with `lineCoverage >= summary.threshold`.
     These are the well-covered classes whose test quality is worth auditing.

   **Path B — argument supplied.** Treat the argument as either a class
   name (full or short, case-insensitive) or a folder path under `src/`.
   - Build the candidate list directly: match against `modules[].name` /
     `modules[].file` when a summary exists; otherwise enumerate matching
     `.cs` files via `Glob`. Coverage threshold does NOT apply in this mode.
   - If nothing matches, STOP and tell the user no source files matched.

2. **Find the test file for each candidate.** Two strategies, in order:

   - **Mirrored convention (strict, preferred).** Map
     `src/<TopProject>/<sub>/<Class>.cs` →
     `tests/<TestProject>/<sub>/<Class>Tests.cs`. The test project name is
     not fixed — discover it by globbing `tests/*/`.
   - **Reference search (fallback).** If the mirrored file does not exist,
     grep test files for `class <ClassName>` (as the system under test) or
     `new <ClassName>(` invocations. Pick the test file with the most
     references.
   - If neither produces a hit, skip the class with the note `no test file
     found; coverage likely indirect`. Do **not** file a card for it —
     record it for the summary table only.

3. **Cap and order.** Sort surviving candidates lowest-coverage-first
   (within the above-threshold set in Path A; by name in Path B) and take
   at most **15** per run. If more than 15 survive, tell the user after the
   audit:

   > Audited the lowest-coverage 15 of N candidates. Re-run `/bish-tests`
   > after working these cards to surface the next batch.

4. **Audit each test file via the Explore subagent.** For each (class,
   test file) pair, delegate to the `Explore` subagent (via the `Agent`
   tool with `subagent_type: "Explore"`). Brief it with:

   - The class name and source path.
   - The test file path.
   - The four quality dimensions (below).

   Ask it to return paths + line numbers + short excerpts (not whole
   files). Only fall back to direct Read/Grep for tight follow-ups on a
   specific range the Explore agent already surfaced.

   **The four dimensions:**

   1. **Assertion quality.** Are tests asserting on meaningful state
      (returned value, persisted side effects), or only that no exception
      was thrown / that a method was called? Tests with no `Should` /
      `Assert` are red flags.
   2. **Edge-case & error-path coverage.** Are null / empty / boundary /
      invalid inputs covered? Are the exception paths (`throw` statements
      in the class under test) exercised?
   3. **Brittleness / over-mocking.** Do mocks verify internal interactions
      that lock implementation details (e.g. asserting exact call sequences
      on collaborators when the public output is what matters)? Are real
      collaborators substituted where a fake DB / in-memory provider would
      be more representative?
   4. **Public-surface completeness.** Does each public method on the class
      have at least one direct test? Are there public methods covered only
      indirectly through another method's tests?

5. **Build one suggested card per class with at least one issue.** Classes
   with no findings are recorded as `clean` for the summary table — do not
   file a card.

   - **Title:** `Improve tests for <ClassName>`.
   - **Tag:** `test`.
   - **Lane:** `To Do`.
   - **Body** (H3-section markdown; `### Why` and `### Acceptance` are
     required):

     ```markdown
     ### Why
     `<ClassName>` is at `<coverage>%` but the test file has gaps in `<dimensions>`.

     ### Issues
     - **<dimension>**: <description, test method name, quoted offending line>

     ### Acceptance
     - <one bullet per listed issue, each addressed in `<FooTests.cs>`>

     ### Related
     - `<path/to/FooTests.cs>`
     ```

   `<dimensions>` in `### Why` is a comma-separated list of the dimensions
   that produced issues for this class (e.g. `assertion quality, edge cases`).

6. **Ensure the `test` tag exists.** Check `tags[].name` from the workspace
   JSON. If `test` is absent, stop and tell the user: the canonical tags are
   seeded by `bishop workspace init` — re-running it will restore any missing tags.

7. **Dedupe against existing cards.** Run `bishop card list --json`. Drop
   any suggestion whose title matches an existing card UNLESS that card is
   in the `Done` lane — Done cards represent shipped work, so a regression
   should re-surface.

   If every suggestion is filtered out, tell the user:

   > All audited classes already have open quality cards on the board.
   > Nothing new to file.

   Then continue to step 9 so the clean-classes summary still prints.

8. **Interview per surviving suggestion** with `AskUserQuestion`. For each
   one, show the class, the test file path, and the issues found. Offer:

   - **Push as-is (Recommended)** — file the card with the proposed
     title/body.
   - **Skip** — do not file this card.
   - **Edit title/body** — fall back to a free-text prompt for the
     replacement.

   When two or more suggestions clearly belong together (same dimension
   gap, same module/folder), offer a fourth option to merge them into one
   card before pushing.

   Push confirmed cards by calling `bishop card add` (see [bishop context print --section "Card model"](.bishop/BISHOP_CONTEXT.md#card-model)) with:

   - `--lane "To Do"`
   - `--title "<Title>"`
   - `--tag test`
   - `--description-file -` piping the body shape from step 5
   - `--bottom` — bulk pushes must not jump ahead of manually prioritised work

9. **Print a summary table.** Include audited-but-clean classes and
   no-test-file skips so the user can tell audited-good from
   skipped-no-test-file from filed:

   | Class | Coverage | Test file | Result |
   |-------|----------|-----------|--------|
   | `Foo` | 92% | `tests/.../FooTests.cs` | card #N |
   | `Bar` | 100% | `tests/.../BarTests.cs` | clean |
   | `Baz` | 88% | _none found_ | skipped — coverage likely indirect |

   Then offer:

   > Re-run `/bish-tests` after the new cards are worked. Dedupe makes
   > repeated runs safe.

10. **Record this run** by following [bishop context print --section "Skill-Run Recording Procedure"](.bishop/BISHOP_CONTEXT.md#skill-run-recording-procedure-stable) (STABLE) with `--skill bish-tests`.

</what-to-do>

<guardrails>

- Do NOT push cards before the user confirms each suggestion via the
  interview.
- Do NOT file a card for a class with `no test file found` — flag it in
  the summary as `skipped — coverage likely indirect` and move on.
- Do NOT run mutation testing (Stryker.NET) or otherwise execute the test
  suite as part of this skill. The skill reads test source only; coverage
  data comes from the latest `/bish-coverage` run. Mutation scoring
  belongs in a separate spike.
- Do NOT re-file a card whose title matches an existing open card. Re-runs
  must be idempotent.
- If the `test` tag is missing, stop and tell the user to re-run `bishop workspace init` to restore canonical tags. Never push untagged.
- Do NOT exceed 15 classes per run, even if more survive the threshold.
  Tell the user to re-run after working the cards.
- Do NOT assume a specific test framework or mocking library. Mirrored-path
  discovery uses the `<Class>Tests.cs` convention; reference-search
  fallback handles anything else.

</guardrails>
