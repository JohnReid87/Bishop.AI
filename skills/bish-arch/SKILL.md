---
name: bish-arch
description: Architectural and code-quality review of any .NET solution registered as the current Bishop workspace. Detects the stack (WPF, ASP.NET Core, Blazor, MAUI, console, worker, class library) and applies universal heuristics (SOLID, .NET idioms, DI lifetimes, layer hygiene, public API surface, test seams) plus stack-conditional ones (EF Core, MediatR, MVVM, etc. — only when relevant). Walks findings with the user one at a time and pushes agreed cards tagged `arch`. Use when the user mentions "architecture review", "arch review", "SOLID check", or invokes `/bish-arch`.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-arch
bishop.stage: false
---

**Before anything else — detect the active Bishop workspace:**

Run `bishop workspace current --json`.

- If the command exits non-zero or produces no output, STOP immediately and tell the user:

  > **Not in a Bishop workspace.** Run `bishop workspace list` to see available workspaces,
  > then `cd` into one of the listed paths and retry.

- If it succeeds, parse the JSON and extract:
  - `name` — workspace name (echoed back as confirmation)
  - `path` — repo root used by the discovery subagent
  - `tags[].name` — existing tag names (the skill needs an `arch` tag; see step 5)
  - `lanes[].name` — lane names (defaults to `Ideas` when pushing)

Echo the workspace name on its own line:

> **Workspace:** \<name\>

---

## What this skill does

Reviews architectural and code-quality health of the .NET solution at the
current Bishop workspace's path. A single Explore subagent first detects the
stack in use, then applies universal dimensions plus stack-conditional ones,
producing a ranked list of findings. The user triages findings one at a time,
clustering related ones into single cards as they go. Confirmed cards are
pushed tagged `arch` to the `Ideas` lane.

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
   `subagent_type: "Explore"`) with a brief that:

   - Names the repo root from `path`.
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
     change).
   - Returns findings as a numbered list, severity-ordered (high first).

   If the subagent returns more than 15 findings, ask it to re-rank and trim
   to 15. If it returns fewer than 3, that's fine — surface what it found.

   If the subagent reports no findings (all applicable dimensions clean),
   congratulate the user and STOP without pushing anything.

3. **Echo summary.** Print a one-line overview the user can scan before triage.
   Format (paths shown are illustrative only — use whatever the subagent
   surfaced):

   ```
   #1 [high] SOLID/SRP     — <project>/<...>/X.cs:42         — <one-line>
   #2 [med]  MediatR       — <project>/<...>/Y.cs:55         — <one-line>
   #3 [med]  Layer hygiene — <project>/<...>/Z.cs:18,33      — <one-line>
   ...
   ```

4. **Triage loop.** Walk findings in severity order. For each finding:

   - Print the full body: location(s), what, why-it-matters, suggested-action,
     plus your own recommended verdict (e.g. "Recommended: card it — this is a
     clear DIP violation worth fixing before the next API host is added").

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

   - When the user picks **Card it**, ask one follow-up if the dimension is
     ambiguous about lane: default is `Ideas`, but for **high** severity items
     offer `To Do` as an alternative.

5. **Ensure the `arch` tag exists.** If `tags[].name` doesn't include `arch`,
   run `bishop tag add arch` once before pushing any cards. (`tag add` is
   idempotent for an existing name, so re-running is safe.)

6. **Granularity pass.** Before printing the task list, re-read the agreed
   cards:

   - Merge cards that share the same dimension AND touch the same module if
     they got separated during triage.
   - Split anything that grew beyond one PR's worth of work into two cards
     with independent acceptance criteria.
   - One card ≈ one PR. If a card is a one-line change, fold it into the
     nearest related card.

7. **Print task list** for review:

   > **Target workspace:** \<name\>
   >
   > ## Tasks
   > - [ ] **Title**: \<concise card title\> | **Tag**: arch | **Lane**: Ideas | **Body**: \<body\>
   > - [ ] ...

   Then ask:

   > "Please review the tasks above. Say **push** to create the Bishop cards."

8. **Push confirmed cards** in order. For each, pipe the body via stdin:

   ```bash
   bishop card add --lane "<Lane>" --title "<Title>" --tag arch --description-file - --bottom << 'BODY'
   ### Why
   <fill in>

   ### Acceptance
   - <fill in>
   BODY
   ```

   Always pass `--bottom` so arch cards land at the bottom of the target lane
   without disrupting manual ordering.

9. **Print summary table:**

   | Card | Title | Lane | Severity |
   |------|-------|------|----------|
   | #N | ... | Ideas | high |

   Then offer:

   > Re-run `/bish-arch` after these are worked. Dismissed findings will
   > resurface — explain the same way or capture the rebuttal in a project
   > memory if it's load-bearing.

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

<guardrails>

- Do NOT push cards before the user confirms each finding via triage AND says
  "push" at the task list stage.
- Do NOT propose findings without `file:line` citations — every claim must be
  locatable in the code. Reject subagent output that omits locations.
- Do NOT include dismissed or deferred findings in the pushed cards.
- Default lane is `Ideas`. Only promote to `To Do` if the user explicitly
  picks it for a high-severity item during triage.
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
