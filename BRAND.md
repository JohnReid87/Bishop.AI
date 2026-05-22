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

## Tag palette

The seven workspace tags use a muted, hue-spaced palette (~50° apart on
the colour wheel, ~50% saturation, ~55% lightness). The palette sits
calmly on `#141414` surfaces and stays well clear of the `#00ff41`
accent so chips never read as "active" UI. Reserved hues — pure red
(`#ff5555`, errors only) and the bright brand green — are absent.

| Tag       | Hex        | Reads as     |
| --------- | ---------- | ------------ |
| `arch`    | `#6b8caf`  | slate blue   |
| `bug`     | `#c97a8a`  | muted rose   |
| `chore`   | `#a89878`  | warm taupe   |
| `docs`    | `#5fa89c`  | soft teal    |
| `feature` | `#7fa87a`  | sage green   |
| `spike`   | `#9a7ab8`  | muted violet |
| `test`    | `#c4a85f`  | muted gold   |

Listed alphabetically. All seven sit above the WCAG 0.179 luminance
threshold, so the contrast converter renders **black** chip text
consistently across the set. When adding a custom tag, pick a hue that
isn't already represented and keep saturation/lightness in the same
range so it blends with the canonical seven.

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
