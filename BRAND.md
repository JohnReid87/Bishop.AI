# Bishop.AI — Brand & Design Rules

Single source of truth for Bishop's visual identity. Future contributors
(human or LLM) should reference this file before introducing any colour,
icon, or asset.

The brand reads as a dark terminal cockpit lit by a single signal-green
indicator. The **tag palette** is the harmonic foundation — every other
colour is either neutral scaffold supporting it or the signal accent
sitting on top.

## Tag palette — harmonic foundation

The eight workspace tags are the colour family Bishop is tuned to. They
share a mid-saturation, mid-luminance character (~50% saturation, ~55%
lightness, hues spaced ~50° apart on the wheel) so the set sits calmly
on `#141414` surfaces and stays well clear of the `#00ff41` signal so
chips never read as "active" UI. Reserved hues — pure red (`#ff5555`,
errors only) and the bright signal green — are absent from the family.

| Tag        | Hex        | Reads as       |
| ---------- | ---------- | -------------- |
| `arch`     | `#6b8caf`  | slate blue     |
| `bug`      | `#c97a8a`  | muted rose     |
| `chore`    | `#9aa86a`  | olive          |
| `docs`     | `#5fa89c`  | soft teal      |
| `feature`  | `#7fa87a`  | sage green     |
| `security` | `#7878bc`  | slate-indigo   |
| `spike`    | `#9a7ab8`  | muted violet   |
| `test`     | `#c4a85f`  | muted gold     |

Listed alphabetically. All eight sit above the WCAG 0.179 relative-
luminance threshold, so the contrast converter renders **black** chip
text consistently across the set.

## Core palette — neutral scaffold

The dark, near-monochrome stage on which the tag family and the signal
accent are placed.

| Role            | Hex        | Notes                                                |
| --------------- | ---------- | ---------------------------------------------------- |
| bg              | `#0a0a0a`  | App background                                       |
| surface         | `#141414`  | Cards, lanes, board chrome, default surface          |
| dialog-surface  | `#14141A`  | Modal dialog backgrounds only — see surface tiers    |
| icon-surface    | `#141914`  | SVG app-icon backgrounds only — see surface tiers    |
| border          | `#2a2a2a`  | Dividers, outlines                                   |
| text            | `#FFFFFF`  | Primary text (branded wordmarks, etc.)               |
| accent (signal) | `#00ff41`  | Focus ring, selection indicator, primary CTA         |
| accent-hover    | `#00b830`  | Signal on hover / pressed                            |
| error           | `#ff5555`  | Error & destructive states only — see carve-outs     |

### Carve-outs

- **Signal green `#00ff41`** is *not* a member of the tag family — it's
  the single signal colour sitting on top of the harmonic palette. See
  the accent rule under [Design rules](#design-rules).
- **Red `#ff5555`** is reserved for true error / destructive states
  (validation failures, delete confirmations, failed runs). Do not use
  red for warnings, badges, or decoration.

## State overlays

Alpha-blended whites and blacks layered onto the neutral scaffold. Hex
is shown in `#AARRGGBB` form (WPF / WinUI native). These are documented
as they appear in code today — they are defined as named brushes in
`Themes/Brushes.xaml`.

| Token                       | Hex          | Where it lives                                                                 |
| --------------------------- | ------------ | ------------------------------------------------------------------------------ |
| `overlay-selected`          | `#20FFFFFF`  | `ListViewItemBackgroundSelected` (workspace list, resting selected state)      |
| `overlay-selected-hover`    | `#30FFFFFF`  | `ListViewItemBackgroundSelectedPointerOver`                                    |
| `overlay-selected-pressed`  | `#40FFFFFF`  | `ListViewItemBackgroundSelectedPressed`                                        |
| `placeholder-text`          | `#44FFFFFF`  | TextBox / search-box placeholder labels (`WorkspaceDetailPage`)                |
| `text-tertiary`             | `#55FFFFFF`  | `AppTextTertiaryBrush` in `Themes/Brushes.xaml` — least prominent text             |
| `text-secondary`            | `#99FFFFFF`  | `AppTextSecondaryBrush` in `Themes/Brushes.xaml` — disabled / muted secondary text |
| `divider`                   | `#1AFFFFFF`  | `AppDividerBrush` in `Themes/Brushes.xaml` — thin separators on dark surfaces      |
| `scrim`                     | `#80000000`  | Full-content darkening overlay (e.g. cat-mode tint in `MainWindow.xaml`)       |

The selection scale (`#20` → `#30` → `#40`) is a deliberate ramp — each
state lifts ~6% luminance from the surface. Pick from the existing ramp
when adding new selection states rather than inventing intermediate
values.

## Design rules

### Tag-on-surface contrast

Every tag chip must remain legible against `surface: #141414`. Chip
**text colour** is chosen by relative luminance: tags whose luminance
exceeds the WCAG 0.179 threshold get **black** text; tags below it get
white. All eight canonical tags exceed the threshold, so the canonical
chip text colour is black. Custom tags must be checked against the same
threshold before they ship — see `LuminanceContrast` (or equivalent
converter) in the UI layer.

### Tag-palette reservation

Tag hex values must not be reused literally outside tag chips and
chip-derived UI (filter chips, tag lists, tag pickers). The eight hexes
above are **the** identifier for tags — repurposing one elsewhere makes
that surface read as a tag.

### Tag palette as tonal guide

When introducing any new colour anywhere in the app, match the tag
family's mid-saturation / mid-luminance character. The exact hex stays
unique (don't copy a tag value), but the tonal grammar — calm, muted,
sitting comfortably on `#141414` — carries over. New colours that
out-saturate or out-shine the tag family fight the brand.

### Accent (signal) usage

`#00ff41` is reserved for:

- focus rings,
- the active-selection indicator strip,
- primary call-to-action surfaces (single primary button per dialog).

It is **banned** from:

- large fills (background panels, full-height bars),
- running text or paragraph copy,
- decoration (separator lines, idle borders).

`#00b830` (`accent-hover`) is the only hover / pressed companion.

### Surface tiers

Three near-identical darks, one per role. Pick by what's being rendered,
not by aesthetic preference:

- **`surface: #141414`** — the default. All in-app surfaces (cards,
  lanes, board chrome, popovers, flyouts, notes panels) use this.
- **`dialog-surface: #14141A`** — modal dialog content roots only
  (`AddWorkspaceDialog`, `CardDetailDialog`, `SkillStageDialog`,
  `WorkNextOptionsDialog`). The subtle blue lift separates the dialog
  layer from the board behind it.
- **`icon-surface: #141914`** — SVG icon-tile backgrounds in
  `src/Bishop.UI/Assets/Brand/` (app icon, favicon, chat / code /
  terminal / run / branch / settings glyphs). The subtle green lift
  hints at the signal colour without using it.

Do not introduce a fourth dark variant. If a new surface needs a tier
of its own, justify it against the three above.

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

Anything outside this directory is not considered a brand asset. The
app ships dark-mode only — light-mode variants are not shipped and not
maintained.
