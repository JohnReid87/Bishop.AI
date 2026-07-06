# Bishop.AI — Direction

The destination this project is pointed at, and the scope decisions that get us there. Read this alongside [CONTEXT.md](CONTEXT.md), which describes what is *already shipped*; this file describes what we're *building toward* and what we've deliberately decided not to build.

Single source of truth for "is this in scope or not". When the answer changes, update this file in the same PR.

## Destination

Deep Claude Code integration on top of the kanban + `bishop` CLI scaffolding that already exists.

The kanban tracks work state per workspace. Skills (`bish-work-on-card`, `bish-grill-cards`, `bish-auto-card`) shell out to the `bishop` CLI to read and mutate that state. The user reviews everything material in the conversational loop. Bishop.UI is the interactive surface over the same state — a kanban view with a narrow set of direct mutations (see [Resolved scope decisions](#resolved-scope-decisions)). No agent observability dashboard, no embedded terminals, no plugin system.

The differentiator is the **layer above the editor** — cross-workspace orchestration + kanban as work-state source of truth + skill integration. The editor layer (VS Code + Claude extension) already exists; Bishop doesn't compete inside its box.

## Load-bearing assumptions

If any of these changes, expect the design to fall over.

- **Single-user, local-first, Windows-only.** No accounts, no servers, no sync. The `bishop` CLI is a privileged surface — anything that can invoke it as the user can mutate the DB. If a service account or multi-user mode ever appears, the security model needs revisiting from scratch.
- **Solo + Claude is the audience.** Not external contributors, not end users yet. Docs (CONTEXT.md, DIRECTION.md) exist to give Claude operating context and to remind future-me of decisions, not to onboard others.
- **Kanban is the work-state source of truth.** Not the agent session, not git history. Closing the terminal loses nothing because the card captures intent and progress.

## Resolved scope decisions

### CLI as primary mutation surface for skills and automation
The `bishop` CLI is the primary mutation surface for skills and automated workflows. Skills invoke `bishop` commands; the conversational loop is the human-in-the-loop gate.

**Decision updated:** the WinUI app now supports a narrow set of direct mutations — card detail dialog (with delete) and drag-and-drop card move. The previous stance was: *"The WinUI app is a read-only viewer of the same state. UI editability is possible later but not required for any committed milestone."* This held while the board was scaffolding; it no longer reflects what is actually being built. The CLI remains the automation surface; the UI is the interactive surface for the human user and handles these mutations directly rather than routing through the CLI.

### No pending-move review queue
Card moves apply immediately when the CLI is called. Human-in-the-loop gating happens at the **skill** level — `bish-work-on-card` prompts the user in the conversation before invoking `bishop card move ... --to-lane Done`. By the time the CLI runs, the move is already approved.

Cut because the original "Bishop surfaces pending move requests for review" design (notes/_archive/claude-code-integration.md §3) was overengineering for the workflow that actually emerged. Revisit only if non-interactive agent runs become a real use case.

### No automatic CLAUDE.md seeding
Bishop does not inject anything into workspace `CLAUDE.md` files. Skills are companion installs that carry their own knowledge of the `bishop` CLI surface.

Cut because (a) workspaces attached from existing repos (FotM.IO-style) already have authored `CLAUDE.md` files and auto-seeding risks collisions, (b) keeping ownership clean is cheap, (c) the skill is already loaded globally for the only consumer that matters.

### Skills live in this repo
The bundled skills are vendored under `skills/` in the Bishop.AI repo, and `bishop install-skills` copies them to `%USERPROFILE%/.claude/skills/`. Skills version with Bishop; CLI evolution and skill text update in the same commit. They group into six categories (see [docs/SKILL_FAMILY.md](docs/SKILL_FAMILY.md) for rationale):

- **Conversational:** `bish-grill-cards`, `bish-grill-docs`, `bish-scripts`, `bish-spec-cards`
- **Code:** `bish-arch`, `bish-dead-code`, `bish-security`
- **Tests:** `bish-coverage`, `bish-tests`
- **Review:** `bish-audit-docs`, `bish-review-batch`, `bish-triage`
- **Setup-Execute:** `bish-auto-card`, `bish-life-add`, `bish-life-init`, `bish-life-standup`, `bish-onboard`, `bish-work-on-card` (the `bish-life-*` skills operate on the bishop.life data file rather than a workspace)
- **Bishop-level / meta:** `bish-write-skill`, `bish-audit-skills` — operate on `skills/` itself rather than a workspace's code

### Automated Claude runs use bypassPermissions, with check-path as the containment layer

`ClaudeCliRunner` (used by `bishop batch run`) passes `--permission-mode bypassPermissions` to the Claude subprocess so unattended card work can run without stalling on per-tool approval prompts.

**History.** Card #981 removed `bypassPermissions` in favour of an allowlist in `.claude/settings.json`. In practice the harness's multi-operation parser refused chained or comma-array commands even when each constituent part matched an `allow` entry — so deletions, moves, and any compound shell invocation stalled the loop indefinitely. Three rounds of allowlist tuning could not close the gap, so the bypass was reinstated.

**Threat-model rationale for reinstating.** The realistic attack surface for `bish-auto-card` is narrow: the prompt is assembled from `CONTEXT.md` and recent commit subjects in the same repo the user is already developing against, and the subprocess runs under the user's own account with no elevated privileges. The remaining containment layers are still in force:

- `BISHOP_AUTO_CARD=1` activates `bishop hook check-path`, which blocks file writes outside the workspace root.
- `.claude/settings.json` `deny` list still applies under `bypassPermissions` (deny rules are honoured) — `git push`, `sudo`, `curl`, `gh`, `chmod`, etc. remain blocked.
- The host (`RunBatchCommandHandler`) performs the final `git commit` itself; the skill cannot push.

Net effect: the bypass eliminates the unattended-stall failure mode without giving the subprocess any capability it could not already obtain by writing files inside the workspace.

### CLI surface is unversioned and additive-only
No `bishop v1 ...` prefix. Commands and flags are not renamed or removed once shipped — additive changes only, in the style of `gh`. Stated here explicitly so a future quietly-renamed flag doesn't silently break the skills.

### No installer — build from source
Bishop has no packaged distribution. It is built from source and run directly on the one machine it serves; `bishop.exe` reaching `PATH` is the user's own arrangement, not an install step.

**Decision updated:** the previous stance was *"Bishop ships as an MSI that installs both `Bishop.UI.exe` and `bishop.exe`, places the CLI on user PATH, and removes the PATH entry on uninstall."* The Wix v5 MSI was deliberately abandoned — the `installer/` project no longer exists on `main` — because a packaged installer earns nothing for a solo, build-from-source workflow. Revisit only if Bishop ever needs to land on a machine that isn't the dev box.

### Terminal launcher stays a dumb shell (Bishop.AI only)
Spawns `wt -d <workspace_path>` (with `claude` launched in the shell). No WebView2 / xterm.js / ConPTY embedding inside Bishop.AI. No card context passed via env vars — skills pull what they need from the `bishop` CLI.

This decision is scoped to Bishop.AI, the dev tool. Bishop.Life deliberately went the other way — its stand-up session embeds `claude` via ConPTY inside the WebView2 viewer (see [Bishop.Life](#bishoplife--sibling-product-in-the-same-repo)). The rationale for keeping Bishop.AI's launcher dumb (the editor layer already exists; Bishop doesn't compete inside its box) doesn't apply to Bishop.Life, where the Claude session *is* the product surface.

## Bishop.Life — sibling product in the same repo

bishop.life is a sister product living in the `life/` peer (with `bishop/` holding the dev tool): a daily-reflection tool over a single `bishop.life.json` file, driven by the `bish-life-*` skills and viewed through a WinUI 3 + WebView2 shell. The original design is [docs/bishop-life-spec.md](docs/bishop-life-spec.md) — **treat that spec as historical**: it governed the initial build (cards #1003–#1008) and the product has deliberately moved past it. Where the spec and the code disagree, the code wins; CONTEXT.md's "Bishop.Life host↔viewer wire contract" section describes the shipped shape.

Scope decisions made since the spec, recorded here so they don't read as drift:

- **The stand-up session is embedded, not launched.** The spec's "shell launches Windows Terminal" model was replaced by a ConPTY-hosted `claude` session inside the viewer (cards #1053+), with keystrokes routed from the page and the transcript rendered as HTML from the session JSONL. The "dumb shell" decision above does not apply here.
- **Speech is part of the ritual.** TTS output (`bishop life speak`, the `speak-on-stop` Stop hook, prelude phrases) and the oscilloscope visualizer are shipped surface, tuned for a user who dictates while doing other things.
- **Google Calendar is a read-only context source.** `bishop life auth google` (installed-app OAuth, DPAPI-stored refresh token) feeds upcoming events into the stand-up context pack. This supersedes the spec's blanket "no login, sync, or remote surface" — the boundary is now: *read-only* external context is allowed; no remote storage or sync of `bishop.life.json` itself.
- **The viewer SPA is TypeScript** (`life/src/Bishop.Life.App/Assets/js/*.ts`), with `schema.d.ts` generated from the C# schema records by `Bishop.Life.SchemaCodegen` on each build.
- **Decoupling holds.** `Bishop.Life.*` projects still do not reference `Bishop.App` / `Bishop.Core` (and vice versa); the shared-chassis lift into a `Bishop.Shared` remains a future option, not a commitment.

## Out of scope

Decided against, not deferred. Don't drift back into these without explicitly re-opening the question.

- **Pending-move review UX inside Bishop.UI.** Cut — see above.
- **Automatic CLAUDE.md seeding.** Cut — see above.
- **Embedded terminal in Bishop.AI.** Cut — see above. (Bishop.Life's embedded ConPTY stand-up session is a separate, deliberate decision — see [Bishop.Life](#bishoplife--sibling-product-in-the-same-repo).)
- **Tabbed right pane.** The original idea was a tab host with kanban + (future tools) per workspace; with the layer-above-editor framing, the kanban *is* the workspace view and a tab host is unjustified weight.
- **Full kanban admin UI in Bishop.UI.** Light mutations are now in scope (card delete, drag-and-drop move — see [CLI as primary mutation surface for skills and automation](#cli-as-primary-mutation-surface-for-skills-and-automation)); a fully featured editing UI that replaces the CLI as the mutation surface is not.
- **General-purpose file viewer / browser inside Bishop.** Editor territory; VS Code + Claude extension already owns it.
- **Plugin system / extensibility points.** Future tabs and tools ship as in-tree code if they ship at all.
- **GitHub Issues / Projects sync.** Removed in card #973. Imported issue descriptions were a prompt-injection surface flowing into LLM-driven skills, and the feature was effectively unused. Not coming back.
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
