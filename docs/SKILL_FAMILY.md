# Bishop Skill Family — Authoring Heuristics

Captured from the grill on card #218 ("full review of the skills to card handling hand off process around token and performance efficiency, duplication of card handling processes in every skill"). The follow-up work (cards #257–#263) implements what is described here; this doc preserves the reasoning so future audits and new skills do not re-derive it.

Read this once before:

- writing a new skill (especially via `bish-write-skill`),
- restructuring an existing skill,
- auditing the skill family for drift.

---

## 1. Skill categories

Skills divide into six categories. The category determines the restructure approach (see §2) and what may be extracted to shared surfaces (see §3). Code, Tests and Review are three flavours of the same underlying *Review* shape — a heuristic catalogue walked finding-by-finding — split out because the flyout was unreadable with seven skills under one header.

| Category | Current members | What the skill *is*, fundamentally |
|---|---|---|
| **Conversational** | `bish-grill-cards`, `bish-grill-docs` | An interview style — relentless interrogation. The soul is the *quality bar of the conversation*. |
| **Code** | `bish-arch`, `bish-dead-code`, `bish-security` | Heuristic catalogue applied to production C# — structure, dead code, security. The soul is the *heuristic catalogue*. |
| **Tests** | `bish-coverage`, `bish-tests` | Heuristic catalogue applied to the test surface — line coverage gaps, mutation/quality. The soul is the *heuristic catalogue*. |
| **Review** | `bish-audit-docs`, `bish-triage` | Heuristic catalogue applied to artefacts that aren't production code or tests — docs drift, bug-skeleton walks. The soul is the *heuristic catalogue*. |
| **Setup-Execute** | `bish-onboard`, `bish-auto-card`, `bish-work-on-card` | A deterministic procedure that mutates state (filesystem, board, git). The soul is the *procedure itself*. |
| **Bishop-level / meta** | `bish-write-skill`, `bish-audit-skills` | Skills *about* the skill family — authoring guides, audits. Operate on `skills/` directly, not on a workspace's code. |

The Conversational / Code / Tests / Review / Setup-Execute split is **workspace-level** — each skill targets the user's current Bishop workspace. Bishop-level skills are distinct: they treat the Bishop repository itself (or any `skills/` directory) as their subject. Keep them separate so workspace-level skills do not accumulate self-referential plumbing.

Code, Tests, and Review share the same restructure rules (see §2 "Review" row) — they are split only for menu navigability, not because their authoring contract differs.

---

## 2. Per-category restructure triage

Where the SKILL.md leads matters more than the total line count. Leading with mechanics buries the soul; leading with the soul orients the agent (and the human reader).

| Category | SKILL.md leads with | Why |
|---|---|---|
| **Conversational** | Quality bar / contract ("what relentless means", "bug-skeleton walk") | The plumbing is generic; the conversation is the entire value. Burying the soul under workspace-detection prose makes the agent treat it as one more procedural step rather than a stance. |
| **Code / Tests / Review** | Heuristic catalogue (SOLID checks, CVE patterns, test-quality dimensions, coverage thresholds, doc-drift classifications) | The heuristics ARE the deliverable. Mechanics (workspace detection, per-finding walk, card push) are a thin frame around them — extract aggressively, but do not reorder the heuristics. Same authoring contract across all three buckets. |
| **Setup-Execute** | The procedure, top to bottom | The procedure IS the value. Re-ordering for "purpose-first" obscures the imperative flow that the agent must execute literally. Extract only the workspace-detection preamble; leave the rest in step order. |
| **Bishop-level / meta** | The authoring contract / audit checklist | These skills emit guidance or check conformance — leading with the contract is leading with the soul. |

**Rule of thumb:** if a paragraph could be in any skill in the family, it is plumbing — extract it. If it would lose meaning outside this skill, it is soul — keep it inline and prominent.

---

## 3. Boilerplate inventory (snapshot at card #218)

Counts taken from `skills/*/SKILL.md` at the time the grill closed. Refresh before any future audit; the absolute numbers will drift, but the *pattern* (one shareable preamble dominating many skills) is the point.

| Repeated block | Skills carrying it | Approximate size per copy |
|---|---|---|
| Workspace detection preamble (`bishop workspace current --json` + STOP-message) | 11 | ~13 lines + a 4-line STOP message |
| Card push procedure (`bishop card create` temp-file flow + `--bottom` + `--description-file <path>`) | 7 | ~10 lines |
| Task-list preview format (H3 cards, Tag/Lane line, body sections, `---` separators) | ~7 | ~12 lines |
| Source-card closing prompt (close / done / leave + CLI mapping) | 3 | ~15 lines |

