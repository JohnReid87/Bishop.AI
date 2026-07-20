# bishop.ai

> See [.bishop/BISHOP_CONTEXT.md](./.bishop/BISHOP_CONTEXT.md) — Bishop CLI reference and live workspace state for LLM agents.
> See [BRAND.md](./BRAND.md) — palette, tag colours, icon policy, and asset directory. Reference before introducing any colour or icon.

## Shell command conventions for agents

Claude Code's sandbox hard-refuses certain shell patterns — no allowlist entry can satisfy them, so the `bish-auto-card` loop just stalls. Avoid generating these in `Bash` or `PowerShell` tool calls:

- **Bash with variable expansion** (`$VAR`, `${VAR}`) — pass literal values, or use a dedicated tool (`Read`, `Glob`, `Grep`) instead of a shell pipeline.
- **PowerShell script blocks that look like arbitrary code** — `Where-Object {$_.Name -match "x"}` is fine; anything resembling `Invoke-Expression` or runtime-assembled script is not.
- **PowerShell .NET method calls** (`[Type]::Method(...)`, e.g. `[Math]::Round(...)`) — prefer cmdlets and operators.
- **`Select-String` with complex `-Path`** — use a single literal path, no arrays or subexpressions. For multi-file search, use the `Grep` tool.

Host-specific:

- The Python binary on this Windows host is `python`, not `python3`. Prefer PowerShell `ConvertFrom-Json` for parsing JSON output instead of Python one-liners.

## Documentation drift resistance

When `CONTEXT.md` and the code disagree, the code wins — surface the drift to the user and fix the doc, don't perpetuate the false claim in further edits or answers.

Load-bearing factual lists in `CONTEXT.md` (the tag set, the lane set) are wrapped in HTML-comment fact-blocks of the form `<!-- bishop-fact:NAME --> … <!-- /bishop-fact -->` and asserted against their canonical code constants (e.g. `TagNames.All`, `SystemLaneNames.All`) by `Bishop.Tests.Docs.ContextMdFactBlockTests`. Edit the block as you would normal Markdown — the next build fails fast if the contents diverge from code. Add new fact-blocks the same way when another doc fact starts drifting.

## UI conventions

### No nested ContentDialogs

From inside a `ContentDialog`, do not open another `ContentDialog` — WinUI 3 throws a `COMException` when a dialog is shown while another is already active.

Approved substitutes:

- **`Flyout`** — for small inline confirms (precedent: `ManageWorkspacesControl.ConfirmFlyoutAsync` in `bishop/src/Bishop.UI/Views/ManageWorkspacesControl.xaml.cs`).
- **`MarkdownViewerWindow`** — for displaying markdown content in a separate window.
- **`TeachingTip`** — for transient, non-blocking information.

## Out of scope

- **bishop.life.** Removed (cards #1123/#1124) — the sister daily-reflection product (`life/` peer, `bish-life-*` skills) wasn't used and added ongoing repo complexity; recoverable from git history if ever needed. Mirrors the GitHub Issues sync removal precedent (cards #973/#974).
