# Bishop.AI — Context Pane — Pre-Grill Notes

Seed material for a future `/grill-me` session on whether (and how) Bishop should surface project-context markdown files (`CLAUDE.md`, `README.md`, etc.) inside a workspace. Spawned from a "should we build something mdpeek-shaped into Bishop?" discussion.

These are **pre-decisional notes**, not specs. Re-test before they harden.

## Premise

While prototyping the MVP it became tempting to add an in-app markdown viewer (mdpeek-style) so the user can read a workspace's context documents without leaving Bishop. The concern: this risks turning Bishop into a worse VS Code, which already does this via the file explorer + Claude extension.

## Working position

**Don't build a general file viewer.** That's editor territory and the VS Code + Claude extension combination already owns it. Building a worse version inside Bishop trades the project's actual differentiator (cross-workspace orchestration + kanban + skill integration) for a duplicate of an editor feature.

**A narrowly-scoped "Context Pane" is potentially worth it later** — but only after the kanban + `bishop` CLI ship and we know whether users actually reach for project docs from inside Bishop.

### Differentiator framing

- VS Code + Claude extension = the **editor layer**, per workspace.
- Bishop = the **layer above** — cross-workspace orchestration, kanban as work-state source of truth, skill integration via the `bishop` CLI + pending-move-review pattern.

Don't compete inside the editor's box.

### If a Context Pane is ever built, the shape

- **Discovery, not browsing.** Surface known paths only: `CLAUDE.md`, `README.md`, `CONTRIBUTING.md`, `ARCHITECTURE.md`, `notes/*.md`, `docs/*.md`. No recursive file tree.
- **Read-only render.** Markdig + a WinUI markdown renderer.
- **"Open in editor" per file.** Shells out (`code <file>`, or whatever the user's editor is). No in-app editing.
- **Panel, not main view.** Lives as a tab (when the tab host comes back) or a secondary panel. Never the primary right-pane content.
- **Reasoning:** gives the human the same orientation surface Claude already has via `CLAUDE.md` auto-load. About workspace *intent*, not file manipulation.

## Why this is deferred (not killed)

Three reasons it might *feel* compelling right now that don't justify shipping it now:

1. **MVP feels too small to be exciting.** Ship the boring MVP first; see what's missing in practice.
2. **mdpeek code reuse.** Lifting the renderer later is trivial. Reuse isn't a reason to ship the feature.
3. **It's plausibly useful.** Possibly — but competes for time with the kanban + `bishop` CLI work, which is load-bearing for the project's actual differentiator.

## Trigger to revisit

Reconsider once both are true:

- MVP (workspace + terminal launch + kanban + `bishop` CLI) has shipped and been used for at least a couple of weeks.
- You catch yourself wanting project-doc context *inside Bishop* — not "would be nice", but "I'm actively switching to VS Code just to read `CLAUDE.md` and that's friction".

If the second never materialises, the feature was never needed.

## Open questions for /grill-me

1. **Scope of "context docs"**. Just the canonical set above, or user-configurable globs per workspace? The latter starts looking like a file browser again.
2. **Edit affordance.** Strict read-only, or allow a "quick edit" inline for `CLAUDE.md` specifically (since that's the doc Bishop most directly cares about)? Editing pulls in dirty-state, save UX, undo, formatting — slope is steep.
3. **Where does it live in the UI.** Tab in the right pane (once tab host returns), persistent side panel, or a dedicated "Workspace overview" landing tab that combines context docs + kanban summary?
4. **Auto-discovery vs explicit list.** Scan the workspace for known filenames, or maintain a list of "tracked context docs" per workspace in the DB? Auto-discovery is zero-config but surfaces whatever's there; explicit list is curated but adds management UI.
5. **Relationship to `bishop` CLI.** Should skills be able to *write* to context docs via the CLI (e.g. `bishop context append --doc CLAUDE.md "<text>"`)? If yes, the read-only model needs revisiting.
6. **Reuse of mdpeek.** Pull the renderer/discovery code in as a library, vendor it, or just reimplement against Markdig directly inside Bishop? Depends on how coupled mdpeek's internals are to its own UI.
