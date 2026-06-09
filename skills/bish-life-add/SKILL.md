---
name: bish-life-add
description: Short-form bishop.life inbox capture. Reads `bishop.life.json`, opens with one prompt ("what just came to mind?"), echoes each action sentence back for confirmation, then loops with "anything else?" until the user is done. Single atomic write at the end appends new `InboxItem` entries to `inbox[]` with ids of the form `ibx-<max+1>`. Triage happens later, in `/bish-life-standup` — this skill never touches areas, goals, or standups. Use when the user wants to capture an actionable point mid-day, or invokes `/bish-life-add`.
allowed-tools: Read, Write, PowerShell, Bash
bishop.category: setup
bishop.command: /bish-life-add
bishop.stage: false
---

> Exempt from `bishop context-pack` (per `docs/SKILL_FAMILY.md` §4): this skill operates on bishop.life's data file, not on a Bishop workspace. No workspace context is relevant.

Shell tool selection (Bash vs PowerShell) — this skill targets a Windows-only path (`%APPDATA%`). Use `PowerShell` throughout.

Design tenets carried from `docs/bishop-life-spec.md` §1:

1. **Inbox-only landing.** Capture goes into `inbox[]` as-is. Do NOT classify into area/goal here — the next stand-up triages. Keeping capture short is the point.
2. **Echo before write.** The user dictates; an echo-back catches phonetic misreads before they hit disk.
3. **Atomic single write.** Every successful run produces exactly one `.prev` backup and one rename. No partial writes; no per-item flushes.

<what-to-do>

### Step 1 — Resolve the canonical path

- If `$env:BISHOP_LIFE_FILE` is set and non-empty, use that absolute path verbatim.
- Otherwise, the canonical path is `Join-Path $env:APPDATA 'Bishop\life\bishop.life.json'`.

Capture the resolved path as `$path`.

### Step 2 — Refuse-to-run when uninitialised

Run `Test-Path -LiteralPath $path`.

- If it returns `False` → print exactly:

  > `bishop.life is not initialised at <path>. Run /bish-life-init first.`

  Then STOP. Do not prompt, do not write.

### Step 3 — Load the file

Read `$path` with the `Read` tool and parse it via PowerShell `ConvertFrom-Json` (per the repo's `CLAUDE.md` JSON convention). Keep the parsed object in memory — every mutation in this skill is applied to that object, then written out once at the end.

Schema guard: if `$plan.schema` is not `"bishop.life/v1"`, STOP and surface:

> `bishop.life schema mismatch — expected "bishop.life/v1", got "<schema>". Refusing to proceed.`

### Step 4 — Compute the next inbox id

Scan `$plan.inbox[].id` for entries matching `^ibx-(\d+)$`. Let `$next` be `max(captured-numbers) + 1`, or `1` if there are no matching ids. Hold `$next` in memory and increment it for each new entry minted in Step 5 — the file is not rewritten until Step 6.

### Step 5 — Capture loop

Ask exactly one open prompt:

> What just came to mind?

Read the user's response. For each captured item:

1. Echo the action sentence back verbatim and ask:

   > Capture as: "<text>" — keep? (`y` / paste a correction / `n` to drop)

   - On `y` (or empty input) → accept the text as typed.
   - On any other text → treat that text as the corrected sentence; do not re-confirm (one round of echo-back is enough).
   - On `n` → drop the item and continue.

2. Append the accepted item to the in-memory `inbox[]` as:

   ```json
   { "id": "ibx-<next>", "text": "<text>", "capturedAt": "<UTC-NOW>" }
   ```

   Capture `<UTC-NOW>` as `Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC` (or equivalent — match the timestamp shape already used elsewhere in the file). Increment `$next`.

3. Ask:

   > Anything else?

   - On a substantive response → loop back to step 5.1 with that text as the next captured item.
   - On `no` / `done` / empty input → exit the loop and proceed to Step 6.

If zero items were accepted (every echo was dropped, or the very first prompt got "nothing"), STOP without writing — do not produce a `.prev` backup for a no-op run.

### Step 6 — Atomic write

In this exact order (each step gated on the previous succeeding):

1. **Snapshot the pre-state** to `.prev`:

   ```
   Copy-Item -LiteralPath $path -Destination "$path.prev" -Force
   ```

2. **Serialise the mutated plan** to a `.tmp` sibling using the `Write` tool:

   - Target path: `"$path.tmp"`.
   - Content: the mutated plan, two-space indented, UTF-8 without BOM (the `Write` tool's default). Preserve the seed property order (`schema`, `meta`, `areas`, `inbox`, `standups`) and the existing area/goal/action/standup contents byte-equivalent — only `inbox[]` gains entries.

3. **Rename** `.tmp` over the live file:

   ```
   Move-Item -LiteralPath "$path.tmp" -Destination $path -Force
   ```

   The rename is the commit point (spec §5).

If any step fails, surface the error and STOP. Do not roll back from `.prev` automatically.

### Step 7 — Confirm

Print a single short line naming how many items landed, e.g.:

> `Captured 2 inbox items. Backup at <path>.prev — delete or restore by hand if you need to undo.`

</what-to-do>

<guardrails>

- Do NOT classify captures into areas or goals. Inbox is the only landing — triage belongs to `/bish-life-standup`.
- Do NOT mutate `areas`, `goals`, `actions`, `standups`, `meta`, or `schema`. The only field this skill writes is `inbox[]`, by append.
- Do NOT write directly to `$path`. Always write `.tmp` and rename — partial writes are how this file gets corrupted (spec §5).
- Do NOT skip the `.prev` snapshot when the run is non-empty. The single-step undo is a hard contract from spec §4.
- Do NOT produce a `.prev` backup or write `.tmp` when zero items were accepted. A no-op run touches nothing.
- Do NOT mint timestamp or GUID ids. Use `ibx-<max+1>` so the existing human-readable sequence on disk is preserved.
- Do NOT re-prompt past one echo-back per item. The ritual is "open prompt → confirm → next" — anything more turns capture into data entry.
- Do NOT call `bishop` CLI. This skill has no Bishop workspace dependency — running it from any Claude Code session anywhere on the machine must succeed.

</guardrails>
