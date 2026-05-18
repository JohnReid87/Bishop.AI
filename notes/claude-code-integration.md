# Bishop.AI ↔ Claude Code Integration — Pre-Grill Notes

Seed material for a future `/grill-me` session on the Claude Code integration design. Captures decisions reached in an earlier exploratory discussion, plus open questions to drill.

These are **pre-decisional notes**, not specs. The decisions below are working positions that survived a single round of unpacking — `/grill-me` should re-test them before they harden into CONTEXT.md or implementation.

## Premise

The current `CONTEXT.md` includes "a one-click launcher for the Claude Code Windows app" as a workspace feature. This doc explores what that integration actually looks like, given:

- Bishop.AI is single-user, local-first, Windows-only.
- The per-workspace kanban is the source of truth for work state.
- Existing skills (`grill-me`, `push-tasks`, `work-on-issue`) already encode a human-in-the-loop pattern.

## Decisions reached

### 1. Terminal launcher is an external Windows Terminal session, not embedded

Spawn `wt -d <workspace_path> -- claude` (or equivalent). No WebView2 / xterm.js / ConPTY embedding inside Bishop.AI.

**Reasoning:** state lives in the kanban, not in the agent session. Closing the terminal loses nothing because the card lifecycle captures intent + progress. Embedding would invite scope creep — last-activity timestamps on cards, agent-todo sidebar surfacing, session resume, etc. — all of which become non-features once the kanban owns state. Keeps Bishop.AI from drifting into "agent observability dashboard".

### 2. Terminal is a dumb shell — no card context passed

Just opens at the workspace root. The launcher does **not** pass card ID, description, or any other Bishop state into the shell environment.

**Reasoning:** skills handle pulling card info via the `bishop` CLI (see #4). No need to bake assumptions into the launcher about which card the user is working on.

### 3. Card lifecycle is human-driven, with skill assist

Skills *can* propose card moves (via the `bishop` CLI). Human-in-the-loop QA gates the actual move — Bishop.AI surfaces pending move requests for review before applying them.

**Reasoning:** mirrors the existing pattern in `push-tasks` (explicit user trigger) and `work-on-issue` (no auto-close, no auto-commit-push without confirmation). Human QA is part of the workflow's intent, not a limitation.

### 4. Bishop ships a CLI (`bishop`) for skill integration

Standalone `bishop.exe` process that reads/writes the same SQLite DB (`%AppData%\Bishop.AI\bishop.db`) the WinUI app uses. Skills shell out:

```
bishop card move <id> --to <lane> --note "<summary>"
```

Mirrors the `gh` pattern existing skills already know.

**Reasoning:** lowest-friction integration surface. Skills already shell out to external CLIs (`gh`, `dotnet`, `git`). No HTTP overhead. Crucially, the WinUI app does **not** need to be running for the CLI to work — Claude can be working in a terminal while Bishop is closed; pending moves surface next time the user opens Bishop.

### 5. Claude is informed of the CLI via per-workspace `CLAUDE.md`

When Bishop.AI creates a workspace, it seeds (or updates) a `CLAUDE.md` in the workspace root with a stanza describing the `bishop` CLI and how to use it for card lifecycle. Self-documenting per workspace; no skill changes needed.

**Reasoning:** Claude Code auto-loads `CLAUDE.md` from the project root. A single seed block gives every skill instant awareness without coupling skills to Bishop. Stanza overwrite policy is an open question (see below).

## Architectural shape implied

A new `Bishop.Cli` project sits as a presentation-layer sibling to `Bishop.UI`:

```
Core → Data → App → { UI, Cli }
```

Both `UI` and `Cli` call MediatR handlers in `App`. No duplicated logic.

SQLite WAL mode is required for concurrent UI + CLI access. EF Core / `Microsoft.Data.Sqlite` supports this — needs explicit configuration in the `DbContext` setup.

## Open questions for /grill-me

1. **CLI surface shape and versioning.** `bishop card move` is one verb. What's the rest of the surface — `card create`, `card update`, `card list`, `workspace list`, `workspace create`? Versioned (`bishop v1 card move ...`) from day one, or additive-only changes on an unversioned surface? GitHub's `gh` evolution is the precedent worth studying.

2. **Pending-move review UX.** When a skill calls `bishop card move <id> --to Done`, Bishop queues it. How does the user see and act on it? Toast notification? A "Pending Reviews" tab? Inline indicator on the card itself ("agent thinks I'm done — approve?")? What does dismissal look like — accept, reject, edit?

3. **CLI auth / security boundary.** Currently single-user local — no auth. But the CLI is a privileged surface (anything that can call `bishop.exe` as the user can mutate the DB). Worth being explicit: this is load-bearing for "single-user local-first" staying true. If that ever changes (a service account, CI, multi-user), the security model needs revisiting.

4. **Workspace `CLAUDE.md` seeding — overwrite policy.** When the user creates a workspace pointed at an existing repo that already has its own `CLAUDE.md` (e.g. FotM.IO), do we (a) refuse to overwrite, (b) append a Bishop stanza, (c) prompt the user, (d) maintain a separate `BISHOP.md` instead? Each has trade-offs. (b) is probably right but needs an idempotency story so re-seeding doesn't accumulate.

5. **Stale pending moves.** If the user never reviews pending moves, the queue grows indefinitely. Auto-apply after N days? Auto-expire? Just leave them forever and let the user clean house? Implication for kanban hygiene.

6. **CLI install / discovery.** Does `bishop.exe` get added to PATH by Bishop.AI? The current tech stack says **unpackaged** WinUI 3 — so the installer (if any) is a `dotnet publish` artifact, not an MSIX. How does the user get `bishop` on PATH? Manual instructions, an opt-in "add to PATH" toggle in settings, or shipping a small installer that handles it?

7. **Skill design for Bishop.AI workspaces.** Should a future Bishop-specific skill (`work-on-card <id>`?) exist, paralleling `work-on-issue` for GitHub? Or does the existing `work-on-issue` shape generalise — fetch card → explore code → implement → propose card-move? Related: does Bishop ever need its own backlog flow analogous to `grill-me` → `push-tasks` → `work-on-issue`, or do users just keep using the GitHub flow and import results into Bishop?
