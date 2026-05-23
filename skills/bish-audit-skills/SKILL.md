---
name: bish-audit-skills
description: Audit the Bishop skill family in `skills/` against the canonical patterns in `docs/SKILL_FAMILY.md` — category, leading content, workspace-detection, STABLE-section references, heuristic content, frontmatter — then walk findings with the user and push refactor cards. Use when the family has accumulated drift, after a structural change to the conventions, or when the user invokes `/bish-audit-skills`.
allowed-tools: Read, Glob, Grep, AskUserQuestion, Bash(bishop:*)
bishop.category: meta
---

## What this skill is

A **Bishop-level meta-skill**. It operates on `skills/` in the Bishop.AI
repository (or any directory the user nominates that already holds
`bish-*` skill directories), not on a workspace's application code.

The soul is the **audit checklist** — the same ten items a skill author
should self-check against (and that `bish-write-skill` runs as a
post-write check on a single file). This skill applies the checklist
at scale to every skill in the family and converts agreed violations
into refactor cards.

For the reasoning behind each checklist item, see
[`docs/SKILL_FAMILY.md`](../../docs/SKILL_FAMILY.md) — especially §1
(categories), §2 (per-category triage), §4 (extraction taxonomy), §5
(STABLE vs TUNABLE), and §6 (the checklist this skill walks). If this
file and `SKILL_FAMILY.md` disagree, `SKILL_FAMILY.md` is the source
of truth — flag the drift and STOP before walking the checklist.

This skill is the audit counterpart to `bish-write-skill`. Both are
Bishop-level meta skills; together they form the authoring/conformance
pair for the family.

---

## The audit checklist

Apply each item to every `skills/bish-*/SKILL.md` found under the
working directory. The notes below are short reminders; expand each
via §6 of `SKILL_FAMILY.md` when judging an edge case.

1. **Category** — frontmatter `bishop.category` is set and matches what
   the skill actually does (`discuss` for Conversational, `review`,
   `setup` or `execute` for Setup-Execute, `meta` for Bishop-level).

2. **Leading content** — the file leads with the category-appropriate
   content per §2: quality bar / contract for Conversational, heuristic
   catalogue for Review, the procedure for Setup-Execute, the
   contract / checklist for Bishop-level. A Conversational skill that
   opens with workspace-detection prose is a violation here.

3. **Workspace detection** — Conversational / Review / Setup-Execute
   skills call `bishop skill bootstrap`. No inline
   `bishop workspace current --json` preamble. The only documented
   exception is `bish-onboard` (the workspace does not yet exist when
   it runs); the exception must carry a one-line comment explaining
   why. Bishop-level / meta skills must **not** call bootstrap.

4. **Card push** — skills that push cards reference
   `BISHOP_CONTEXT.md → ## Card Push Procedure (STABLE)` rather than
   restating the `bishop card add` heredoc.

5. **Task-list preview** — skills that emit a multi-card preview
   reference `## Task List Preview Format (STABLE)` rather than
   restating the H3 / Tag / Lane / `---` format.

6. **Source-card closing** — skills that spawn child cards from a
   source card reference `## Source Card Closing Prompt (STABLE)`
   rather than restating the `close` / `done` / `leave` options.

7. **STABLE-section paraphrase** — the body does not paraphrase any
   STABLE section anywhere. Restated heredoc, restated preview format,
   restated closing prompt → finding.

8. **Heuristic content (Review skills only)** — the heuristic catalogue
   still dominates the file in line count and visual weight. If
   boilerplate extraction has eroded the heuristics, restore them.

9. **Procedural flow (Setup-Execute skills only)** — the procedure
   reads top-to-bottom as imperative steps. "Purpose-first" reordering
   is a finding for this category.

10. **Frontmatter completeness** — `name`, `description`, `allowed-tools`
    present and accurate. `bishop.scope`, `bishop.command`,
    `bishop.stage`, `bishop.stage_prompt` (when `stage: true`),
    `bishop.category` set per the skill's category and surface.

---

<what-to-do>

1. **Locate the `skills/` root.** Glob `skills/bish-*/SKILL.md` from
   the current working directory. If at least one match comes back,
   continue. If nothing matches, STOP and tell the user:

   > No `skills/bish-*/SKILL.md` found under the current working
   > directory. Run `/bish-audit-skills` from the Bishop.AI repo root
   > (or any directory containing a `skills/` folder with existing
   > `bish-*` skills).

2. **Confirm against `SKILL_FAMILY.md`.** `Read`
   [`docs/SKILL_FAMILY.md`](../../docs/SKILL_FAMILY.md). If §6 of
   that file disagrees with the ten-item checklist above, STOP and
   tell the user the doc and this skill have drifted. Do not paper
   over the disagreement — the checklist is the contract and one of
   the two files needs an update first.

