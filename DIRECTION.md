# Bishop.AI — Direction

The destination this project is pointed at, and the scope decisions that get us there. Read this alongside [CONTEXT.md](CONTEXT.md), which describes what is *already shipped*; this file describes what we're *building toward* and what we've deliberately decided not to build.

Single source of truth for "is this in scope or not". When the answer changes, update this file in the same PR.

## Destination

Deep Claude Code integration on top of the kanban + `bishop` CLI scaffolding that already exists.

The kanban tracks work state per workspace. Skills (`bish-work-on-card`, `bish-grill-me`, future siblings) shell out to the `bishop` CLI to read and mutate that state. The user reviews everything material in the conversational loop. Bishop.UI is the interactive surface over the same state — a kanban view with a narrow set of direct mutations (see [Resolved scope decisions](#resolved-scope-decisions)). No agent observability dashboard, no embedded terminals, no plugin system.

The differentiator is the **layer above the editor** — cross-workspace orchestration + kanban as work-state source of truth + skill integration. The editor layer (VS Code + Claude extension) already exists; Bishop doesn't compete inside its box.

## Load-bearing assumptions

If any of these changes, expect the design to fall over.

- **Single-user, local-first, Windows-only.** No accounts, no servers, no sync. The `bishop` CLI is a privileged surface — anything that can invoke it as the user can mutate the DB. If a service account or multi-user mode ever appears, the security model needs revisiting from scratch.
- **Solo + Claude is the audience.** Not external contributors, not end users yet. Docs (CONTEXT.md, DIRECTION.md) exist to give Claude operating context and to remind future-me of decisions, not to onboard others.
- **Kanban is the work-state source of truth.** Not the agent session, not git history. Closing the terminal loses nothing because the card captures intent and progress.

## Resolved scope decisions

### CLI as primary mutation surface for skills and automation
The `bishop` CLI is the primary mutation surface for skills and automated workflows. Skills invoke `bishop` commands; the conversational loop is the human-in-the-loop gate.

**Decision updated:** the WinUI app now supports a narrow set of direct mutations — card detail dialog (with delete), drag-and-drop card move, and lane CRUD. The previous stance was: *"The WinUI app is a read-only viewer of the same state. UI editability is possible later but not required for any committed milestone."* This held while the board was scaffolding; it no longer reflects what is actually being built. The CLI remains the automation surface; the UI is the interactive surface for the human user and handles these mutations directly rather than routing through the CLI.

### No pending-move review queue
Card moves apply immediately when the CLI is called. Human-in-the-loop gating happens at the **skill** level — `bish-work-on-card` prompts the user in the conversation before invoking `bishop card move ... --to-lane Done`. By the time the CLI runs, the move is already approved.

Cut because the original "Bishop surfaces pending move requests for review" design (notes/_archive/claude-code-integration.md §3) was overengineering for the workflow that actually emerged. Revisit only if non-interactive agent runs become a real use case.

### No automatic CLAUDE.md seeding
Bishop does not inject anything into workspace `CLAUDE.md` files. Skills are companion installs that carry their own knowledge of the `bishop` CLI surface.

Cut because (a) workspaces attached from existing repos (FotM.IO-style) already have authored `CLAUDE.md` files and auto-seeding risks collisions, (b) keeping ownership clean is cheap, (c) the skill is already loaded globally for the only consumer that matters.

### Skills live in this repo
The bundled skills (`bish-arch`, `bish-audit-docs`, `bish-auto-card`, `bish-chat`, `bish-coverage`, `bish-grill-me`, `bish-onboard`, `bish-tests`, `bish-triage`, `bish-work-on-card`) are vendored under `skills/` in the Bishop.AI repo, and `bishop install-skills` copies them to `%USERPROFILE%/.claude/skills/`. Skills version with Bishop; CLI evolution and skill text update in the same commit.

### CLI surface is unversioned and additive-only
No `bishop v1 ...` prefix. Commands and flags are not renamed or removed once shipped — additive changes only, in the style of `gh`. Stated here explicitly so a future quietly-renamed flag doesn't silently break the skills.

### Installer is MSI
Bishop ships as an MSI that installs both `Bishop.UI.exe` and `bishop.exe`, places the CLI on user PATH, and removes the PATH entry on uninstall. No `bishop install-self`-style self-installer — let the Windows installer convention handle uninstall cleanly.

### Terminal launcher stays a dumb shell
Spawns `wt -d <workspace_path>` (with `claude` launched in the shell). No WebView2 / xterm.js / ConPTY embedding inside Bishop. No card context passed via env vars — skills pull what they need from the `bishop` CLI.

## Out of scope

Decided against, not deferred. Don't drift back into these without explicitly re-opening the question.

- **Pending-move review UX inside Bishop.UI.** Cut — see above.
- **Automatic CLAUDE.md seeding.** Cut — see above.
- **Embedded terminal.** Cut — see above.
- **Tabbed right pane.** The original idea was a tab host with kanban + (future tools) per workspace; with the layer-above-editor framing, the kanban *is* the workspace view and a tab host is unjustified weight.
- **Full kanban admin UI in Bishop.UI.** Light mutations are now in scope (card delete, drag-and-drop move, lane CRUD — see [CLI as primary mutation surface for skills and automation](#cli-as-primary-mutation-surface-for-skills-and-automation)); a fully featured editing UI that replaces the CLI as the mutation surface is not.
- **General-purpose file viewer / browser inside Bishop.** Editor territory; VS Code + Claude extension already owns it.
- **Plugin system / extensibility points.** Future tabs and tools ship as in-tree code if they ship at all.
- **Full GitHub Issues / Projects sync.** Basic GitHub integration is shipped (`bishop workspace set-github`, `bishop card push`); bidirectional sync and automatic issue tracking are not in scope.
- **Cross-platform** (Mac / Linux). Windows-only by design.
- **Multi-user / cloud sync.**

## Deferred possibilities

Not cut, not committed — reconsider once the Claude Code integration has shipped and there's lived experience to draw on. Do not pre-build any of these.

### Simple text editor pane in Bishop.UI
Plain markdown / text editing of a workspace's `CLAUDE.md` (and maybe a small set of other canonical docs) directly inside Bishop. Distinct from a general file browser — narrowly scoped to project-context docs the human and Claude both reference. Revisit once the read-only viewer has been used in anger.

### Context Pane (mdpeek-shaped)
A narrowly-scoped **read-only** renderer for `CLAUDE.md` / `README.md` / `CONTRIBUTING.md` / `notes/*.md`. Discovery, not browsing — surface known paths only, no recursive file tree. Trigger to revisit: catching yourself opening VS Code purely to read project docs while Bishop is up.

If both end up wanted, the text-editor pane is the natural superset and the Context Pane becomes its read-only special case.

## Prior reasoning (archived)

Pre-decisional notes are preserved in `notes/_archive/` for the decision rationale they contain. They are **superseded by this file** for any forward-looking question:

- `notes/_archive/claude-code-integration.md` §3 (pending-move review) — superseded by [No pending-move review queue](#no-pending-move-review-queue).
- `notes/_archive/claude-code-integration.md` §5 (workspace `CLAUDE.md` seeding) — superseded by [No automatic CLAUDE.md seeding](#no-automatic-claudemd-seeding).
- `notes/_archive/context-pane.md` is the source of the [Context Pane](#context-pane-mdpeek-shaped) deferred possibility; the open questions in that file remain open and will be re-grilled if/when the feature is reconsidered.
