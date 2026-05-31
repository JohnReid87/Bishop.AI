---
name: bish-coverage
description: Run code coverage for the current .NET Bishop workspace, identify test-debt classes (low line coverage or high CRAP) and high-complexity refactor candidates, interview the user to confirm or cluster the gaps, and push agreed cards tagged `test` (test debt) or `arch` (refactor candidates). Use when the user wants to scope test work or mentions "coverage review". Assumes a .NET workspace using coverlet + ReportGenerator with a `coverage.ps1` runner at the repo root.
allowed-tools: Read, Glob, AskUserQuestion, Bash(bishop:*), Bash(pwsh:*), Bash(dotnet:*), PowerShell
bishop.scope: workspace
bishop.command: /bish-coverage
bishop.stage: false
bishop.category: tests
---

The context-pack below bundles workspace metadata, recent git history, and Bishop convention procedures (Shell selection, Card model, Findings Recording Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack coverage
```
If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.tags` — existing tag names (the skill needs a `test` tag; see step 6)
- `workspace.lanes` — lane names (defaults to `To Do` when pushing)
- `conventions` — STABLE/TUNABLE procedure sections (Shell selection, Card model, Findings Recording Procedure)
- `skill_specific.prior_findings` — outcomes from previous `bish-coverage` runs in this workspace; each entry has `identity_hash`, `project_name`, `file`, `symbol`, `rule`, `title`, `status` (`pending` / `dismissed` / `resolved` / `carded:#N`), `rebuttal_text`, and `linked_card_number`. Use during step 8 to skip findings the user has already decided on.

Echo the workspace name back on its own line:

> **Workspace:** \<name\>

---

## What this skill expects

A .NET workspace using `coverlet.collector` for instrumentation and a
`coverage.ps1` runner at the repo root that:

1. Runs `dotnet test` with the `XPlat Code Coverage` collector and a
   `coverlet.runsettings` (which defines exclusions for migrations, generated
   code, DI wiring, etc.).
2. Runs ReportGenerator to produce `TestResults/coverage-report/Summary.json`
   and `index.html` for humans.
3. Writes a normalized per-class summary at `TestResults/coverage-summary.json`
   that this skill consumes. Expected shape:

   ```json
   {
     "schemaVersion": 1,
     "threshold": 80,
     "generatedAt": "2026-05-20T01:03:26Z",
     "modules": [
       {
         "name": "<full class name>",
         "file": "<repo-relative source path, forward slashes>",
         "lineCoverage": 0.0,
         "linesCoverable": 10,
         "cyclomaticComplexity": 12,
         "crapScore": 18.4
       }
     ]
   }
   ```

   Field meanings:
   - `name` — .NET full class name. Used for card titles and as a fallback label.
   - `file` — repo-relative source file path. Used for clustering.
   - `lineCoverage` — percent line coverage, 0–100.
   - `linesCoverable` — instrumented line count. Surfaced in the interview so
     the user can judge effort, but the skill no longer filters on it — even
     a one-line handler may hide a guard, null check, or mapping worth testing.
   - `cyclomaticComplexity` — sum of per-method cyclomatic complexity for the
     class (from Cobertura `complexity`). Drives the refactor-candidate phase.
   - `crapScore` — CRAP = complexity² × (1 − lineCoverage)³ + complexity. High
     when complex code is also poorly tested. Drives test-debt ranking.

Note: example values throughout this skill use `Bishop.App.*` names because
that's the workspace where the skill was first written. The skill itself
makes no assumption about specific namespace prefixes — it works for any
.NET workspace whose `coverage.ps1` produces the schema above.

<what-to-do>

1. **Prerequisite check.** Verify the runner exists:

   - `coverage.ps1` at the repo root.

   If missing, STOP and tell the user:

   > **Coverage runner not set up.** This skill needs a `coverage.ps1` at the
   > repo root that runs `dotnet test` with coverage and writes
   > `TestResults/coverage-summary.json` in the schema documented in the
   > `/bish-coverage` skill. File a card to set one up (see Bishop.AI's
   > coverage.ps1 / coverlet.runsettings as a reference), then re-run
   > `/bish-coverage`.

2. **Run the coverage script.** Execute `pwsh ./coverage.ps1`. If it exits
   non-zero, surface its stderr and STOP. A failing test suite invalidates any
   gap list — fix the suite before scoping test work.

3. **Load the normalized summary.** Read `TestResults/coverage-summary.json`.

   If the file is missing after a successful run, STOP and tell the user the
   workspace's `coverage.ps1` is not producing the canonical summary. Do not
   attempt to fall back to ReportGenerator's `Summary.json` — it doesn't carry
   file paths and the skill needs those for clustering.

   Validate `schemaVersion == 1`; if it's a future version the skill doesn't
   recognise, STOP and ask the user to update the skill.

4. **Identify test-debt gaps.** From `modules[]`:

   - Keep any module where **`lineCoverage < summary.threshold` (default 80)
     OR `crapScore > 30`**, regardless of size. The CRAP arm catches complex
     classes that are technically above the line threshold but still under-
     tested for the risk they carry; the line arm catches trivial 0%-covered
     classes that have low CRAP. Small classes are included — a 1- or 2-line
     handler can still hide a guard, null check, or mapping worth testing,
     and the user decides per-card in the interview whether it's worth filing.
   - Sort the surviving modules by `crapScore` descending so the riskiest
     gaps surface first in the interview queue.

   If nothing survives **and** the refactor phase (step 5b) also yields no
   candidates, record this run via the no-findings path of `Findings Recording Procedure` (in `conventions`) with `--skill bish-coverage`,
   then congratulate the user — coverage is at or above the threshold and no
   classes exceed the complexity ceiling — and STOP without pushing anything.

5. **Cluster test-debt gaps by source-file directory.** Use the `file` field's
   parent directory as the cluster key. Classes sharing the same parent folder
   become one suggested card; classes with no folder peers stay singleton.
   Within each cluster, sort classes by `crapScore` descending; rank clusters
   by their highest member CRAP so the worst clusters come first in step 8.

   Illustrative examples (using Bishop.AI's layout — your project's paths
   will look different):

   - `src/Bishop.App/Tags/AddTag/AddTagCommandHandler.cs` and
     `src/Bishop.App/Tags/RemoveTag/RemoveTagCommandHandler.cs` both under
     threshold → cluster key `src/Bishop.App/Tags` (longest shared prefix
     when all peers sit in subfolders of one parent).
   - `src/Bishop.App/Terminal/TerminalLauncher.cs` with no peers under
     threshold → singleton card for that file.

   The skill never invents cluster boundaries beyond folders — it does not
   second-guess the project's directory structure.

   Build one suggested card per cluster:

   - **Title:** `Add tests for <cluster-folder-or-class-name>`. For singletons
     use the class name (e.g. `Add tests for TerminalLauncher`); for clusters
     use the folder (e.g. `Add tests for src/Bishop.App/Tags`).
   - **Body** (use the H3-section template; `### Why` and `### Acceptance` are required):

     ```markdown
     ### Why
     `<cluster>` is under-tested for its risk: worst class `<name>` at `<coverage>%` line coverage, cyclomatic complexity `<cyclomatic>`, CRAP `<crap>` (gate: lineCoverage < `<threshold>` OR crapScore > 30).

     ### Classes
     Add unit tests exercising public methods (including null/edge inputs and error paths) for the classes below (sorted by CRAP descending).
     - `<ClassName1>` (cov <coverage1>%, cyclomatic <cyclomatic1>, CRAP <crap1>, `<file1>`)
     - `<ClassName2>` (cov <coverage2>%, cyclomatic <cyclomatic2>, CRAP <crap2>, `<file2>`)

     ### Acceptance
     - All listed classes report `lineCoverage ≥ <threshold>` AND `crapScore ≤ 30` in the next coverage run.

     ### Related
     - `<file1>`
     - `<file2>`
     - Open `TestResults/coverage-report/index.html` and drill into each class to find uncovered methods and line ranges.
     ```
   - **Tag:** `test`.
   - **Lane:** `To Do`.

5b. **Identify refactor candidates.** From the full `modules[]` (not just the
    test-debt set), keep any module with `cyclomaticComplexity > 30`,
    regardless of coverage. A 100%-covered switch-heavy parser is still a
    refactor candidate — high complexity is a maintainability signal in its
    own right. Sort by `cyclomaticComplexity` descending. **Do not cluster**
    refactor candidates by folder — refactoring is a per-class judgement, not
    a directory sweep. Build one suggested card per surviving class:

    - **Title:** `Refactor <ClassName> (cyclomatic <N>)`.
    - **Body**:

      ```markdown
      ### Why
      `<ClassName>` has cyclomatic complexity `<cyclomatic>` (threshold: 30), CRAP `<crap>`, line coverage `<coverage>%`. High complexity hurts readability and increases the surface area for regressions even when tests pass today.

      ### Candidate
      - `<ClassName>` (`<file>`)

      ### Acceptance
      - `<ClassName>` reports `cyclomaticComplexity ≤ 30` in the next coverage run, or the work is consciously deferred with a note on the card.

      ### Related
      - `<file>`
      - Open `TestResults/coverage-report/index.html` for the per-method complexity breakdown.
      ```
    - **Tag:** `arch`.
    - **Lane:** `To Do`.

6. **Ensure the `test` and `arch` tags exist.** Check `tags[].name`. If either
   is absent, stop and tell the user: the canonical tags are seeded by
   `bishop workspace init` — re-running it will restore any missing tags.

7. **Dedupe against existing cards.** Run `bishop card list --json`. Drop
   any suggestion (test-debt or refactor) whose title matches an existing card
   UNLESS that card is in the `Done` lane. Done cards represent shipped work —
   if the gap has re-opened, surface it again.

   If every suggestion from both phases is filtered out, tell the user:

   > All under-covered and over-complex areas already have open cards on the
   > board. Nothing new to file.

   Record this run via the no-findings path of `Findings Recording Procedure` (in `conventions`) with `--skill bish-coverage`, then STOP.

8. **Interview per surviving suggestion** with `AskUserQuestion`, processing
   the **test-debt phase first** (clusters ordered by highest member CRAP
   descending), then the **refactor phase** (classes ordered by
   `cyclomaticComplexity` descending). Before each interview, apply
   **Prior-findings recall** against `skill_specific.prior_findings`:

   - Match the current suggestion against prior findings by `title`. If a
     prior finding matches with status `dismissed`: skip the interview
     silently, list under "previously dismissed: '<rebuttal_text>'" in the
     step 10 table, and track outcome `dismissed`. If status is `carded:#N`
     (or `linked_card_number` is set): skip the interview, reference card #N
     in the table, and track outcome `carded:#N`. Otherwise fall through to
     the interview below.

   For test-debt clusters that fall through, show the cluster name, then each
   class with its coverage, cyclomatic complexity, CRAP score, and source file
   (e.g. `ClassName (cov 42%, cyclomatic 18, CRAP 47, src/Foo/Bar.cs)`).

   For refactor candidates that fall through, show the class name with its
   cyclomatic complexity, CRAP score, and source file (e.g.
   `ClassName (cyclomatic 38, CRAP 12, src/Foo/Bar.cs)`) and frame it as a
   "refactor candidate — high complexity regardless of coverage".

   Offer for both phases:

   - **Push as-is (Recommended)** — file the card with the proposed title/body.
   - **Skip** — do not file this card.
   - **Edit title/body** — fall back to a free-text prompt for the replacement.

   For the test-debt phase only, when two or more cluster suggestions clearly
   belong together (same top-level folder, same theme), offer a fourth option
   to merge them into one card before pushing. Refactor candidates are never
   merged — each class is its own decision.

   **Track each suggestion** in a session log per the "Track findings during
   triage" sub-step of `Findings Recording Procedure` (in `conventions`) — one
   entry per suggestion, using the suggested card title and the source file(s)
   as `location`, with a pending outcome from the choice: **Push as-is** /
   **Edit** / **Merge** → `pending-card:<session-index>` (resolved to
   `carded:#<N>` after step 9 pushes); **Skip** → `parked`.

9. **Push confirmed cards** using `bishop card create` per `Card Push Procedure`
   (in `conventions`). Use `--tag test` for test-debt cards and `--tag arch`
   for refactor-candidate cards.

10. **Print a summary table** after all cards are pushed, grouped by phase:

    Test debt:

    | Card | Title | Worst coverage | Worst CRAP | Classes |
    |------|-------|----------------|------------|---------|
    | #N | ... | NN% → ≥80% | NN.N → ≤30 | n classes |

    Refactor candidates:

    | Card | Title | Cyclomatic | CRAP |
    |------|-------|------------|------|
    | #N | Refactor `<ClassName>` | NN → ≤30 | NN.N |

    Then offer:

    > Re-run `/bish-coverage` after the new cards are worked, then
    > `/bish-tests` to audit the test files of well-covered classes.
    > Dedupe makes repeated runs safe.

11. **Record this run** by following `Findings Recording Procedure` (in
    `conventions`) with `--skill bish-coverage`. Before emitting, resolve any
    `pending-card:<n>` markers in the session log to `carded:#<N>` using the
    card numbers returned by step 9, so every tracked cluster carries one of
    the three final outcomes (`carded:#<N>` / `dismissed` / `parked`).

</what-to-do>

<guardrails>

- Do NOT push cards before the user confirms each suggestion via the interview.
- Do NOT lower the line-coverage threshold or raise the CRAP/cyclomatic
  thresholds to surface more or fewer gaps. The interview is the signal
  filter — class size is not, since small classes can still contain logic.
- Do NOT parse `Summary.json` (ReportGenerator) or `*.cobertura.xml` directly.
  The skill consumes `TestResults/coverage-summary.json` only. .NET-specific
  format conversion belongs in the workspace's `coverage.ps1`, not here.
- Do NOT re-file a card whose title matches an existing open card. Re-runs
  must be idempotent across both phases.
- Do NOT cluster refactor candidates by folder. Each high-complexity class is
  its own per-class judgement.
- Do NOT cross-tag: test-debt cards are `test`, refactor-candidate cards are
  `arch`. A class that appears in both phases generates two separate cards.
- If `coverage.ps1` fails, surface its stderr and STOP. Do not try to recover
  or parse a partial summary.
- If the `test` or `arch` tag is missing, stop and tell the user to re-run `bishop workspace init` to restore canonical tags. Never push untagged.
- Do NOT assume specific namespace prefixes (`Bishop.*`, `MyApp.*`, etc.).
  The skill clusters by directory, not by namespace pattern.

</guardrails>
