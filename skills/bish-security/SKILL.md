---
name: bish-security
description: Security audit of any .NET solution registered as the current Bishop workspace. Mirrors `bish-arch` for security — scans for injection (SQL/command/LDAP/XPath/path), weak crypto and hardcoded secrets, unsafe deserialization and dynamic code, missing authn/authz and sensitive logging, plus stack-conditional checks (ASP.NET Core, Blazor, WPF/MAUI, Worker, EF Core), config-file secrets, and GitHub Actions workflow misconfig (unpinned actions, broad `permissions:`, `pull_request_target`). Also runs `dotnet list package --vulnerable --include-transitive` for CVE coverage. Walks findings with the user one at a time and pushes agreed cards tagged `security`. Use when the user mentions "security review", "security audit", or invokes `/bish-security`.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*), Bash(dotnet:*)
bishop.scope: workspace
bishop.command: /bish-security
bishop.stage: false
bishop.category: code
firstRunModel: claude-opus-4-7
reRunModel: claude-sonnet-4-6
---

> Recommended model: Opus 4.7 — security heuristic catalogue requires sustained multi-step judgement.

The context-pack below bundles workspace metadata, recent git history, and Bishop convention procedures (Shell selection, Card model, Findings Recording Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**Before anything else — load the context-pack:**

```
bishop context-pack security
```
If the command exits non-zero, surface the stderr message as-is and STOP.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.path` — repo root used by the discovery subagent
- `workspace.tags` — existing tag names (the skill needs a `security` tag; see step 3)
- `workspace.lanes` — lane names (defaults to `To Do` when pushing)
- `conventions` — STABLE/TUNABLE procedure sections (Shell selection, Card model, Findings Recording Procedure)

Echo the workspace name back on its own line:

> **Workspace:** \<name\>

---

## What this skill does

Reviews the security posture of the .NET solution at the current Bishop
workspace's path. A single Explore subagent first detects the stack in use,
then applies universal security dimensions plus stack-conditional ones, also
folding in any vulnerable-package findings from
`dotnet list package --vulnerable --include-transitive` and any GitHub
Actions workflow misconfig under `.github/workflows/`. The user triages
findings one at a time, clustering related ones into single cards as they
go. Confirmed cards are pushed tagged `security` to the `To Do` lane.

The skill always reviews the whole solution. It does not accept a scoping
argument — security findings are cross-cutting, and the CVE scan is global
anyway. If the user wants something narrower, they can say so during triage
and dismiss out-of-scope findings.

**Project agnostic, .NET only.** The skill works on any .NET solution
registered as a Bishop workspace — WPF, ASP.NET Core, Blazor, MAUI, console,
worker, or class library. It adapts the applied dimensions to what's in the
solution (detected from `*.csproj` SDK and package references). Example
paths or names anywhere in this skill are illustrative; no Bishop.AI-specific
layout, namespace prefix, or package set is assumed.

## Review dimensions

The discovery subagent must first detect the stack and apply only the
dimensions relevant to what's actually in the solution. Each finding must
cite at least one `file:line` location.

### Universal — apply to any .NET solution

- **Injection family** — SQL via `FromSqlRaw`, `SqlCommand`, or Dapper
  string-concat; command injection via `Process.Start` with unvalidated
  args; LDAP filter injection; XPath injection; path traversal in file
  APIs taking user input.
- **Crypto & secrets** — hardcoded keys / connection strings / API tokens
  in source; `MD5` / `SHA1` / `DES` / `RC4` for security purposes; ECB
  block mode; static or all-zero IVs; `System.Random` used for tokens or
  passwords; DPAPI used with `CurrentUser` scope where `LocalMachine` is
  required (or vice versa).
- **Deserialization & dynamic code** — `BinaryFormatter`,
  `NetDataContractSerializer`, `SoapFormatter`; `JsonSerializerSettings`
  with unsafe `TypeNameHandling` (`Auto` / `All` / `Objects`);
  `Activator.CreateInstance` from external input; `Assembly.LoadFrom`
  on attacker-controlled paths.
- **Authn / authz & sensitive logging** — endpoints missing `[Authorize]`
  in a project that otherwise gates access; leaking `[AllowAnonymous]` on
  endpoints that handle account state; role / policy bypass via missing
  enforcement; tokens, passwords, full PII, or stack traces written to
  logs; developer exception page enabled outside Development.

### Stack-conditional — apply only when the SDK / package is detected

- **ASP.NET Core** *(if `Microsoft.AspNetCore.*` referenced or SDK is
  `Microsoft.NET.Sdk.Web`)* — permissive CORS (`AllowAnyOrigin` plus
  `AllowCredentials`); missing antiforgery on cookie-authenticated POST
  endpoints; cookie flags (`HttpOnly`, `Secure`, `SameSite`); missing HSTS
  in production; missing HTTPS redirect; absent or over-permissive CSP;
  developer exception page leaking outside Development.
- **Blazor** *(if `Microsoft.AspNetCore.Components.*` referenced or SDK is
  `Microsoft.NET.Sdk.BlazorWebAssembly`)* — render-mode crossings that
  break the auth boundary (interactive component reachable without the
  expected `[Authorize]`); `@page` routes missing authorization where
  siblings require it.
- **WPF / WinForms / WinUI / MAUI** *(if `UseWPF` / `UseWindowsForms`
  true, `Microsoft.WindowsAppSDK` referenced, or `Microsoft.Maui.*`
  referenced)* — DPAPI scope misuse for at-rest secrets; missing
  certificate pinning on HTTPS calls to first-party endpoints; ClickOnce
  deployments without signing.
- **Worker / hosted services** *(if `Microsoft.Extensions.Hosting` is the
  host model)* — secrets read from environment but echoed in startup
  logs; long-lived singletons capturing per-request secrets;
  cancellation-token propagation gaps that leave credentials in memory
  after shutdown.
- **EF Core** *(if `Microsoft.EntityFrameworkCore.*` referenced)* — raw
  SQL paths via `FromSqlRaw` / `ExecuteSqlRaw` with interpolated user
  input where `FromSqlInterpolated` / `ExecuteSqlInterpolated` (or
  parameters) would parameterise safely.

### Config files

Audit `appsettings*.json`, `*.config`, and `web.config` for: secrets
checked into source (connection strings with embedded passwords, API
keys, JWT signing keys); debug flags enabled
(`"DetailedErrors": true`, `<compilation debug="true"/>`); weak
settings (TLS downgrade, validation disabled, anonymous access toggled).

### CI / supply chain

Audit `.github/workflows/*.yml` for: third-party actions pinned by
floating ref (`@main`, `@vN`) rather than full commit SHA; missing or
overly broad top-level `permissions:` (defaulting to repo-wide
write); `pull_request_target` triggers that check out and execute
PR-supplied code; secrets passed to jobs that run untrusted code.

### Vulnerable packages (CVE)

Output of `dotnet list package --vulnerable --include-transitive` is
folded into the subagent's input as a structured list of
(project, package, version, severity, advisory URL). Each vulnerable
package becomes a candidate finding unless suppressed by an in-repo
ignore mechanism the subagent can identify.

### Test-project exclusion

For files inside `*.Tests.csproj` projects (or under a `tests/`
directory), skip **secrets** and **authn / authz** dimensions only.
Still apply **injection**, **deserialization**, and **crypto**
dimensions — production-shaped vulnerabilities in test helpers should
surface, because the same code often gets copy-pasted into production.

<what-to-do>

1. **Workspace detection** (above).

2. **Pre-flight: .NET workspace check.** Glob for `**/*.csproj` from
   `workspacePath`. If zero matches, STOP and tell the user:

   > **No `*.csproj` found at `<workspacePath>`.** `/bish-security` audits .NET
   > solutions only. If this workspace is .NET, check that the projects
   > are committed; otherwise this skill doesn't apply.

3. **Ensure the `security` tag exists.** If `tags[].name` does not
   include `security`, auto-create it with a muted amber colour drawn
   from `BRAND.md`'s palette guidance (avoids the reserved error red
   `#ff5555` and the bright accent green, sits in the same
   saturation/lightness band as the canonical seven):

   ```bash
   bishop tag add security --colour "#c9885f"
   ```

   Do NOT stop on this step — auto-create is the contract, to remove
   first-run friction.

