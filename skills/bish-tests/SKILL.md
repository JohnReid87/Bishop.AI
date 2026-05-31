---
name: bish-tests
description: Audit test quality for coverage-mature classes in the current .NET Bishop workspace — reads mutation-summary.json, finds classes above 80% line coverage but below 60% mutation score, and files per-class cards quoting survived mutants. Also flags brittleness/over-mocking as an orthogonal overlay. Use when the user wants to scope test-quality work, mentions "test review" / "test quality", or invokes `/bish-tests`. Targeted mode: pass a class name or folder path.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-tests
bishop.stage: true
bishop.stage_prompt: "Class name or folder path (leave blank for full workspace)"
bishop.stage_projects: true
bishop.category: tests
---

The context-pack below bundles workspace metadata, recent git history, and Bishop convention procedures (Shell selection, Card model, Findings Recording Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack tests
```
If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.tags` — existing tag names (the skill needs a `test` tag; see step 6)
- `workspace.lanes` — lane names (defaults to `To Do` when pushing)
- `conventions` — STABLE/TUNABLE procedure sections (Shell selection, Card model, Findings Recording Procedure)
- `skill_specific.prior_findings` — outcomes from previous `bish-tests` runs in this workspace; each entry has `identity_hash`, `project_name`, `file`, `symbol`, `rule`, `title`, `status` (`pending` / `dismissed` / `resolved` / `carded:#N`), `rebuttal_text`, and `linked_card_number`. Use during step 8 to skip findings the user has already decided on.

Echo the workspace name back on its own line:

> **Workspace:** \<name\>

---

## What this skill expects

A .NET workspace with one or more test projects under `tests/` and source
files under `src/`. For the default (no-argument) flow, the skill needs
two summary files produced by prior runs of `/bish-coverage` and `mutation.ps1`:

**Coverage summary** at `TestResults/coverage-summary.json`:
```json
{
  "schemaVersion": 1,
  "threshold": 80,
  "modules": [
    { "name": "<class name>", "file": "<repo-relative path>", "lineCoverage": 0.0, "linesCoverable": 10 }
  ]
}
```

**Mutation summary** at `TestResults/mutation-summary.json`:
```json
{
  "schemaVersion": 1,
  "threshold": 60,
  "generatedAt": "<ISO8601>",
  "modules": [
    {
      "name": "<class name>",
      "file": "<repo-relative path>",
      "mutationScore": 0.0,
      "mutantsCovered": 10,
      "survived": [
        { "line": 42, "mutator": "ArithmeticOperator", "original": "x + 1", "replacement": "x - 1" }
      ]
    }
  ]
}
```

If the user passes a class name or folder path as the argument, the skill
skips both summary requirements and audits the targeted scope directly.

Coverage tells you _whether_ each line was executed. Mutation score tells
you _whether the tests would actually catch a bug_ — a line can be covered
yet all mutants on it survive if the tests never assert on the outcome.
`/bish-tests` targets the intersection: well-covered classes with weak mutation scores.

## Quality signal

The primary signal is **survived mutants**: lines where the test suite
executed but no assertion failed when the code was changed. Each survived
mutant is a concrete, quoted finding (e.g. `RunFormatting.cs:20 — cache > 0
→ cache >= 0, no test failed`). Acceptance is closed-loop: re-run
`mutation.ps1`, the mutant must appear as `Killed`.

An orthogonal **brittleness / over-mocking** overlay is applied on top:
the Explore step also flags mocks that verify internal interactions rather
than public outcomes. These two concerns are independent — mutation testing
does not subsume brittle mocks.

### Equivalent mutants

Some survived mutants are *equivalent*: the mutation is semantically identical
to the original and can never be killed by any test (e.g. flipping a boundary
comparison on a value that never reaches that boundary). Filing a card for an
equivalent mutant is wasted effort — dismiss it so it does not re-surface.

**Dismissal mechanism:** add a `// Stryker disable once <MutatorName>` comment
on the line immediately **above** the mutated expression in the source file:

```csharp
// Stryker disable once ArithmeticOperator
return x + 1;
```

On the next `mutation.ps1` run Stryker marks the mutant `Ignored`. The
transform's `status == 'Survived'` filter already excludes `Ignored` mutants,
so the dismissed entry will not appear in `survived[]` or trigger a card on
any future run. The per-finding walk (step 8) will prompt you for each mutant
you flag as equivalent during the interview.

---

<what-to-do>

1. **Resolve scope.** Two paths:

   **Project name for findings.** Before either path, derive a `--project` value
   for `bishop findings record` (see step 10). The staging dialog's project
   picker writes `src/<ProjectName>` into the argument; treat that as the
   authoritative source:
   - If the argument matches `src/<ProjectName>` (or `src\<ProjectName>`) —
     possibly with a trailing sub-path — set `projectName` to the second path
     segment (e.g. `src/Bishop.App` → `Bishop.App`,
     `src/Bishop.UI/Views` → `Bishop.UI`).
   - Otherwise (blank, a bare class name, or any other shape) leave
     `projectName` unset — the run is recorded without `--project` and keyed
     on `(workspace, bish-tests, null)`.

   Carry this value through to every `bishop findings record` invocation
   below — both the no-findings paths in this step / step 7 and the final
   call in step 10.

   **Path A — no argument (default).** Read both summary files.

   - Read `TestResults/coverage-summary.json`.
     - If missing, STOP:
       > **No coverage summary found.** Run `/bish-coverage` first to produce
       > `TestResults/coverage-summary.json`, then re-run `/bish-tests`.
     - Validate `schemaVersion == 1`; if future version, STOP and ask the
       user to update the skill.
     - Build the **coverage-mature set**: `modules[]` entries with
       `lineCoverage >= summary.threshold` (typically 80%).

   - Read `TestResults/mutation-summary.json`.
     - If missing, STOP:
       > **No mutation summary found.** Run `mutation.ps1` first to produce
       > `TestResults/mutation-summary.json`, then re-run `/bish-tests`.
     - Validate `schemaVersion == 1`; if future version, STOP and ask the
       user to update the skill.
     - From `modules[]`, keep entries with `mutationScore < summary.threshold`
       (typically 60). This is the **mutation-weak set**.

   - Intersect: candidates = classes that are in both the coverage-mature
     set AND the mutation-weak set, matched by `name` (case-insensitive).
     If the intersection is empty, tell the user:
     > All coverage-mature classes are above the 60% mutation threshold. Nothing to file.

     Record this run via the no-findings path of `Findings Recording Procedure`
     (in `conventions`) with `--skill bish-tests` (plus `--project <name>` when
     a `projectName` was derived above) and STOP.

   **Path B — argument supplied.** Treat the argument as either a class
   name (full or short, case-insensitive) or a folder path under `src/`.
   - Build the candidate list directly: match against `modules[].name` /
     `modules[].file` from the mutation summary when it exists; otherwise
     enumerate matching `.cs` files via `Glob`. Thresholds do NOT apply.
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
     found; coverage likely indirect`. Do **not** file a card — record it
     for the summary table only.

3. **Cap and order.** Sort candidates by `mutationScore` ascending (weakest
   first) and take at most **15** per run. If more than 15 survive, tell the
   user after the audit:

   > Audited the 15 weakest-mutation-score classes of N candidates. Re-run
   > `/bish-tests` after working these cards to surface the next batch.

4. **Analyse each class via the Explore subagent.** For each (class, test
   file) pair, delegate to the `Explore` subagent (via the `Agent` tool with
   `subagent_type: "Explore"` and `model: "sonnet"`). Brief it with:

   - The class name, source path, mutation score, and the list of survived
     mutants (from `mutation-summary.json`).
   - The test file path.
   - Two tasks:
     1. **Explain each survived mutant** — locate the exact line, show the
        surrounding context, and explain in plain English why no existing
        test catches the mutation.
     2. **Propose the killing assertion** — for each mutant, suggest the
        specific test method and `Should` assertion that would kill it, e.g.
        `result.Should().Be(0)` rather than just `result.Should().NotBeNull()`.
     3. **Brittleness overlay** — additionally flag any mocks that verify
        internal interactions rather than observable output (e.g. asserting
        exact call counts on collaborators when the public return value is
        what matters). This is orthogonal to mutation score.

   Ask it to return paths + line numbers + short excerpts (not whole
   files). Only fall back to direct Read/Grep for tight follow-ups on a
   specific range the Explore agent already surfaced.

5. **Build one suggested card per class with at least one finding.** Classes
   with no findings are recorded as `clean` in the summary table — do not
   file a card.

   - **Title:** `Improve tests for <ClassName>`.
   - **Tag:** `test`.
   - **Lane:** `To Do`.
   - **Body** (H3-section markdown; `### Why` and `### Acceptance` are
     required):

     ```markdown
     ### Why
     `<ClassName>` is at `<lineCoverage>%` line coverage but only `<mutationScore>%`
     mutation score — `<N>` mutant(s) survived.

     ### Survived mutants
     - `<ClassName>.cs:<line>` — `<original>` → `<replacement>` (`<mutator>`): <plain-English explanation>
       - Killing assertion: `<proposed assertion>`
     - ...

     ### Brittleness (if any)
     - <description of brittle mock / over-verification, with test method name>

     ### Acceptance
     - Re-run `mutation.ps1`; all listed mutants appear as `Killed`.
     - (One bullet per brittleness finding addressed, if any.)

     ### Related
     - `<path/to/FooTests.cs>`
     ```

   Omit `### Brittleness` when the overlay found no issues.

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

   Record this run via the no-findings path of `Findings Recording Procedure` (in `conventions`) with `--skill bish-tests` (plus `--project <name>` when a `projectName` was derived in step 1), then continue to step 9 so the clean-classes summary still prints.

