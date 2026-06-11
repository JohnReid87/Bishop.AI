# MCP integration — spike notes

Seed material for a future decision on whether `bishop.exe` should expose itself to Claude as a [Model Context Protocol](https://modelcontextprotocol.io) server, in addition to the current CLI surface.

> **Pre-decisional.** Working position, not a spec. The positions below survived a single round of unpacking — re-test them via `/bish-grill-cards` before they harden into [CONTEXT.md](../CONTEXT.md) or [DIRECTION.md](../DIRECTION.md).

## Purpose

Bishop currently integrates with Claude as a **CLI surface**: skills, `CLAUDE.md`, and `.bishop/BISHOP_CONTEXT.md` describe the `bishop` command set in prose, and Claude composes calls through the `Bash` / `PowerShell` tools. That works, but every command signature lives in tokens the model has to read each session, and every invocation pays a `bishop.exe` cold-start plus a transcript-resident text result.

This note asks whether porting the AI-facing surface to MCP — keeping the CLI for scripts and humans — meaningfully reduces the **per-session context cost** of working in a Bishop workspace, and what that port would entail.

The short answer, given how Claude Code's MCP client currently treats Resources (see [Premise](#premise--current-claudebishop-surface)), is that the cost case does not pay out today. The longer answer — the shape an MCP port would take if a trigger does fire later — is held in this note so the design questions don't have to be re-litigated from scratch.

Out of scope:

- Token measurements. The analysis here is qualitative — naming the prose blocks that would shrink without measuring them.
- A build spec. Implementation shape is sketched only enough to judge difficulty.
- MCP Prompts as a primitive. Claude Code skills are already a richer authoring surface than MCP prompts; skills stay where they are.

## Premise — current Claude↔Bishop surface

What's true today:

- The CLI is the AI integration surface. Skills (`bish-work-on-card`, `bish-auto-card`, `bish-coverage`, …) shell out to `bishop` via `Bash` / `PowerShell` tool calls.
- Command signatures are documented in prose. The full CLI surface lives under "CLI surface (`bishop`)" in [CONTEXT.md](../CONTEXT.md) and is mirrored into the generated `.bishop/BISHOP_CONTEXT.md` (canonical source: `bishop/src/Bishop.App/Services/Terminal/BishopContext.static.md`).
- Every CLI invocation pays a `bishop.exe` cold-start (~200–500 ms of .NET startup) and emits a text result into the transcript. Text results are not cached across turns; they re-bill on each subsequent turn that includes them in context.
- Workspace resolution is cwd-driven. `bishop workspace current` walks the directory tree until it finds a registered workspace; commands accept `-w` to override.
- The CLI is used by *more than* Claude. `bish-auto-card`, batch runs, `coverage.ps1`, ad-hoc PowerShell from humans, and the bundled skills' inline shell calls all depend on the CLI's surface and output format.
- **Claude Code's MCP client does not materialise Resources into the cached system prompt.** Resources stay on the server and are accessed dynamically, ending up in the conversation context only when explicitly requested. This is the load-bearing input to the cost analysis below: an MCP Resource read of `BISHOP_CONTEXT.md` costs the same tokens, in the same uncached part of the conversation, as running `bishop context print` and reading its output. Tool schemas — separately — are believed to live in the cached system prompt, but that has not been verified for Claude Code specifically (see [open questions](#open-questions)).

## Working position — defer, but architecturally ready

The proposal is **not** to build MCP support today.

The token-cost case was the driver for opening this investigation, and the Resources-not-cached behaviour collapses its central lever. With Resources living in-conversation rather than in-prefix, an MCP read of `BISHOP_CONTEXT.md` costs the same as a CLI invocation that prints the same content. The structural cost difference that originally motivated the work — system-prompt-cached context being ~8–10× cheaper than transcript-resident tool results — does not apply to the Resources surface in Claude Code today. What survives is a smaller bundle of tactical wins (see [What MCP would shrink and entail](#what-mcp-would-shrink-and-entail)) which, individually, do not pay for a second integration surface.

The architecture leaves the door open. The MediatR-based `Bishop.App` layer is exactly the right shape for an MCP adapter — each `IRequest<T>` maps cleanly to one MCP tool, and the read-side queries are already factored well enough to back Resources. A future `Bishop.Mcp` project would be additive, not a rewrite. The cost of deferring is near zero: nothing in the current code closes the option off.

Triggers to revisit:

- Claude Code (or another primary MCP host Bishop targets) ships Resource caching into the system-prompt prefix. This is the load-bearing condition — it is what the original cost argument required.
- `.bishop/BISHOP_CONTEXT.md` grows materially beyond its current size, such that the CLI-surface prose alone becomes a measurable bottleneck.
- Token-cost pain surfaces from real Bishop usage (long sessions, batch runs, etc.) that traces back to the CLI surface rather than to other context.
- A clean source-generator pattern for `IRequest<T>` → MCP-tool emerges in the .NET MCP ecosystem, dropping the implementation cost low enough that even the small tactical wins justify it.

Until one of these fires, the CLI keeps earning its keep.

## Shape if and when we revisit

Held here so that, if a trigger does fire, the design questions don't have to be re-litigated from scratch. Working positions, not commitments.

### 1. `bishop mcp serve` — a new subcommand over stdio

A new subcommand on the same `bishop.exe`:

```
bishop mcp serve --workspace <path>
```

Speaks the MCP JSON-RPC protocol over stdio. Single binary, no daemon, lifecycle = lifetime of the Claude session that spawned it. Reuses the existing `Bishop.App` MediatR handlers in-process — no DB-over-IPC, no second process model.

**Reasoning:** stdio is the MCP standard for local servers and the simplest lifecycle to reason about. A separate binary or long-running HTTP daemon adds shipping and lifecycle complexity for no concurrent-session benefit (Bishop is single-user, local-first per [CONTEXT.md](../CONTEXT.md) → Out of scope).

### 2. Per-workspace `.mcp.json` registers the server

Claude Code reads MCP server config from a project-level `.mcp.json` at the workspace root. `bishop workspace init` writes (or updates) it to register the server pinned to that workspace:

```jsonc
{
  "mcpServers": {
    "bishop": {
      "command": "bishop",
      "args": ["mcp", "serve", "--workspace", "<absolute-workspace-path>"]
    }
  }
}
```

Workspace is fixed for the session. This mirrors the per-workspace Claude / Terminal launcher model already shipped — every Bishop workspace already implies "open Claude here, scoped to this directory".

**Reasoning:** the cwd-ancestor-match the CLI uses is unreliable for MCP because Claude Code's launch cwd isn't guaranteed to match the user's workspace, especially in headless or IDE-launched flows. A global server with `workspace_path` on every tool call works but adds verbose required args to every call and weakens the "current workspace" mental model.

### 3. Surface = Tools first, Resources contingent

- **Tools** map 1:1 from existing CLI commands: `card_add`, `card_view`, `card_move`, `card_edit`, `card_claim`, `card_close`, `card_reopen`, `card_push`, `batch_create`, `batch_view`, … Each takes structured arguments matching the underlying MediatR request — no flag parsing or escaping.
- **Resources** (`bishop://workspace/context`, `bishop://workspace/metadata`, `bishop://board/snapshot`) are only worth implementing if the host caching story changes. As long as Resources are read-on-demand into the conversation rather than cached in the prefix, they offer no token-cost advantage over the equivalent CLI invocation — and the CLI already covers the same read surface via `bishop context print`, `bishop workspace current --json`, and `bishop card list --json`. Ship Tools first; revisit Resources when (and if) the trigger fires.
- **No Prompts.** Skills stay as files under `skills/<name>/` and continue to ship via `bishop install-skills`.

**Reasoning:** Tools capture the action surface and benefit from the cached-system-prompt schema (subject to [open question 1](#open-questions)). Resources only pay off if the host caches them — which Claude Code doesn't, today. Building Resources speculatively means maintaining a surface whose value is contingent on a behaviour outside our control.

### 4. CLI stays as-is for scripts, batch, and humans

The CLI is not deprecated. `bish-auto-card`, `bishop batch run`, `coverage.ps1`, manual PowerShell, and human terminal use all keep working unchanged. Both surfaces share the same `Bishop.App` handlers underneath; MCP doesn't fork the domain logic.

**Reasoning:** the surfaces serve different audiences. Interactive Claude sessions pay token cost on every prose line in context; scripts and humans don't. Deprecating the CLI for MCP would punish the non-AI consumers for an AI-only win, and the maintenance cost of running both is low because they share handlers.

## What MCP would shrink and entail

The combined wins-and-costs read that justifies the deferral.

**Wins, in descending order of magnitude:**

- Tool schemas live in the cached system prompt (assumed; see [open question 1](#open-questions)). The "CLI surface (`bishop`)" block in `BISHOP_CONTEXT.md` and `CONTEXT.md` becomes redundant *for Claude* — a one-off saving of a few hundred tokens per session, no structural reshape. The prose stays in the human-facing docs.
- Subprocess spawn (~200–500 ms per call) → in-process dispatch. Real but rarely felt at the call counts a typical Bishop session hits.
- Structured arguments / structured results replace CLI flag composition and text parsing. Modest correctness win on edge cases (PowerShell quoting, list arguments). Few enough mistakes in practice that it does not pay for the port on its own.
- Skill-body prose that today rehearses CLI syntax could be replaced with "use the Bishop MCP tools" — but only after rewriting every skill, which is its own substantial scope (see [open question 2](#open-questions)).

**Costs:**

- New code: a `Bishop.Mcp` project (or folder under `Bishop.App`) carrying the stdio JSON-RPC loop and the tool-registration layer mapping each MediatR request to an MCP tool descriptor. Tool descriptors need a JSON-schema for args and results; the mapping is mechanical but per-handler.
- New subcommand wiring in `Bishop.Cli` to host the server loop.
- Changes to existing code: `bishop workspace init` writes/merges `.mcp.json`; `bishop install-skills` may need MCP-related guidance depending on how skills migrate.
- An MCP server library dependency (the official `ModelContextProtocol` NuGet package handles stdio JSON-RPC + capability negotiation).
- Documentation drift surface: tool naming, argument shape, and error semantics need a written rule before MCP lands, or the CLI and MCP surfaces will diverge cosmetically.

**Sizing:**

- Small if scoped to Tools only, no Resources, no skill rewrites — probably one focused batch.
- Medium if `bishop workspace init` plumbing and a couple of Resources land alongside Tools — two or three batches.
- Open-ended once skill rewrites enter scope, because every bundled skill that hardcodes `bishop` CLI calls has to be reviewed.

**Honest assessment:** even with a clean target architecture, the work-to-payoff ratio is poor today. Each win is small in isolation; the path to a larger win runs through skill rewrites whose cost dominates the win.

## Open questions

Things to drill if a trigger fires:

1. **Does Claude Code cache MCP tool schemas into the system prompt?** The largest remaining win assumes yes. If tool schemas are injected per-turn into the conversation instead, the BISHOP_CONTEXT.md trim disappears too and the tactical-wins bundle shrinks further.
2. **Bundled-skill compatibility.** Skills are vendored to `~/.claude/skills/` globally and don't know whether the current workspace has MCP configured. If a skill body says "run `bishop card view 42`", Claude executes it via Bash regardless of whether MCP is available. Options: (a) leave skills on CLI; the only Claude-visible win is the BISHOP_CONTEXT.md trim, (b) rewrite skill bodies to prefer MCP tools when available and fall back to CLI, (c) make `bishop install-skills` aware of MCP and ship two skill variants.
3. **`.mcp.json` ownership.** If `bishop workspace init` writes it, what happens when the user has hand-edited the file? Merge or overwrite? Probably merge with a marker block, similar to how `CLAUDE.md` stanza ownership has been considered before.
4. **CLI-to-MCP drift.** Both surfaces share `Bishop.App` handlers, so logical drift is bounded — but tool-name conventions (snake_case vs the CLI's space-separated subcommands) and argument-name conventions need a written rule before the second surface lands, otherwise the two will diverge cosmetically.
5. **MCP server install / discoverability.** Per-workspace `.mcp.json` requires that `bishop.exe` is on the user's `PATH` (or that the JSON uses an absolute path). Bishop has no installer — `bishop.exe` is built from source and its presence on `PATH` is a manual arrangement, so this needs checking before committing.