4. **CVE scan.** Run from `workspacePath`:

   ```bash
   dotnet list package --vulnerable --include-transitive
   ```

   - On non-zero exit (offline, restore failure, etc.), print a single
     line: `⚠ CVE scan skipped: <stderr>` and continue with the code
     review. Partial value beats no value.
   - On success, parse the text output into a structured list of
     `(project, package, version, severity, advisoryUrl)` tuples and
     hold it for the subagent brief.

5. **Discovery phase.** Spawn one Explore subagent (via `Agent` with
   `subagent_type: "Explore"` and `model: "haiku"`). Brief it with:

   - The repo root from `workspacePath`.
   - **First sub-step: detect the stack.** Read the `*.sln` to enumerate
     projects, then for each `*.csproj` capture the `Sdk` attribute,
     `<TargetFramework[s]>`, `UseWPF` / `UseWindowsForms` properties,
     and `PackageReference`s. Report the detected stack on its own line
     before producing any findings (e.g. "Detected: ASP.NET Core +
     EF Core + Serilog + xUnit").
   - The **Universal** dimensions above, applied to every project.
   - The **Stack-conditional** dimensions above, applied only to
     projects where the relevant SDK / package is present. If no
     ASP.NET Core project exists, do not surface CORS / antiforgery
     findings; if no Blazor project exists, do not surface render-mode
     findings; and so on.
   - The **Config files** dimensions, applied to every
     `appsettings*.json`, `*.config`, and `web.config` in the repo.
   - The **CI / supply chain** dimensions, applied to every
     `.github/workflows/*.yml`.
   - The **Vulnerable packages** list captured in step 4 (or note that
     the scan was skipped if step 4 failed).
   - The **Test-project exclusion** rule: for files inside test
     projects, skip secrets and authn/authz dimensions only.
   - Cap: **at most 15 findings**, ranked by severity. If more than 15
     survive, ask the subagent to re-rank and trim to 15.
   - Each finding must include: `severity` (high/med/low), `dimension`
     (one of the labels above, or a stack-derived dimension the
     subagent introduces with justification), `location` (file:line —
     may be multiple), `file` (primary source file path,
     workspace-relative), `symbol` (canonical identifier the finding
     is about — affected method/class/identifier, e.g.
     `OrderRepository.GetByName`), `what` (1 sentence describing the
     issue), `why_it_matters` (consequence in this codebase, not a
     textbook quote), `cwe` (when known — e.g. `CWE-89` for SQL
     injection; omit when not confidently identified),
     `suggested_action` (concrete change). Reject subagent output that
     omits `file` or `symbol`.
   - Returns findings as a numbered list, severity-ordered (high first).

   If the subagent reports **no findings** (all applicable dimensions
   clean, no vulnerable packages, no workflow misconfig), record this
   run via the no-findings path of `Findings Recording Procedure` (in
   `conventions`) with `--skill bish-security`, then congratulate the user
   and STOP without pushing anything.

6. **Echo summary.** Print a one-line overview the user can scan before
   triage. Format (paths shown are illustrative):

   ```
   #1 [high] Crypto/Secrets    — <project>/<...>/X.cs:42         — <one-line>
   #2 [med]  Injection/SQL     — <project>/<...>/Y.cs:55         — <one-line>
   #3 [med]  CI/Action pinning — .github/workflows/ci.yml:18,33  — <one-line>
   ...
   ```

7. **Dedupe against existing cards.** Run `bishop card list --json`.
   Drop any finding whose proposed title matches an existing **open**
   card (case-insensitive prefix). Findings whose title matches a card
   already in `Done` are kept — resurfacing means a regression.

   If every finding is filtered out, tell the user:

   > All security findings already have open cards on the board.
   > Nothing new to file.

   Record this run via the no-findings path of `Findings Recording Procedure` (in `conventions`) with `--skill bish-security`, then STOP.

8. **Triage loop.** Walk surviving findings in severity order. For each
   finding:

   - Print the full body: location(s), what, why-it-matters, CWE (if
     known), suggested-action, plus your own recommended verdict
     (e.g. "Recommended: card it — `BinaryFormatter` on attacker-reachable
     input is a clear RCE risk").

   - Use `AskUserQuestion` with these options (recommended one first,
     suffixed " (Recommended)"):

     - **Card it (new)** — file this finding as a new card. Default body
       uses the **Card body template** below.
     - **Cluster with #N: \<title\>** — fold this finding into a card
       already agreed earlier in the session. Append its location and a
       one-line summary to that card's `### Related` section. Only offer
       when at least one prior in-session card exists.
     - **Dismiss — context** — user explains why this isn't an issue.
       Capture the reason verbatim so a repeat run can show it again
       with the user's own rebuttal as a hint. Suggest the user pin
       load-bearing rebuttals as a project memory so future runs don't
       re-ask.
     - **Defer** — note it but don't card it now.

   - **Track the finding** in a session log per the "Track findings during
     triage" sub-step of `Findings Recording Procedure` (in `conventions`).
     For the identity fields: `file` = the subagent's `file`, `rule` = the
     subagent's `cwe` when known (e.g. `CWE-89`) otherwise the `dimension`
     (e.g. `Injection/SQL`, `Crypto/Secrets`), `symbol` = the subagent's
     `symbol`. Emit all three on every finding so the handler computes a
     stable identity hash rather than falling back to the title.
     Map this skill's triage choices to pending outcomes: **Card it (new)** and
     **Cluster with #N** → `pending-card:<session-index>` (the cluster reuses
     the index assigned to the card it folds into); **Dismiss — context** →
     `dismissed`; **Defer** → `parked`. Every finding surfaced must appear in
     the log; it is the input to step 12's record call.

9. **Granularity pass.** Before pushing, re-read the agreed cards:

   - Merge cards that share the same dimension AND touch the same
     module if they got separated during triage.
   - Split anything with independent acceptance criteria into two
     cards.
   - One card ≈ one PR. If a card is a one-line change, fold it into
     the nearest related card.

10. **Push confirmed cards** in order using `bishop card create` per
    `Card Push Procedure` (in `conventions`). Use `--tag security`.

11. **Print summary table:**

    | Card | Title | Severity |
    |------|-------|----------|
    | #N | ... | high |

    Then offer:

    > Re-run `/bish-security` after these are worked. Dismissed findings
    > will resurface — capture load-bearing rebuttals as a project memory
    > so future runs don't re-ask.

12. **Record this run** by following `Findings Recording Procedure` (in
    `conventions`) with `--skill bish-security`. Before emitting, resolve any
    `pending-card:<n>` markers in the session log to `carded:#<N>` using the
    card numbers returned by step 10, so every tracked finding carries one of
    the three final outcomes (`carded:#<N>` / `dismissed` / `parked`).

</what-to-do>

## Card body template

Use the H3-section markdown template below. Required sections: `### Why`,
`### Risk`, and `### Acceptance`. Include optional sections only when they
add value.

```markdown
### Why
<dimension> — <what's wrong, 1 sentence>. Locations: `<file:line[, file:line]...>`.

### Risk
<High|Med|Low> — <CWE-N name if known>

### Acceptance
- <verifiable criterion>

### Changes
- `<file:line>` — <suggested change>

### Related
- `<file:line>` — <one-line summary of clustered finding>
```

Rules:
- Backtick file paths, identifiers, and CLI commands.
- Use bullets in `### Changes`, `### Acceptance`, and `### Related`.
- Omit `### Changes` when the suggested action is already clear from `### Why`.
- Omit `### Related` for single-finding cards.

<guardrails>

- Do NOT push cards before the user confirms each finding via triage.
- Do NOT propose findings without `file:line` citations — every claim
  must be locatable in the code. Reject subagent output that omits
  locations.
- Do NOT include dismissed or deferred findings in the pushed cards.
- Do NOT STOP when the CVE scan fails — print a one-line warning and
  continue. Partial value beats no value when offline.
- Do NOT STOP when the `security` tag is missing — auto-create it.
- Always pass `--bottom` to `bishop card create`. Security reviews are
  bulk pushes by nature — they must not jump ahead of manually
  prioritised work.
- Do NOT assume specific namespace prefixes (`Bishop.*`, `MyApp.*`).
  Work from project layout, not naming conventions.
- Do NOT apply stack-conditional dimensions to projects where the
  relevant SDK / package isn't referenced. If ASP.NET Core isn't in
  the solution, no CORS / antiforgery findings. The stack detection
  in step 5 is the gate.
- Do NOT skip injection / deserialization / crypto findings in test
  projects. Test-project exclusion applies to secrets and authn/authz
  only — production-shaped vulns in test helpers still surface.
- If the discovery subagent returns >15 findings, ask it to re-rank
  and trim — don't silently truncate.
- If a finding spans multiple files but reflects the same underlying
  issue (e.g. "every handler logs the raw request body including PII"),
  present it as ONE finding with multiple `file:line` locations, not
  one finding per file.

</guardrails>
