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

## UI conventions

### No nested ContentDialogs

From inside a `ContentDialog`, do not open another `ContentDialog` — WinUI 3 throws a `COMException` when a dialog is shown while another is already active.

Approved substitutes:

- **`Flyout`** — for small inline confirms (precedent: `ManageWorkspacesControl.ConfirmFlyoutAsync` in `src/Bishop.UI/Views/ManageWorkspacesControl.xaml.cs`).
- **`MarkdownViewerWindow`** — for displaying markdown content in a separate window.
- **`TeachingTip`** — for transient, non-blocking information.