Total extractable mass when summed across all skills is in the low hundreds of lines — every invocation of every skill pays those tokens whether or not the surrounding logic ever runs. The refactor (cards #257–#261) reclaims them.

---

## 4. Extraction taxonomy — where each block belongs

Three destinations, picked by the *nature* of the block, not by its length.

| Nature of block | Destination | Why | Example |
|---|---|---|---|
| **Deterministic mechanics** — a step the agent has no judgment on, with a single correct invocation | C# binary (`bishop skill ...` subcommand) | Testable, version-controlled, single source of truth. Removes a token-cost-per-skill-per-invocation. | Workspace + tag + lane resolution → `bishop skill bootstrap --json` (card #257). |
| **Conversational scaffolding** — phrasing, granularity rules, confirmation prompts that the agent must read but can reference rather than carry | `BISHOP_CONTEXT.md` named section, referenced from SKILL.md | The agent already loads BISHOP_CONTEXT on orientation. Linking from many skills to one section means edits propagate without re-publishing skills. | Card push procedure, task-list preview format, source-card closing prompt, card granularity rules, per-finding walk pattern (card #258). |
| **Behavior contracts** — invariants the agent must not paraphrase or improvise around | STABLE label in BISHOP_CONTEXT.md, OR binary enforcement when the contract is mechanical | Labeling tells the agent "this wording is load-bearing, do not paraphrase." Binary enforcement makes paraphrasing impossible. | "Insert at bottom of lane during preview, then push" — STABLE in BISHOP_CONTEXT. "Card number must exist before move" — enforced by `bishop card move` exit code. |

**Anti-pattern:** lifting heuristic content (SOLID checks, CVE patterns, test-quality dimensions) into BISHOP_CONTEXT. Heuristics are skill-specific — each review skill's catalogue *is* its differentiator. Keep them inline.

### Context-pack entry-point pattern

The legacy entry-point fan-out — `bishop skill bootstrap`, then a handful of `bishop context print --section "<name>"` calls scattered through the SKILL.md prose — re-bills every tool-call result on each subsequent turn at uncached rates. The `bishop context-pack <skill-name>` command collapses that fan-out into one cached prompt prefix.

Workspace-level skills (Conversational / Review / Setup-Execute) open with a single `bishop context-pack <skill-name> [--card N]` call and a one-line intro stating what the pack contains. The pack bundles:

- `workspace` — name, path, GitHub repo, lanes, tags, and `CONTEXT.md` content when size permits.
- `git` — current branch and recent commits.
- `skill_specific` — provider-built block (e.g. the loaded card body for `work-on-card` / `auto-card`).
- `conventions` — the STABLE/TUNABLE sections sliced from `BishopContext.static.md` by the provider's `RequiredSections`.

Inline `bishop context print --section "<name>"` links in skill prose duplicate what the pack already delivers. They are flagged by `bish-audit-skills`. `bishop context print` itself stays available for ad-hoc human / debug inspection — only the in-skill references are removed.

A provider is one C# class implementing `IContextProvider` (in `Bishop.App.Context.ContextPack`). Adding skill-specific context for a new skill is a one-class change registered in DI; the framework supplies the common blocks automatically.

**Exceptions — skills that do NOT call `bishop context-pack`:**

| Skill | Reason |
|---|---|
| `bish-onboard` | The workspace doesn't exist yet at skill entry — `bishop context-pack` requires a registered workspace to resolve. |
| `bish-audit-skills` | Meta-skill; operates on `skills/` itself, not a workspace's code. No workspace context is relevant. |
| `bish-write-skill` | Meta-skill; same rationale as `bish-audit-skills`. |

`bish-audit-docs` is a partial exception: it calls `bishop context-pack audit-docs` with a soft fallback (`git rev-parse --show-toplevel`) for repos that aren't Bishop workspaces, because the skill is useful outside the Bishop toolchain.

---

## 5. STABLE vs TUNABLE labelling

Every shared section in BISHOP_CONTEXT.md is labelled STABLE or TUNABLE. The label tells the agent whether the wording is part of the contract or part of the voice.

- **STABLE** — behaviour contract. The agent must not paraphrase, reorder, or skip steps. Editing requires reasoning about every skill that points at it.
  - *Examples:* card push procedure, task-list preview format, source-card closing prompt. Skills depend on these matching exactly so that user-visible output stays consistent across the family.
- **TUNABLE** — voice / copy. The agent may paraphrase to fit context, and edits can land without auditing all consumers.
  - *Examples:* card granularity rules ("one PR ≈ one card"), per-finding walk cadence. Useful guidance; not a contract.

Section headers carry the label inline so the agent sees it during orientation:

```markdown
## Card Push Procedure (STABLE)
## Task List Preview Format (STABLE)
## Source Card Closing Prompt (STABLE)
## Card Granularity Rules (TUNABLE)
## Per-finding Walk Pattern (TUNABLE)
```

A short STABLE/TUNABLE convention note belongs at the top of BISHOP_CONTEXT.md so the labels are interpretable on first read (card #258).

---

## 6. Audit checklist

Run through this list when auditing the skill family (the future `bish-audit-skills` skill walks it with the user).

For each skill in `skills/`:

1. **Category** — is the skill correctly tagged Conversational (`discuss`) / Code (`code`) / Tests (`tests`) / Review (`review`) / Setup-Execute (`setup` or `execute`) / Bishop-level (`meta`)? Frontmatter `bishop.category` must match one of those values.
2. **Leading content** — does the SKILL.md lead with the category-appropriate content (see §2)? If a Conversational skill leads with workspace-detection, flag it.
3. **Workspace detection** — does the skill use one of the two accepted patterns: `bishop context-pack <skill-name>` (preferred) or `bishop skill bootstrap` (legacy, valid for skills not yet migrated to context-pack)? No inline `bishop workspace current --json` preamble. The only documented exception is `bish-onboard` (the workspace does not yet exist when it runs); the exception must carry a one-line comment explaining why. Bishop-level / meta skills must not call bootstrap or context-pack. If inline workspace-detection prose remains, flag it.
4. **Card push** — if the skill pushes cards, does it reference the BISHOP_CONTEXT `Card Push Procedure (STABLE)` section rather than restating the write→push→remove temp-file procedure?
5. **Task-list previews** — if the skill emits a multi-card preview, does it reference `Task List Preview Format (STABLE)`?
6. **Source-card closing** — if the skill spawns child cards from a source card, does it reference `Source Card Closing Prompt (STABLE)`?
7. **STABLE sections** — does the skill paraphrase a STABLE section anywhere? If yes, replace with a reference.
8. **Heuristic content** — for review skills: are the heuristics still the dominant portion of the file? If extraction has eroded them, restore.
9. **Procedural flow** — for setup/execute skills: is the procedure still readable top-to-bottom as imperative steps? Reordering for "purpose-first" is a bug here.
10. **Frontmatter** — `name`, `description`, `allowed-tools`, `bishop.scope`, `bishop.command` all present and accurate? Stage flags (`bishop.stage`, `bishop.stage_prompt`) correct for the workspace-launch path?
11. **Context-pack entry-point** — workspace-level skills (Conversational / Review / Setup-Execute) open the body with a single `bishop context-pack <skill-name> [--card N]` call and a one-line intro about the pack's contents. Any inline `bishop context print --section "<name>"` reference in the SKILL.md prose is drift — the pack already delivers those sections. Bishop-level / meta skills do not call context-pack. See §4 ("Context-pack entry-point pattern") for the rationale.

Finally, run this family-wide check (not per-skill):

12. **Bundled-skills-list drift** — every skill in `skills/bish-*/` must appear at least once in each of these four canonical bundled-skills lists, grouped by category rather than as a flat list:
    - `README.md` — "Getting started → After MSI install" bullet group.
    - `CONTEXT.md` — both the `skills/` entry under "Repository layout" and the "Skill integration" paragraph.
    - `DIRECTION.md` — the "Skills live in this repo" decision block.
    - `src/Bishop.App/Terminal/BishopContext.static.md` — the `## Workflow` section's `### *** skills` sub-headings.

    Each doc must group skills under labels that map 1:1 to the canonical six categories — either the doc-level names (Conversational / Code / Tests / Review / Setup-Execute / Bishop-level / meta) or equivalent labels that correspond 1:1 to the frontmatter `bishop.category` values (`discuss` / `code` / `tests` / `review` / `setup` and `execute` (as separate values) / `meta`). Flag:
    - **Missing skill** — a `skills/bish-*/` directory not named in one of the four docs.
    - **Missing category** — one of the four categories has no heading or label in a doc that otherwise lists skills.
    - **Stale flat list** — a doc lists skills inline without any category grouping at all.

Flag findings (per-skill for items 1–11, family-wide for item 12) and walk them with the user before pushing follow-up cards (the standard per-finding cadence used by `bish-arch` / `bish-security`).

---

## Source

- Card #218 — `full review of the skills to card handling hand off process around token and performance efficiency, duplication of card handling processes in every skill`. The grill that produced this triage. Left open as a marker until the refactor lands.
- Inspiration: Matt Pocock's `write-a-skill` — <https://github.com/mattpocock/skills/blob/main/skills/productivity/write-a-skill/SKILL.md>.
