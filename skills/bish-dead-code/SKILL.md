---
name: bish-dead-code
description: Dead-code review of any .NET solution registered as the current Bishop workspace: finds unreferenced C# types/members, MediatR requests never dispatched, and DI registrations never injected. Walks findings with the user one at a time and pushes agreed cards tagged `chore`. Use when the user mentions "dead code", "unused code", "obsolete code", or invokes `/bish-dead-code`.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*)
bishop.scope: workspace
bishop.command: /bish-dead-code
bishop.stage: false
bishop.category: review
---

> Recommended model: Sonnet 4.6 — grep-based heuristic detection; extended reasoning not required.

The context-pack below bundles workspace metadata, recent git history, and Bishop convention procedures (Shell selection, Card Granularity Rules, Task List Preview Format, Card Push Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack dead-code
```
If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.path` — repo root used by the discovery subagent
- `workspace.tags` — existing tag names (the skill needs a `chore` tag; see step 5)
- `workspace.lanes` — lane names (`To Do` is the push target; see step 8)
- `conventions` — STABLE/TUNABLE procedure sections

Echo the workspace name on its own line:

> **Workspace:** \<name\>

---

## What this skill does

Hunts dead code in the .NET solution at the current Bishop workspace's path. A single Explore subagent applies three heuristic dimensions — unreferenced types/members, MediatR requests never dispatched, and DI registrations never injected — producing a ranked list of candidates. The user triages findings one at a time, dismissing false positives and clustering related ones. Confirmed cards are pushed tagged `chore` to the `To Do` lane.

The skill scans the whole solution. It does not accept a scoping argument — the user dismisses out-of-scope findings during triage. Detection is grep-based, not Roslyn-based: false positives (reflection, dynamic dispatch, runtime discovery) are expected and handled via triage.

**Project agnostic, .NET only.** Works on any .NET solution registered as a Bishop workspace. No specific layout, namespace prefix, or package set is assumed.

## Review dimensions

The discovery subagent applies the Universal dimensions to every project and the Stack-conditional dimension only when the relevant package is detected. Each finding must cite at least one `file:line` location.

### Universal — apply to every project in scope

- **Unreferenced C# types** — classes, interfaces, enums, and structs defined in non-generated, non-test source files (exclude `*.g.cs`, `AssemblyInfo.cs`, `GlobalUsings.cs`, and files under `obj/`) with zero references outside their own definition file. Heuristic: grep for the type name across all `.cs` files; flag when the only hits are the definition line itself, or hits exclusively in test files with no production callers. Types decorated with reflection-discovery attributes (`[JsonConverter]`, `[XamlType]`, `[Export]`, source-generator marker attributes) are likely live via runtime discovery — flag with lower confidence and note the attribute.

- **Unreferenced public/internal members** — methods and properties on types that are themselves referenced, but the member has no callers outside its defining file. Exclude: interface-implementation members (the interface's member may be called indirectly), `override` and `virtual` members (runtime/polymorphic dispatch), members attributed `[RelayCommand]` or `[ObservableProperty]` (source-generated counterparts are the public surface), constructors, standard object-method overrides (`ToString`, `Equals`, `GetHashCode`, `GetType`), and event accessors (`add`/`remove`). Heuristic: grep for the member name across all `.cs` files; flag when zero hits appear outside the defining file.

- **DI registrations never injected** — service types registered via `AddSingleton`, `AddScoped`, `AddTransient`, or `TryAdd*` variants, but whose service type never appears as a constructor parameter anywhere in the solution. Heuristic: collect the concrete or interface type name from each registration call; grep for the type name in constructor parameter position (patterns: `(TypeName `, `, TypeName `, `(TypeName,`). Flag registrations with no constructor-parameter match. Services consumed via `IServiceProvider.GetRequiredService<T>()`, factory methods, or `ActivatorUtilities` won't appear as constructor parameters — note this when flagging so the user can verify.

### Stack-conditional — apply only when the relevant package is detected

- **MediatR IRequest types never dispatched** *(if `MediatR` or a project-local mediator abstraction is referenced)* — types implementing `IRequest`, `IRequest<T>`, `INotification`, or any project-local `ICommand`/`IQuery<T>` aliases, that are never passed to `mediator.Send(...)`, `mediator.Publish(...)`, or `sender.Send(...)`. Heuristic: collect all implementing type names; grep for the type name adjacent to a `.Send(` or `.Publish(` call (patterns: `Send(new TypeName`, `Publish(new TypeName`, `new TypeName(` within a few lines of `.Send(`). Flag types with no dispatch match. Types dispatched via a factory or string-keyed resolver won't appear — flag with medium confidence and note the absence.

<what-to-do>

1. **Workspace detection** (above).

2. **Discovery phase.** Spawn one Explore subagent (via `Agent` with `subagent_type: "Explore"` and `model: "haiku"`) with a brief that:

   - Names the repo root from `workspacePath`.
   - **First sub-step: detect whether MediatR (or a project-local mediator abstraction) is referenced.** Read the `*.sln` to enumerate projects, then check `PackageReference`s in each `*.csproj` for `MediatR` or similar. Report the detected stack on its own line before producing findings (e.g. "Detected: EF Core + MediatR" or "Detected: no mediator package").
   - Applies the **Universal** dimensions to every non-generated, non-obj `.cs` file in every project.
   - Applies the **MediatR** stack-conditional dimension only if MediatR (or an equivalent) is detected.
   - Asks for **at most 15 findings**, ranked by confidence (high/med/low — how likely the candidate is truly dead vs. a false positive). Bias toward high/medium; low-confidence items may be mentioned briefly under "also noticed" but should not be expanded.
   - Requires each finding to include: `confidence` (high/med/low), `dimension` (one of the labels above), `location` (file:line, may be multiple), `what` (1 sentence — the type/member/request/registration name and why it appears unused), `why_it_matters` (maintenance risk: dead surface area, confusion, or a missed hook-up), `suggested_action` (delete / hook up / investigate further), `fix_cost` (low/med/high).
   - Returns findings as a numbered list, confidence-ordered (high first).

   If the subagent returns more than 15 findings, ask it to re-rank and trim to 15.
   If it returns fewer than 3, surface what it found — the codebase may genuinely be clean.
   If the subagent reports no findings, congratulate the user and STOP without pushing anything.

3. **Echo summary.** Print a one-line overview the user can scan before triage:

   ```
   #1 [high] [fix:low]  Unreferenced type    — <project>/<...>/X.cs:12         — <one-line>
   #2 [med]  [fix:med]  MediatR undispatched  — <project>/<...>/Y.cs:5          — <one-line>
   #3 [low]  [fix:low]  DI never injected     — <project>/<...>/Z.cs:34         — <one-line>
   ...
   ```

4. **Triage loop.** Walk findings in confidence order. For each finding:

   - Print the full body: location(s), what, why-it-matters, suggested-action, and your recommended verdict. Weigh `fix_cost` explicitly. For low-confidence findings, name the most likely false-positive reason (reflection, runtime dispatch, service-locator pattern, etc.) and recommend dismissing unless the user recognises it as a genuine miss.

   - Use `AskUserQuestion` with these options (recommended one first, suffixed " (Recommended)"):

     - **Card it (new)** — file this finding as a new card. Default body uses the **Card body template** below.
     - **Cluster with #N: \<title\>** — fold into a card already agreed earlier in this session. Only offer when at least one prior in-session card exists.
     - **Dismiss — false positive** — confirmed live via reflection/runtime/service-locator. Capture the reason verbatim.
     - **Dismiss — context** — not worth acting on now. Capture the reason.
     - **Defer** — note it but don't card it now.

5. **Ensure the `chore` tag exists.** If `workspace.tags[].name` from the pack doesn't include `chore`, stop and tell the user: the canonical tags are seeded by `bishop workspace init` — re-running it will restore any missing tags.

6. **Granularity pass.** Before printing the task list, apply Card Granularity Rules (TUNABLE) (in `conventions`). Merge findings that touch the same type or module for the same reason; split only when pieces have independent acceptance criteria.

7. **Print task list** per Task List Preview Format (STABLE) (in `conventions`). Then ask:

   > "Please review the tasks above. Say **push** to create the Bishop cards."

8. **Push confirmed cards** in order per Card Push Procedure (STABLE) (in `conventions`). Use `--tag chore` and `--lane "To Do"`. Always pass `--bottom`.

9. **Print summary table:**

   | Card | Title | Lane | Confidence |
   |------|-------|------|------------|
   | #N | … | To Do | high |

</what-to-do>

## Card body template

Use the H3-section markdown template below. Required sections: `### Why` and `### Acceptance`. Include optional sections only when they add value.

```markdown
### Why
<dimension> — `<TypeName>` appears unused. Locations: `<file:line[, file:line]...>`.

### Changes
- `<file:line>` — <delete / hook up / investigate>

### Acceptance
- <how we'll know the dead code is gone or confirmed live>

### Related
<for clustered cards: list each additional finding as `<file:line> — <one-line summary>`>
```

Rules:
- Backtick file paths, identifiers, and CLI commands.
- Use bullets in `### Changes` and `### Acceptance`.
- Omit `### Related` for single-finding cards.
- Omit `### Changes` when the suggested action is already clear from `### Why`.

<guardrails>

- Do NOT push cards before the user confirms each finding via triage AND says "push" at the task list stage.
- Do NOT propose findings without `file:line` citations — every claim must be locatable in the code.
- Do NOT apply the MediatR dimension if MediatR isn't referenced in the solution. Always re-detect; never assume the stack from the workspace name or prior runs.
- Do NOT flag compiler-generated files (`*.g.cs`, `obj/`, `AssemblyInfo.cs`, `GlobalUsings.cs`) as containing unreferenced types.
- Do NOT flag `override`, `virtual`, or interface-implementation members as unreferenced — runtime/polymorphic dispatch makes them live.
- Do NOT flag members attributed `[RelayCommand]` or `[ObservableProperty]` as unreferenced — the source generator emits the real public surface.
- All `chore` cards land in `To Do`. Do not prompt for an alternative lane during triage — re-prioritisation happens on the board after the push.
- Always pass `--bottom` to `bishop card add`. Dead-code reviews are bulk pushes by nature — they must not jump ahead of manually prioritised work.
- Do NOT produce a Markdown report or companion file. Cards are the durable output.
- If a finding spans multiple types/files reflecting the same underlying issue, present it as ONE finding with multiple `file:line` locations, not one finding per file.

</guardrails>
