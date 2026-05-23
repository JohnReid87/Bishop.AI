---
name: bish-coverage
description: Run code coverage for the current .NET Bishop workspace, identify classes below 80% line coverage, interview the user to confirm or cluster the gaps, and push the agreed cards to the board tagged `test`. Use when the user wants to scope test work or mentions "coverage review". Assumes a .NET workspace using coverlet + ReportGenerator with a `coverage.ps1` runner at the repo root.
allowed-tools: Read, Glob, AskUserQuestion, Bash(bishop:*), Bash(pwsh:*), Bash(dotnet:*), PowerShell
bishop.scope: workspace
bishop.command: /bish-coverage
bishop.stage: false
bishop.category: review
---

> Recommended model: Sonnet 4.6 — procedure-following; extended reasoning not required.

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
         "linesCoverable": 10
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

4. **Identify gaps.** From `modules[]`:

   - Keep any module with `lineCoverage < summary.threshold` (default 80),
     regardless of size. Small classes are included — a 1- or 2-line handler
     can still hide a guard, null check, or mapping worth testing, and the
     user decides per-card in the interview whether it's worth filing.

   If nothing survives, congratulate the user — coverage is at or above the
   threshold — and STOP without pushing anything.

5. **Cluster gaps by source-file directory.** Use the `file` field's parent
   directory as the cluster key. Classes sharing the same parent folder become
   one suggested card; classes with no folder peers stay singleton.

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
     `<cluster>` is below the `<threshold>%` line-coverage threshold (worst class: `<name>` at `<coverage>%`).

     ### Classes
     Add unit tests exercising public methods (including null/edge inputs and error paths) for the classes below.
     - `<ClassName1>` (<coverage1>%, `<file1>`)
     - `<ClassName2>` (<coverage2>%, `<file2>`)

     ### Acceptance
     - `<ClassName1>` reports `lineCoverage ≥ <threshold>` in the next coverage run.
     - `<ClassName2>` reports `lineCoverage ≥ <threshold>` in the next coverage run.
     - All listed classes report `lineCoverage ≥ <threshold>` in the next coverage run.

     ### Related
     - `<file1>`
     - `<file2>`
     - Open `TestResults/coverage-report/index.html` and drill into each class to find uncovered methods and line ranges.
     ```
   - **Tag:** `test`.
   - **Lane:** `To Do`.

6. **Ensure the `test` tag exists.** Check `tags[].name`. If `test` is absent,
   stop and tell the user: the canonical tags are seeded by `bishop workspace init` — re-running it will restore any missing tags.

7. **Dedupe against existing cards.** Run `bishop card list --json`. Drop
   any suggestion whose title matches an existing card UNLESS that card is in
   the `Done` lane. Done cards represent shipped work — if the gap has
   re-opened, surface it again.

   If every suggestion is filtered out, tell the user:

   > All under-covered areas already have open cards on the board. Nothing new
   > to file.

   Then STOP.

8. **Interview per surviving suggestion** with `AskUserQuestion`. For each one,
   show the cluster name, then each class in the cluster with its coverage
   percentage and source file path (e.g. `ClassName (42%, src/Foo/Bar.cs)`), and offer:

   - **Push as-is (Recommended)** — file the card with the proposed title/body.
   - **Skip** — do not file this card.
   - **Edit title/body** — fall back to a free-text prompt for the replacement.

   When two or more suggestions clearly belong together (same top-level folder,
   same theme), offer a fourth option to merge them into one card before pushing.

9. **Push confirmed cards.** For each, call `bishop card add`
   (see [bishop context print --section "Card model"](.bishop/BISHOP_CONTEXT.md#card-model)) with:

   - `--lane "To Do"`
   - `--title "<Title>"`
   - `--tag test`
   - `--description-file -` piping the body shape from step 5
   - `--bottom` — bulk pushes must not jump ahead of manually prioritised work

10. **Print a summary table** after all cards are pushed:

    | Card | Title | Coverage gap | Classes |
    |------|-------|--------------|---------|
    | #N | ... | NN% → ≥80% | n classes |

    Then offer:

    > Re-run `/bish-coverage` after the new cards are worked, then
    > `/bish-tests` to audit the test files of well-covered classes.
    > Dedupe makes repeated runs safe.

</what-to-do>

<guardrails>

- Do NOT push cards before the user confirms each suggestion via the interview.
- Do NOT lower the threshold to surface more gaps. The interview is the signal
  filter — class size is not, since small classes can still contain logic.
- Do NOT parse `Summary.json` (ReportGenerator) or `*.cobertura.xml` directly.
  The skill consumes `TestResults/coverage-summary.json` only. .NET-specific
  format conversion belongs in the workspace's `coverage.ps1`, not here.
- Do NOT re-file a card whose title matches an existing open card. Re-runs
  must be idempotent.
- If `coverage.ps1` fails, surface its stderr and STOP. Do not try to recover
  or parse a partial summary.
- If the `test` tag is missing, stop and tell the user to re-run `bishop workspace init` to restore canonical tags. Never push untagged.
- Do NOT assume specific namespace prefixes (`Bishop.*`, `MyApp.*`, etc.).
  The skill clusters by directory, not by namespace pattern.

</guardrails>
