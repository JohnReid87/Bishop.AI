# Bishop.AI — Brand & Design Rules

Single source of truth for Bishop's visual identity. Future contributors
(human or LLM) should reference this file before introducing any colour,
icon, or asset.

## Palette

| Role          | Hex        | Notes                                          |
| ------------- | ---------- | ---------------------------------------------- |
| bg            | `#0a0a0a`  | App background                                 |
| surface       | `#141414`  | Cards, lanes, dialogs                          |
| border        | `#2a2a2a`  | Dividers, outlines                             |
| text          | `#e8e8e8`  | Primary text                                   |
| text-muted    | `#888888`  | Secondary text, muted/offline state            |
| accent        | `#00ff41`  | Primary action, focus, selection               |
| accent-hover  | `#00b830`  | Accent on hover / pressed                      |
| error         | `#ff5555`  | Error & destructive states only — see carve-outs |

### Carve-outs

- **Red `#ff5555`** is reserved for true error / destructive states
  (validation failures, delete confirmations, failed runs). Do not use
  red for warnings, badges, or decoration.
- **Grey `#888888`** is the muted / offline indicator (disabled buttons,
  dimmed text, "unavailable" affordances).

## Tag greens (hue ramp)

The seven workspace tags share a green family that hue-ramps from
teal-green through pure green to lime, so they read as a coordinated
set. The ramp deliberately steps around the accent value so no tag
swatch is indistinguishable from an accented UI element.

| Tag       | Hex        |
| --------- | ---------- |
| `arch`    | `#00ffaa`  |
| `bug`     | `#00ff8a`  |
| `chore`   | `#00ff6a`  |
| `docs`    | `#1bff50`  |
| `feature` | `#3fff35`  |
| `spike`   | `#66ff20`  |
| `test`    | `#9aff15`  |

Listed alphabetically by tag name. Add new tag colours by inserting
them into this ramp (interpolate, do not pick arbitrary greens).

## Icons

- **Segoe MDL2 Assets** for all in-app actions (buttons, menu items,
  toolbar glyphs, inline affordances). Standard system font, no
  per-glyph asset to maintain.
- **Brand SVGs** are reserved for:
  - the taskbar / app icon
  - window chrome (title bar, system menu)
  - empty-state illustrations

Do not introduce custom SVGs for in-app actions — use Segoe MDL2.

## Asset directory

Brand SVGs and related image assets live at:

```
src/Bishop.UI/Assets/Brand/
```

Anything outside this directory is not considered a brand asset.