8. **Interview per surviving suggestion** with `AskUserQuestion`. Before
   the interview, apply **Prior-findings recall** against
   `skill_specific.prior_findings`:

   - Match the current class against prior findings by `symbol = <ClassName>`
     and `rule = mutation-coverage`. If a prior finding matches with status
     `dismissed`: skip the interview silently, list under "previously
     dismissed: '<rebuttal_text>'" in the step 9 table, and track outcome
     `dismissed`. If status is `carded:#N` (or `linked_card_number` is set):
     skip the interview, reference card #N in the table, and track outcome
     `carded:#N`. Otherwise (`pending`, `resolved`, or no match) fall through.

   For classes that fall through, show the class name, test file path,
   mutation score, and the list of survived mutants with the Explore
   agent's explanations. Offer:

   - **Push as-is (Recommended)** — file the card with all survived mutants.
   - **Mark equivalent mutants** — review each mutant individually and
     dismiss any that are equivalent (see sub-step 8a below).
   - **Skip** — do not file this card.
   - **Edit title/body** — fall back to a free-text prompt for the
     replacement.

   When two or more suggestions clearly belong together (same module/folder),
   offer a fifth option to merge them into one card before pushing.

   **Sub-step 8a — Per-mutant equivalence review.** For each survived mutant
   in the class, show the line, mutator, original, replacement, and
   explanation, then ask: "Equivalent (dismiss) or killable (include in card)?"

   For each mutant marked **equivalent**, output the exact dismissal comment
   to add to the source file:

   > Add to `<file>`, line `<N-1>` (the line above the expression):
   > ```
   > // Stryker disable once <MutatorName>
   > ```
   > After adding this comment, re-run `mutation.ps1`. Stryker will mark the
   > mutant `Ignored` and it will not appear in future `survived[]` arrays.

   After reviewing all mutants for the class:
   - **All dismissed** → do NOT file a card; record the class as
     `all-dismissed` in the step 9 summary table.
   - **Some remain** → include only the non-dismissed mutants in
     `### Survived mutants` when filing the card.

   **Track each class** in a session log per the "Track findings during triage"
   sub-step of `Findings Recording Procedure` (in `conventions`) — one entry per
   audited class, using `title` = `Improve tests for <ClassName>`, `location` =
   the class file, `file` = the class file path (workspace-relative),
   `rule` = `mutation-coverage`, `symbol` = `<ClassName>`, and a pending
   outcome from the interview choice: **Push as-is** / **Edit** / **Merge** →
   `pending-card:<session-index>` (resolved to `carded:#<N>` after push);
   **Skip** → `parked`; **All dismissed** → `dismissed`. Emit `file`, `rule`,
   and `symbol` on every entry so the handler computes a stable identity hash
   rather than falling back to the title. Classes with no findings (recorded
   `clean`) need no entry.

   Push confirmed cards using `bishop card create` per `Card Push Procedure` (in `conventions`). Use `--tag test`.