3. **Audit each skill.** For each `SKILL.md` found in step 1:

   - `Read` the file.
   - Determine its category from frontmatter (`bishop.category`). If
     the field is missing, that is itself a finding under item 1 and
     also item 10; infer category from content to continue the audit.
   - Walk all ten checklist items against it. Items 8 and 9 are
     category-gated — skip when not applicable.
   - Use `Grep` for pattern-level checks: inline
     `bishop workspace current --json` invocations (item 3), restated
     STABLE-section content (item 7), missing frontmatter keys
     (item 10).
   - Record each violation as a structured finding:

     | Field | Example |
     |---|---|
     | `skill` | `bish-grill-me` |
     | `item` | `2` |
     | `severity` | `high` / `medium` / `low` |
     | `what` | One-sentence statement of the violation |
     | `location` | `skills/bish-grill-me/SKILL.md:12-30` or `frontmatter` |
     | `suggested-action` | Concrete fix, one sentence |

   Severity heuristic: item 7 paraphrases of STABLE sections and item
   3 inline detection in workspace-level skills are `high`; item 2
   miscategorisation is `high`; item 10 missing frontmatter keys are
   `medium`; item 8 and item 9 dilution are `medium`; the rest depend
   on context.

4. **Print a summary.** Group findings by skill, alphabetical. One
   line per finding:

   ```
   bish-grill-me
     [high]   item-3 — inline `bishop workspace current --json` preamble (lines 14-27)
     [medium] item-10 — missing `bishop.category` frontmatter key
   bish-arch
     [low]   item-7 — paraphrased push procedure (lines 220-235)
   ```

   If a skill has no findings, list it under a `Clean:` heading at the
   bottom so the user can see what was checked.

5. **Walk findings one at a time** per
   `BISHOP_CONTEXT.md → ## Per-finding Walk Pattern (TUNABLE)`. For
   each finding, print the full body (skill, item, severity, what,
   location, suggested-action, plus your recommended verdict) and use
   `AskUserQuestion` with the standard options:

   - **Card it (new)** (Recommended when severity is `high` or the
     fix is clear)
   - **Cluster with #N: \<title\>** — fold into a card already agreed
     this session. Only offer when at least one prior in-session card
     exists.
   - **Dismiss — context** — user explains why this is not an issue
     in this codebase.
   - **Defer** — note but do not card now.

6. **Granularity pass** per
   `BISHOP_CONTEXT.md → ## Card Granularity Rules (TUNABLE)`. Common
   clustering patterns for this skill:

   - Multiple item-3 violations across review skills → one card
     "Migrate review skills to `bishop skill bootstrap`", not five.
   - Multiple item-7 paraphrases of the same STABLE section → one
     card per section across all affected skills, not per skill.
   - Item-10 frontmatter gaps across many skills → one card
     "Backfill missing frontmatter keys across the family".

7. **Print the preview** per
   `BISHOP_CONTEXT.md → ## Task List Preview Format (STABLE)`. Each
   card body uses the template below. Cards default to
   `--lane "To Do"` and `--tag chore`; switch to `--tag docs` when
   the finding is about drift in `SKILL_FAMILY.md` or
   `BISHOP_CONTEXT.md` rather than in a `SKILL.md` file.

   Ask:

   > Please review the tasks above. Say **push** to create the Bishop cards.

8. **Push confirmed cards** per
   `BISHOP_CONTEXT.md → ## Card Push Procedure (STABLE)`. Always
   `--bottom` so the audit batch does not jump ahead of manually
   prioritised work.

9. **Print summary table:**

   | Card | Title | Lane | Tag |
   |------|-------|------|-----|
   | #N | … | To Do | chore |

</what-to-do>

---

## Card body template

```markdown
### Why
Item-<N> — <one-sentence statement of the violation>. Affected: `<skill-1>`, `<skill-2>`, …. See `docs/SKILL_FAMILY.md` §6 for the checklist rationale.

### Acceptance
- Re-running `/bish-audit-skills` reports no item-<N> finding against the listed skills.
- <any additional verifiable criterion specific to the finding>
```

---

<guardrails>

- Do NOT call `bishop skill bootstrap`. This is a meta skill and does
  not need workspace context — it operates on `skills/` directly.
- Do NOT modify any `SKILL.md` file. The audit produces cards; humans
  (or `bish-work-on-card`) implement the fixes.
- Do NOT modify any list-of-skills file (`README.md`, `CONTEXT.md`,
  `DIRECTION.md`, `src/Bishop.App/Terminal/BishopContext.static.md`)
  automatically — the user wires those up.
- Do NOT push cards before the user has confirmed each finding via
  the per-finding walk AND said "push" at the preview stage.
- Do NOT propose findings without a `file:section` (or `file:line-range`)
  citation. An uncitable finding is not actionable and should be
  dropped rather than carded.
- Do NOT extend the checklist on the fly. New audit items belong in
  `SKILL_FAMILY.md` §6 first, then mirrored here. If a new pattern
  needs checking, file a card for the doc + skill update and STOP.
- If `docs/SKILL_FAMILY.md` and the checklist above disagree, STOP
  and surface the disagreement. `SKILL_FAMILY.md` is the source of
  truth; the inline checklist may be stale.

</guardrails>
