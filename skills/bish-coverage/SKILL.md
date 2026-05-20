---
name: bish-coverage
description: Run code coverage for the current .NET Bishop workspace, identify classes below 80% line coverage, interview the user to confirm or cluster the gaps, and push the agreed cards to the board tagged `test`. Use when the user wants to scope test work or mentions "coverage review". Assumes a .NET workspace using coverlet + ReportGenerator with a `coverage.ps1` runner at the repo root.
allowed-tools: Read, Glob, AskUserQuestion, Bash(bishop:*), Bash(pwsh:*), Bash(dotnet:*), PowerShell
bishop.scope: workspace
bishop.command: /bish-coverage
bishop.stage: false
---

**Before anything else — detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP immediately and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available workspaces,
  > then `cd` into one of the listed paths and retry.

- If it succeeds, parse the JSON and extract:
  - `name` — workspace name (echoed back as confirmation)
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
   - **Body** (use literal `\n` for paragraph breaks):
     `<cluster> is below the <threshold>% line-coverage threshold (worst class: <name> at <coverage>%).\nAcceptance: all listed classes report lineCoverage ≥ <threshold> in the next coverage run.\nClasses in scope: <comma-separated class names>.`
   - **Tag:** `test`.
   - **Lane:** `To Do`.

6. **Ensure the `test` tag exists.** Check `tags[].name`. If `test` is absent,
   run `bishop tag add test` once before pushing any cards. (`tag add` is
   idempotent for an existing name, so re-running is safe.)

7. **Dedupe against existing cards.** Run `bishop card list --json`. Drop
   any suggestion whose title matches an existing card UNLESS that card is in
   the `Done` lane. Done cards represent shipped work — if the gap has
   re-opened, surface it again.

   If every suggestion is filtered out, tell the user:

   > All under-covered areas already have open cards on the board. Nothing new
   > to file.

   Then STOP.

8. **Interview per surviving suggestion** with `AskUserQuestion`. For each one,
   show the cluster, the worst class and its coverage, and offer:

   - **Push as-is (Recommended)** — file the card with the proposed title/body.
   - **Skip** — do not file this card.
   - **Edit title/body** — fall back to a free-text prompt for the replacement.

   When two or more suggestions clearly belong together (same top-level folder,
   same theme), offer a fourth option to merge them into one card before pushing.

9. **Push confirmed cards** via:

   ```
   bishop card add --lane "To Do" --title "<Title>" --tag test --description "<body with \n replaced by real newlines>"
   ```

   If the body contains double-quotes (rare — file paths and class names don't),
   pipe it via stdin with `--description-file -` instead.

10. **Print a summary table** after all cards are pushed:

    | Card | Title | Coverage gap | Classes |
    |------|-------|--------------|---------|
    | #N | ... | NN% → ≥80% | n classes |

    Then offer:

    > Re-run `/bish-coverage` after the new cards are worked. Dedupe makes
    > repeated runs safe.

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
- If the `test` tag is missing, create it once via `bishop tag add test` before
  pushing. Never push untagged.
- Do NOT assume specific namespace prefixes (`Bishop.*`, `MyApp.*`, etc.).
  The skill clusters by directory, not by namespace pattern.

</guardrails>
