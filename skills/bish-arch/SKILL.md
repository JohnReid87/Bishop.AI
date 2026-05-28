---
name: bish-arch
description: Architectural and code-quality review of any .NET solution registered as the current Bishop workspace. Detects the stack (WPF, ASP.NET Core, Blazor, MAUI, console, worker, class library) and applies universal heuristics (SOLID, .NET idioms, DI lifetimes, layer hygiene, public API surface, test seams) plus stack-conditional ones (EF Core, MediatR, MVVM, etc. — only when relevant). Walks findings with the user one at a time and pushes agreed cards tagged `arch`. Use when the user mentions "architecture review", "arch review", "SOLID check", or invokes `/bish-arch`.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-arch
bishop.stage: false
bishop.category: review
firstRunModel: claude-opus-4-7
reRunModel: claude-sonnet-4-6
---

> Recommended model: Opus 4.7 — architectural critique requires sustained multi-step judgement.

The context-pack below bundles workspace metadata, recent git history, and Bishop convention procedures (Shell selection, Card model, Skill-Run Recording Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack arch
```
If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.path` — repo root used by the discovery subagent
- `workspace.tags` — existing tag names (the skill needs an `arch` tag; see step 5)
- `workspace.lanes` — lane names (`To Do` is the push target; see step 8)
- `conventions` — STABLE/TUNABLE procedure sections (Shell selection, Card model, Skill-Run Recording Procedure)

Echo the workspace name on its own line:

> **Workspace:** \<name\>

---

## What this skill does

Reviews architectural and code-quality health of the .NET solution at the
current Bishop workspace's path. A single Explore subagent first detects the
stack in use, then applies universal dimensions plus stack-conditional ones,
producing a ranked list of findings. The user triages findings one at a time,
clustering related ones into single cards as they go. Confirmed cards are
pushed tagged `arch` to the `To Do` lane.

The skill always reviews the whole solution. It does not accept a scoping
argument — if the user wants something narrower, they can say so during
triage and dismiss out-of-scope findings.

**Project agnostic, .NET only.** The skill works on any .NET solution
registered as a Bishop workspace — WPF, ASP.NET Core, Blazor, MAUI, console,
worker, or class library. It adapts the applied dimensions to what's in the
solution (detected from `*.csproj` SDK and package references). Example paths
or names anywhere in this skill are illustrative; no Bishop.AI-specific
layout, namespace prefix, or package set is assumed.

## Review dimensions

The discovery subagent must first detect the stack (see step 2) and apply
only the dimensions relevant to what's actually in the solution. Each finding
must cite at least one `file:line` location.

### Universal — apply to any .NET solution

- **SOLID** — SRP (classes doing too much; god-handlers), OCP (`switch` over
  types begging for polymorphism), LSP (derived classes violating base
  contracts), ISP (fat interfaces with unrelated members), DIP (depending on
  concretions where an abstraction belongs).
- **.NET idioms** — `async void`, missing `ConfigureAwait` in non-UI code,
  sync-over-async, `IDisposable` patterns and `using` discipline,
  nullable-reference-type annotation drift, exception swallowing,
  allocation-heavy hot paths.
- **DI lifetimes** — captive dependencies (singleton holding scoped or
  transient), scoped-service ownership, `IDisposable` services registered
  as singleton, ambient/static state replacing DI.
- **Layer hygiene** — dependency direction (Core/Domain has zero outward
  refs; application layer doesn't leak infrastructure types upward;
  infrastructure doesn't leak into Core).
- **Public API surface** — types/members declared `public` that should be
  `internal`; over-exposed mutation surfaces; leaked domain/infra types
  across assembly boundaries.
- **Test seams** — testability of core paths; whether tests hit real
  infrastructure (DB, filesystem, network) or fakes; missing seams around
  static helpers, singletons, and `DateTime.Now`-style ambient state.
- **Justified abstractions** — interfaces or base classes with exactly one
  production implementation and no test-seam justification (i.e. the interface
  is never used to inject a fake or stub). Suggested action: delete the
  interface and inline the concrete type. `complexity_delta: removes` makes
  this a legitimate fix, not a regression.
- **Intent encapsulation** — (i) inline blocks of 3+ lines performing one
  nameable concept inside a larger method, and (ii) repeated 3-line chunks
  within one class. Suggested action: extract a private named method in the
  same class. Applies only when the extracted name would be clearer than the
  existing code — do not flag trivially readable one-purpose methods.

### Stack-conditional — apply only when the SDK / package is detected

- **EF Core** *(if `Microsoft.EntityFrameworkCore.*` referenced)* — N+1 query
  patterns, tracking vs no-tracking misuse, `Include` chain drift,
  `DbContext` lifetime, raw SQL leakage out of repositories/handlers,
  transaction boundaries, migrations hygiene.
- **Dapper / raw ADO.NET** *(if those packages referenced)* — connection
  lifetime, parameter binding hygiene, command/connection disposal, SQL
  duplication, mapping leakage.
- **MediatR / in-process mediator** *(if `MediatR` or similar referenced)*
  — handler granularity (per feature vs god-handlers), missing validators
  on mutating commands, unused pipeline behaviours, `ISender` vs `IMediator`
  in callers that only dispatch.
- **ASP.NET Core** *(if `Microsoft.AspNetCore.*` or SDK is
  `Microsoft.NET.Sdk.Web`)* — controller bloat, action result discipline
  (`IActionResult` vs typed results), model-binding validation, filter
  usage, Minimal-API endpoint grouping, options pattern usage, authorization
  attribute placement, exception filters / problem details.
- **Blazor** *(if `Microsoft.AspNetCore.Components.*` referenced or SDK is
  `Microsoft.NET.Sdk.BlazorWebAssembly`)* — component lifecycle misuse,
  render-mode mismatches, parameter validation, cascading-parameter overuse,
  `@inject` for transient services, JS interop leakage.
- **WPF / WinForms / WinUI** *(if `UseWPF` / `UseWindowsForms` true or
  `Microsoft.WindowsAppSDK` referenced)* — MVVM hygiene, code-behind
  leakage, ViewModels depending on UI types, x:Bind/Binding misuse, missing
  or wrong `INotifyPropertyChanged`, command surface, message-box prompts
  inside VMs.
- **MAUI / Avalonia** *(if `Microsoft.Maui.*` or `Avalonia.*` referenced)*
  — platform-specific code leakage out of platform partials, MVVM hygiene,
  view-model lifetime, navigation patterns.
- **Worker / hosted services** *(if `Microsoft.Extensions.Hosting` is the
  host model)* — `IHostedService` / `BackgroundService` lifetime semantics,
  graceful shutdown, cancellation token propagation, unobserved task
  exceptions.
- **Class libraries** *(if SDK is `Microsoft.NET.Sdk` with no executable
  output)* — strict public API surface review, dependency footprint,
  target-framework breadth, semver-relevant change risk.

The subagent may add stack-specific dimensions it derives from the project
that aren't in this list — the headings above are the floor, not the ceiling.

<what-to-do>

1. **Workspace detection** (above).

2. **Discovery phase.** Spawn one Explore subagent (via `Agent` with
   `subagent_type: "Explore"` and `model: "haiku"`) with a brief that:

   - Names the repo root from `workspacePath`.
   - **First sub-step: detect the stack.** Read the `*.sln` to enumerate
     projects, then for each `*.csproj` capture the `Sdk` attribute,
     `<TargetFramework[s]>`, `UseWPF` / `UseWindowsForms` properties, and
     `PackageReference`s. Report the detected stack on its own line before
     producing any findings (e.g. "Detected: WPF + EF Core + MediatR +
     xUnit" or "Detected: ASP.NET Core Web API + Dapper + Serilog").
   - Applies the **Universal** dimensions to every project, and the
     **Stack-conditional** dimensions only to projects where the relevant
     SDK / package is present. If neither EF Core nor Dapper nor any other
     data-access library is referenced, don't surface data-access findings;
     if no UI framework is in the solution, don't surface MVVM findings;
     and so on.
   - Asks for **at most 15 findings**, ranked by severity. Bias toward
     high/medium; low-severity items may be mentioned briefly under
     "also noticed" but should not be expanded.
   - Requires each finding to include: `severity` (high/med/low), `dimension`
     (one of the labels above, or a stack-derived dimension the subagent
     introduces with justification), `location` (file:line, may be multiple),
     `what` (1 sentence describing the issue), `why_it_matters` (consequence
     in this codebase, not a textbook quote), `suggested_action` (concrete
     change), `fix_cost` (low/med/high — effort to apply the suggested action),
     `complexity_delta` (adds/neutral/removes — net effect on codebase
     structural complexity after the fix). Reject any subagent output that
     omits `fix_cost` or `complexity_delta` from a finding.
   - Returns findings as a numbered list, severity-ordered (high first).

   If the subagent returns more than 15 findings, ask it to re-rank and trim
   to 15. If it returns fewer than 3, that's fine — surface what it found.

   If the subagent reports no findings (all applicable dimensions clean),
   record this run with an empty findings array — same call shape as
   step 10, just `"findings": []` — then congratulate the user and STOP
   without pushing anything.

3. **Echo summary.** Print a one-line overview the user can scan before triage.
   Format (paths shown are illustrative only — use whatever the subagent
   surfaced):

   ```
   #1 [high] [fix:med]  SOLID/SRP     — <project>/<...>/X.cs:42         — <one-line>
   #2 [med]  [fix:low]  MediatR       — <project>/<...>/Y.cs:55         — <one-line>
   #3 [med]  [fix:high] Layer hygiene — <project>/<...>/Z.cs:18,33      — <one-line>
   ...
   ```

4. **Triage loop.** Walk findings in severity order. For each finding:

   - Print the full body: location(s), what, why-it-matters, suggested-action,
     plus your own recommended verdict. The verdict must weigh `fix_cost` and
     `complexity_delta` explicitly — for example: "Recommended: card it — clear
     DIP violation, fix is low-cost and removes a layer" or "Recommended:
     dismiss — high fix cost and adds a layer; only worth it if a second caller
     appears". Use the pattern `<fix_cost> fix cost + <complexity_delta>
     complexity → <card it / dismiss>` when the tradeoff is non-obvious.

   - Use `AskUserQuestion` with these options (recommended one first, suffixed
     " (Recommended)"):

     - **Card it (new)** — file this finding as a new card. Default body uses
       the **Card body template** below.
     - **Cluster with #N: \<title\>** — fold this finding into a card already
       agreed earlier in the session. Append its location and a one-line
       summary to that card's body. Only offer when at least one prior
       in-session card exists.
     - **Dismiss — context** — user explains why this isn't an issue. Capture
       the reason verbatim in the session log so a repeat run can show it
       again with the user's own rebuttal as a hint.
     - **Defer** — note it but don't card it now.

   - **Track the finding** in a session log (in memory). For each finding
     captured during the walk, store:

     - `title` — the finding's one-sentence headline
     - `body` — multi-line description: location(s), what, why-it-matters,
       suggested-action
     - `severity` — the subagent's `high` / `med` / `low`
     - `location` — the finding's `file:line` (or comma-separated locations)
     - **pending outcome** — derived from the user's choice:
       - **Card it (new)** → `pending-card:<session-index>` (resolved to
         `carded:#<N>` after step 8 pushes the card)
       - **Cluster with #N** → reuse the same `pending-card:<session-index>`
         that was assigned to the card it was clustered into
       - **Dismiss — context** → `dismissed`
       - **Defer** → `parked`

     This log is the input to step 10's `bishop findings record` call. Every
     finding the subagent surfaced must appear in it, each with one of the
     three final outcomes (`carded:#<N>` / `dismissed` / `parked`).

5. **Ensure the `arch` tag exists.** If `tags[].name` doesn't include `arch`,
   stop and tell the user: the canonical tags are seeded by `bishop workspace init` — re-running it will restore any missing tags.

6. **Granularity pass.** Before printing the task list, re-read the agreed
   cards:

   - Merge cards that share the same dimension AND touch the same module if
     they got separated during triage.
   - Split anything that grew beyond one PR's worth of work into two cards
     with independent acceptance criteria.
   - One card ≈ one PR. If a card is a one-line change, fold it into the
     nearest related card.

7. **Print task list** for review in the shape defined by `Task List Preview Format` (in `conventions`).

   Then ask:

   > "Please review the tasks above. Say **push** to create the Bishop cards."

8. **Push confirmed cards** in order using `bishop card add` per
   `Card Push Procedure` (in `conventions`). Use `--tag arch`.

9. **Print summary table:**

   | Card | Title | Lane | Severity |
   |------|-------|------|----------|
   | #N | ... | To Do | high |

   Then offer:

   > Re-run `/bish-arch` after these are worked. Dismissed findings will
   > resurface — explain the same way or capture the rebuttal in a project
   > memory if it's load-bearing.

10. **Record this run.** Resolve any `pending-card:<n>` markers in the session
    log to `carded:#<N>` using the card numbers returned by step 8, so every
    tracked finding carries one of the three final outcomes (`carded:#<N>` /
    `dismissed` / `parked`).

    Then capture HEAD and emit the findings via the Bash tool (single-quoted
    heredoc — the quotes around the marker prevent shell expansion inside the
    JSON, so `$` and backticks in finding bodies are passed through unchanged):

    ```bash
    # First: capture the current SHA.
    git rev-parse HEAD

    # Then: emit findings, substituting the literal SHA value (no $VAR
    # expansion — see `Shell selection` in conventions).
    bishop findings record --skill bish-arch --sha <captured-sha> --file - <<'JSON'
    {
      "findings": [
        {
          "title": "<short title>",
          "body": "<full body: locations, what, why-it-matters, suggested-action>",
          "outcome": "carded:#<N>",
          "severity": "high",
          "location": "<file:line[, file:line]>"
        }
      ]
    }
    JSON
    ```

    See `## Findings JSON schema` below for the full field rules.

    A successful invocation prints `Recorded N finding(s) for 'bish-arch'`
    plus the JSON / HTML paths under `.bishop/findings/`. On non-zero exit,
    surface the error to the user but do not abort — the review is complete
    whether or not the record write succeeds.

</what-to-do>

## Card body template

Use the H3-section markdown template below. Required sections: `### Why` and `### Acceptance`. Include optional sections only when they add value.

```markdown
### Why
<dimension> — <what's wrong, 1 sentence>. Locations: `<file:line[, file:line]...>`.

### Changes
- `<file:line>` — <suggested change>

### Acceptance
- <how we'll know it's done>

### Related
<for clustered cards: list each additional finding as `<file:line> — <one-line summary>`>
```

Rules:
- Backtick file paths, identifiers, and CLI commands.
- Use bullets in `### Changes` and `### Acceptance`.
- Omit `### Related` for single-finding cards.
- Omit `### Changes` when the suggested action is already clear from `### Why`.

## Findings JSON schema

Reference for future skill authors copying this pattern. The `bishop findings
record --file -` command reads JSON of the shape below; the validator
(`Bishop.App.Findings.FindingsValidator`) rejects malformed input.

```json
{
  "findings": [
    {
      "title": "SRP violation — UserHandler does auth + validation + persistence",
      "body": "Locations: src/Services/UserHandler.cs:42, src/Services/UserHandler.cs:88\n\nThe class handles three unrelated responsibilities. Why it matters: any change touches the same type, making churn unsafe. Suggested action: split into AuthService, ValidationService, and UserRepository.",
      "outcome": "carded:#704",
      "severity": "high",
      "location": "src/Services/UserHandler.cs:42,88"
    },
    {
      "title": "Missing ConfigureAwait in non-UI handler",
      "body": "src/Handlers/QueryHandler.cs:55 — awaits without ConfigureAwait(false). Negligible in this codebase because there is no SynchronizationContext on the host.",
      "outcome": "dismissed",
      "severity": "low",
      "location": "src/Handlers/QueryHandler.cs:55"
    },
    {
      "title": "Public type that should be internal",
      "body": "src/Core/InternalHelper.cs:14 — `public` but only used within the assembly. Tightening to `internal` reduces the public surface.",
      "outcome": "parked",
      "severity": "med",
      "location": "src/Core/InternalHelper.cs:14"
    }
  ]
}
```

Field rules:

- `title` (required) — non-empty string. One-sentence finding headline.
- `body` (required) — non-empty string. Use `\n` for newlines. Should
  contain location(s), what's wrong, why it matters, and the suggested
  action.
- `outcome` (required) — exactly one of:
  - `dismissed` — user explained why this isn't an issue
  - `parked` — user wants to revisit later
  - `carded:#<n>` — became a Bishop card (the literal `#` is required;
    `<n>` is the workspace-scoped card Number)
- `severity` (optional) — `high` / `med` / `low` / `critical`. Drives
  the HTML chip colour. `null` or absent is allowed.
- `location` (optional) — `file:line` or comma-separated locations for
  findings spanning multiple sites.

An empty `findings: []` array is valid and is the right shape for the
"no findings surfaced" run path.

<guardrails>

- Do NOT push cards before the user confirms each finding via triage AND says
  "push" at the task list stage.
- Do NOT propose findings without `file:line` citations — every claim must be
  locatable in the code. Reject subagent output that omits locations.
- Every finding must carry `fix_cost` (low/med/high) and `complexity_delta`
  (adds/neutral/removes). Reject subagent output that omits either field on any
  finding — ask the subagent to add the missing fields before continuing triage.
- `complexity_delta: removes` findings are valid and desirable. Deletion of an
  unjustified abstraction is a legitimate suggested action, not a regression.
- Do NOT include dismissed or deferred findings in the pushed cards.
- All `arch` cards land in `To Do`. Do not prompt the user for an alternative
  lane during triage — re-prioritisation happens on the board after the push.
- Do NOT produce a Markdown report or any companion file. The cards on the
  board are the durable output.
- Always pass `--bottom` to `bishop card add`. Arch reviews are bulk pushes
  by nature — they must not jump ahead of manually prioritised work.
- Do NOT assume specific namespace prefixes (`Bishop.*`, `MyApp.*`). Work from
  project layout, not naming conventions.
- Do NOT apply stack-conditional dimensions to projects where the relevant
  SDK / package isn't referenced. If MediatR isn't in the solution, no MediatR
  findings. If WPF isn't in play, no MVVM findings. The stack detection in
  step 2 is the gate.
- Do NOT default to "WPF + EF Core + MediatR" just because that's what the
  skill was originally written against. Always re-detect.
- If the discovery subagent returns >15 findings, ask it to trim — don't
  silently truncate.
- If a finding spans multiple files but reflects the same underlying issue
  (e.g. "every VM injects IMediator instead of ISender"), present it as ONE
  finding with multiple `file:line` locations, not one finding per file.

</guardrails>
