# Bishop.AI — Overview

A narrative-first introduction to what Bishop is, who it's for, and how you'd use it. For implementation detail see [CONTEXT.md](CONTEXT.md); for scope decisions and cuts see [DIRECTION.md](DIRECTION.md).

## What Bishop is, who it's for, and the mental model

Bishop is a Windows desktop app for managing AI-assisted coding work across many local repositories.

If you've ever:

- Lost track of which workspace had which in-progress feature.
- Found Claude Code's context reset between sessions frustrating because the design lived in the chat.
- Wished kanban work-tracking sat next to your editor rather than in a separate Linear / GitHub tab.
- Wanted a repeatable way to interrogate, plan, and ship work with Claude as the implementer.

…Bishop is the layer-above-editor that holds those threads together for a single solo developer on Windows.

### Mental model

The shape, in four nouns:

**Workspaces** — local directories you've registered with Bishop. Each one bound to a folder on disk; optionally linked to a GitHub repo. The left-hand nav of the UI.

**Kanban** — every workspace owns four fixed lanes (Backlog, To Do, Doing, Done). Cards belong to a workspace; tags are workspace-scoped; lanes are not user-mutable. The kanban is the **work-state source of truth** — not the agent chat, not git history. Closing the terminal loses nothing, because the card captures intent and progress.

**Skills** — Claude Code skills like `bish-grill-cards`, `bish-grill-docs`, `bish-work-on-card`, `bish-auto-card`. They shell out to the `bishop` CLI to read and mutate state. Triggered as slash-commands from a terminal, or as buttons on a workspace / card in the UI.

**CLI (`bishop`)** — the automation surface. Skills mutate Bishop state through `bishop <command>`. Humans use the UI directly for interactive edits. Both writers share a SQLite DB in WAL mode.

The differentiator is the **layer above the editor**: cross-workspace orchestration + kanban as work-state source of truth + skill integration. The editor layer (VS Code + Claude extension) already exists; Bishop doesn't compete inside its box.

## A day with Bishop

Concrete end-to-end flows.

### Plan a feature via `bish-grill-cards`

You have an idea for a feature in workspace `foo`. From Bishop you click the **Claude** button on `foo` — Windows Terminal opens at the workspace path with `claude` already running. You type `/bish-grill-cards`.

The skill interviews you relentlessly — one question at a time, with a recommended answer first, never batched. It walks the decision tree, resolves dependent sub-decisions before moving on, and applies a granularity pass before producing a card preview. You say `push` and the cards land in `To Do` at the bottom of the lane, in agreed order. Done.

### Work a single card

A card in `To Do` is ready. From the same workspace terminal you type `/bish-work-on-card 42`. The skill:

1. Fetches the card body via `bishop card view 42`.
2. Moves it to `Doing` via `bishop card move`.
3. Explores the relevant code (often via the Explore subagent) and asks any clarifying questions.
4. Implements the change.
5. Prompts you before moving the card to `Done` and committing with a `(card 42)` reference in the commit message.

If the work fits cleanly into a single PR, that's the whole loop.

### Batch multiple cards in a worktree

You have a cluster of small related cards. You provision a worktree:

```
bishop batch create --name my-batch --cards 41,42,43
```

Then run them unattended:

```
bishop batch run my-batch
```

`bish-auto-card` runs each card in turn — implementing, building, and testing — and stops on the first failure. When all cards reach `Done`, you complete the batch:

```
bishop batch complete my-batch
```

The CLI merges the batch branch into local `main` with `--no-ff`, closes the Done cards (and their linked GitHub issues, if any), and marks the batch closed. It never pushes and never calls `gh`. You push when you're ready.

### Review the codebase

Periodically you want a second pair of eyes. From inside a workspace terminal:

- `/bish-arch` — architectural / code-quality review (SOLID, layer hygiene, DI lifetimes, stack-conditional heuristics).
- `/bish-security` — security audit (injection, secrets, weak crypto, missing authn/authz, dependency CVEs).
- `/bish-coverage` — coverage scan; raises cards for under-covered classes.
- `/bish-tests` — test-quality audit.
- `/bish-triage` — turn a free-text bug description into a structured `bug` card.

Each one walks findings with you one at a time and pushes agreed cards tagged appropriately.

## What "Bishop is working" looks like

No metrics, no KPIs — qualitative measures of "did this stay in the loop":

- A solo dev can grill a design, plan, work, and merge a card end-to-end without leaving the Bishop + Claude + terminal loop.
- The board reflects current state. If it's not on a card, it's not happening; and the dev trusts the board enough to use it for "what's next?" decisions.
- Context loss between Claude sessions is absorbed by the cards. A fresh agent picks up `bishop card view 42` and produces something close to what the original conversation would have, without re-asking the same clarifying questions.
- Registering a new workspace and getting it productive (path, GitHub link, skills installed) takes minutes, not hours.
- The morning loop is: open Bishop, see what's in `Doing`, pick the next card from `To Do`, get to work — no context-loading from a tool other than Bishop.

## Where the parts live

A guided tour for orientation. Dependency direction is **Core → Data → App → { ViewModels → UI, Cli }**.

- **Domain types** (Workspace, Lane, Card, Tag): `src/Bishop.Core/`
- **Persistence** (EF Core 9, SQLite WAL, migrations, repos): `src/Bishop.Data/`
- **Application** (MediatR handlers, terminal launcher, validators): `src/Bishop.App/`
- **ViewModels** (presentation-framework-agnostic, `IUiDispatcher` lives here): `src/Bishop.ViewModels/`
- **UI** (WinUI 3 desktop app, Views, DI composition root): `src/Bishop.UI/`
- **CLI** (the `bishop` console executable — the automation surface): `src/Bishop.Cli/`
- **Tests** (xUnit + FluentAssertions): `tests/Bishop.Tests/`
- **Bundled skills** (installed to `~/.claude/skills/` via `bishop install-skills`): `skills/`
- **Installer** (Wix v5 per-user MSI): `installer/`

UI and CLI both go through MediatR handlers in App for everything; neither references Data directly. For the full CLI surface and architectural conventions, see [CONTEXT.md](CONTEXT.md).

## What Bishop is not

A short list — the canonical version with rationale lives in [DIRECTION.md → Out of scope](DIRECTION.md#out-of-scope).

- **Not cross-platform.** Windows-only by design.
- **Not multi-user.** No accounts, no servers, no sync.
- **Not a full editor.** VS Code + Claude extension is the editor layer; Bishop sits above it.
- **Not a plugin host.** Future tabs ship in-tree if they ship at all.
- **Not a full GitHub Issues / Projects mirror.** Basic push is shipped; bidirectional sync is not.
