---
name: bish-life-init
description: Bootstraps `%APPDATA%/Bishop/life/bishop.life.json` with the bishop.life v1 schema — six seeded areas (Finances, Side projects, Career, Home, Health, Relationships) with muted area-palette colours, empty inbox and stand-ups. Refuses to overwrite an existing file. Use when the user wants to set up bishop.life for the first time, or invokes `/bish-life-init`.
allowed-tools: Read, Write, PowerShell, Bash
bishop.category: setup
---

> Exempt from `bishop context-pack` (per `docs/SKILL_FAMILY.md` §4): this skill operates on bishop.life's data file, not on a Bishop workspace. No workspace context is relevant.

Shell tool selection (Bash vs PowerShell) — this skill targets a Windows-only path (`%APPDATA%`). Use `PowerShell` throughout.

<what-to-do>

### Step 1 — Resolve the canonical path

- If `$env:BISHOP_LIFE_FILE` is set and non-empty, use that absolute path verbatim.
- Otherwise, the canonical path is `Join-Path $env:APPDATA 'Bishop\life\bishop.life.json'`.

Capture the resolved path as `$path`.

### Step 2 — Refuse-to-overwrite check

Run `Test-Path -LiteralPath $path`.

- If it returns `True` → print exactly:

  > `bishop.life already initialised at <path> — re-running is a no-op. Delete the file by hand if you want a fresh seed.`

  Then STOP. Do not write, do not back up, do not prompt.

### Step 3 — Ensure the parent directory exists

```
$dir = Split-Path -Parent $path
if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
```

### Step 4 — Compose the seed JSON and write it

Use the `Write` tool to create the file at `$path`. The file must be UTF-8 without BOM (the `Write` tool's default) and indented with two spaces for readable diffs (per spec §4).

Set `meta.createdAt` to the current UTC instant in ISO 8601 (`YYYY-MM-DDTHH:MM:SSZ`). Leave `meta.lastStandupAt` as `null` — no stand-up has happened yet.

Exact content (substitute the timestamp):

```json
{
  "schema": "bishop.life/v1",
  "meta": {
    "createdAt": "<UTC-NOW>",
    "lastStandupAt": null
  },
  "areas": [
    { "id": "area-finances",      "name": "Finances",      "color": "#a8b3c4", "goals": [] },
    { "id": "area-side-projects", "name": "Side projects", "color": "#b8a8c4", "goals": [] },
    { "id": "area-career",        "name": "Career",        "color": "#c4b8a8", "goals": [] },
    { "id": "area-home",          "name": "Home",          "color": "#a8c4b8", "goals": [] },
    { "id": "area-health",        "name": "Health",        "color": "#c4a8a8", "goals": [] },
    { "id": "area-relationships", "name": "Relationships", "color": "#c4c4a8", "goals": [] }
  ],
  "inbox": [],
  "standups": []
}
```

The six area colours are the muted area palette (spec §7). They sit in the same mid-saturation / mid-luminance band as the brand's tag family (per `BRAND.md`) but are intentionally distinct hex values — tag hexes are reserved for tag chips (`BRAND.md` → "Tag-palette reservation").

### Step 5 — Confirm

Print:

> `Seeded bishop.life at <path>. Run /bish-life-standup to start your first stand-up.`

</what-to-do>

<guardrails>

- Do NOT overwrite an existing `bishop.life.json`. If the file exists, the skill is a no-op — do not back it up, do not merge, do not prompt.
- Do NOT seed any goals, actions, inbox items, or stand-ups. The card's contract is a *bare* seed: schema + meta + six empty areas.
- Do NOT introduce additional area names or rearrange the six defaults. The order in spec §1 is canonical.
- Do NOT invent new area colours. The six hexes above are the muted palette; users rename areas freely, but the colour stays sticky to the `id` (spec §7).
- Do NOT call `bishop` CLI. This skill has no Bishop workspace dependency — running it from any Claude Code session anywhere on the machine must succeed.

</guardrails>