9. **Print a summary table.** Include audited-but-clean classes and
   no-test-file skips so the user can tell audited-good from
   skipped-no-test-file from filed:

   | Class | Coverage | Mutation score | Test file | Result |
   |-------|----------|----------------|-----------|--------|
   | `Foo` | 92% | 45% | `tests/.../FooTests.cs` | card #N |
   | `Bar` | 100% | 72% | `tests/.../BarTests.cs` | clean |
   | `Baz` | 88% | 38% | _none found_ | skipped — no test file |
   | `Qux` | 90% | 35% | `tests/.../QuxTests.cs` | all-dismissed |

   Then offer:

   > Re-run `/bish-tests` after the new cards are worked. Dedupe makes
   > repeated runs safe.

10. **Record this run** by following `Findings Recording Procedure` (in
    `conventions`) with `--skill bish-tests` (plus `--project <name>` when a
    `projectName` was derived in step 1, so the run is keyed on this project
    rather than overwriting the prior project's run). Before emitting, resolve
    any `pending-card:<n>` markers in the session log to `carded:#<N>` using
    the card numbers returned by step 8's push, so every tracked class carries
    one of the three final outcomes (`carded:#<N>` / `dismissed` / `parked`).

</what-to-do>

<guardrails>

- Do NOT push cards before the user confirms each suggestion via the
  interview.
- Do NOT file a card for a class with `no test file found` — flag it in
  the summary as `skipped — no test file` and move on.
- Do NOT re-file a card whose title matches an existing open card. Re-runs
  must be idempotent.
- If the `test` tag is missing, stop and tell the user to re-run `bishop workspace init` to restore canonical tags. Never push untagged.
- Do NOT exceed 15 classes per run, even if more survive the threshold.
  Tell the user to re-run after working the cards.
- Do NOT assume a specific test framework or mocking library. Mirrored-path
  discovery uses the `<Class>Tests.cs` convention; reference-search
  fallback handles anything else.
- Do NOT invent test gaps from first principles. All mutation findings must
  come from the `survived[]` array in `mutation-summary.json`. The Explore
  step explains and proposes assertions; it does not generate new gaps.
- Do NOT file a card when all survived mutants in a class have been marked
  equivalent — record the class as `all-dismissed` in the summary table
  instead.

</guardrails>
