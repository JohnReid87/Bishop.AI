---
name: bish-review-batch
description: Review a delivered batch as a single unit before it merges. Runs in the batch worktree over `git diff base...branch`, composing four passes — correctness (a /code-review-style bug hunt), architecture heuristics on the touched files, security heuristics on the touched files, and an acceptance check verifying the diff against each member card's `### Acceptance`. Walks findings one at a time; confirmed findings become fix cards added to the same batch. Findings persist keyed to the batch, so re-reviewing after fixes never re-litigates a dismissed finding. Use when a batch run has finished and you want a delivery-level review before `bishop batch merge`, or invoke `/bish-review-batch <batch-name>`.
allowed-tools: Read, Glob, Grep, Agent, AskUserQuestion, Bash(bishop:*), Bash(git:*), Bash(dotnet:*)
bishop.scope: workspace
bishop.command: /bish-review-batch
bishop.stage: true
bishop.stage_prompt: Enter the batch name to review
bishop.category: review
---

The context-pack below bundles workspace metadata, the batch under review (member cards + prior findings), recent git history, and Bishop convention procedures (Shell selection, Card model, Card Push Procedure, Per-finding Walk Pattern, Findings Recording Procedure) — canonical source: `.bishop/BISHOP_CONTEXT.md`.

---

**This skill reviews one batch. It needs a batch name.** The name arrives as the
skill argument (the text after `/bish-review-batch`). If no batch name was
supplied, STOP and ask:

> `/bish-review-batch` reviews a single delivered batch. Which batch?
> (run `bishop batch list` to see the names)

**Before anything else — load the context-pack** (quote the batch name so
names with spaces survive):

```
bishop context-pack review-batch --batch "<batch-name>"
```

If the command exits non-zero, surface the stderr message as-is and STOP. A
missing or ambiguous batch name fails here — do not guess which batch was meant.

Parse the JSON and extract:
- `workspace.name` — echoed back as confirmation
- `workspace.path` — repo root; the batch worktree is a sibling checkout
- `workspace.tags` / `workspace.lanes` — tag/lane names for pushing fix cards
- `conventions` — STABLE/TUNABLE procedure sections
- `skill_specific.batch` — `name`, `branch_name`, `base_branch`, `worktree_path`, `status`
- `skill_specific.cards` — the member cards, each with `number`, `title`, `description` (the `### Acceptance` section is read from here), `tag`, `lane_name`, `is_closed`
- `skill_specific.prior_findings` — outcomes from previous `bish-review-batch` runs **on this batch**; each entry has `identity_hash`, `file`, `symbol`, `rule`, `title`, `status` (`pending` / `dismissed` / `resolved` / `carded:#N`), `rebuttal_text`, and `linked_card_number`. Use during triage to skip findings the user has already decided on — see the "Prior-findings recall" rule in step 6.

Echo the batch back on its own line (`<...>` are values from `skill_specific.batch`):

> **Workspace:** \<name\> — **Batch:** \<batch name\> (`<branch_name>` ← `<base_branch>`)

---

## What this skill does

Reviews the **delivery**, not the whole solution. The unit of review is the
diff a batch introduces: `git diff <base_branch>...<branch_name>` computed in the
batch worktree. Existing review skills (`bish-arch`, `bish-security`) audit the
entire codebase; this skill sits in the gap between "batch run finished" and
`bishop batch merge`, judging only what the batch changed and whether it
delivered what its cards promised.

It composes **four passes** over the batch diff, then walks the merged findings
with the user one at a time. Confirmed findings become **fix cards added to the
same batch** (so they ride along in the same delivery, not the general backlog).
Every finding is persisted keyed to the batch, so a second review after the
fixes land skips anything the user already dismissed.

The skill is read-only over the code — it never edits the worktree or merges the
batch. It only reads the diff and writes cards + findings through the `bishop`
CLI.

## Review passes

All four passes look **only at the batch diff and the files it touches**. A
finding outside the touched set is out of scope — that is what `bish-arch` /
`bish-security` over the whole solution are for. Each finding must cite at least
one `file:line` inside the diff.

1. **Correctness** *(a /code-review-style bug hunt)* — logic errors, off-by-one,
   null/empty-collection handling, incorrect async/await or `CancellationToken`
   propagation, resource leaks, broken invariants, swallowed exceptions,
   mis-wired DI, and regressions the diff introduces. This is the primary pass.

2. **Architecture** *(the `bish-arch` heuristics, scoped to touched files)* —
   layer-boundary violations, SOLID smells introduced by the change, DI-lifetime
   mistakes, leaking a lower layer's types through a public surface, missing test
   seams, and .NET idiom regressions. Only flag what **this diff** introduced or
   made worse — pre-existing debt in an untouched file is out of scope.

3. **Security** *(the `bish-security` heuristics, scoped to touched files)* —
   injection (SQL/command/LDAP/XPath/path), weak crypto or hardcoded secrets,
   unsafe deserialization or dynamic code, missing authn/authz, and sensitive
   data in logs — but only where the diff added or changed the code.

4. **Acceptance check** — for each member card in `skill_specific.cards`, read
   its `### Acceptance` section and verify the diff actually satisfies each
   criterion. A criterion with no corresponding change in the diff is a finding
   (`dimension: Acceptance`, cite the card number and the unmet criterion).
   Closed member cards are still checked — a card marked Done whose acceptance
   the diff does not meet is exactly the kind of gap this pass exists to catch.

## Card body template

Use the H3-section markdown template below for every fix card. Required
sections: `### Why` and `### Acceptance`. Include optional sections only when
they add value.

```markdown
### Why
<pass> — <what's wrong, 1 sentence>. Locations: `<file:line[, file:line]...>`.
Found reviewing batch `<batch.name>` (card #<member-card> acceptance) .

### Acceptance
- <verifiable criterion the fix must meet>

### Changes
- `<file:line>` — <suggested change>

### Related
- `<file:line>` — <one-line summary of a clustered finding>
```

Rules:
- Backtick file paths, identifiers, and CLI commands.
- Omit `### Changes` when the fix is already clear from `### Why`.
- Omit the acceptance-provenance clause when the finding is not from the
  acceptance pass.

<what-to-do>

1. **Workspace + batch detection** (above). If `skill_specific.batch` is null or
   the argument was empty, STOP per the opening prompt.

2. **Compute the diff.** Run against the worktree (`git -C` avoids `cd`):

   ```
   git -C "<worktree_path>" diff <base_branch>...<branch_name> --stat
   git -C "<worktree_path>" diff <base_branch>...<branch_name>
   ```

   - If `--stat` is empty, the branch introduces no changes. Tell the user the
     batch diff is empty, record the run via the no-findings path of `Findings
     Recording Procedure` with `--skill bish-review-batch --batch "<name>"`, and
     STOP — there is nothing to review.
   - Capture the branch tip SHA for the findings record:
     `git -C "<worktree_path>" rev-parse <branch_name>`.
   - Note the touched-file set from `--name-only` — it bounds every pass.

3. **Run the four passes.** Spawn Explore subagents (via `Agent` with
   `subagent_type: "Explore"` and `model: "sonnet"`). You may run the correctness,
   architecture, and security passes as three parallel subagents (single message,
   multiple `Agent` calls) since they are independent; run the acceptance pass
   separately with the member cards' `### Acceptance` text in the brief. Brief
   each subagent with:

   - The `worktree_path` as the repo root and the exact diff command from step 2
     so it reviews the same range.
   - The touched-file set — findings must stay inside it.
   - The heuristics for that pass (from "Review passes" above).
   - For the **acceptance** pass: the `number`, `title`, and `### Acceptance`
     bullets of every member card, plus the diff. Ask it to report, per card,
     which criteria the diff satisfies and which it does not.
   - Each finding must include: `pass` (Correctness / Architecture / Security /
     Acceptance), `severity` (high/med/low), `location` (`file:line`, may be
     multiple, all inside the diff), `file` (primary path, workspace-relative),
     `symbol` (affected method/class/identifier, or the card number for
     acceptance findings), `what` (1 sentence), `why_it_matters` (consequence in
     this delivery), and `suggested_action`. Reject output that omits `file` or
     `symbol`, or that cites a file outside the touched set.

   If **all four passes are clean**, record the run via the no-findings path of
   `Findings Recording Procedure` with `--skill bish-review-batch --batch
   "<name>"`, congratulate the user, note the batch looks ready for `bishop batch
   merge`, and STOP.

4. **Merge + echo.** Combine the four passes into one severity-ordered list
   (dedupe findings that different passes surfaced at the same `file:line`). Print
   a one-line overview per finding the user can scan before triage:

   ```
   #1 [high] Correctness — Foo/Bar.cs:42        — <one-line>
   #2 [med]  Acceptance  — card #1114           — <unmet criterion>
   #3 [med]  Security    — Baz/Qux.cs:88        — <one-line>
   ```

5. **Ensure the fix-card tag exists** if you intend to push cards. Fix cards are
   tagged by the pass that found them: Correctness → `bug`, Architecture →
   `arch`, Security → `security`, Acceptance → `bug`. All four tags are in the
   fixed global set, so no tag creation is needed.

6. **Triage loop.** Walk surviving findings in severity order. For each finding,
   first apply the **Prior-findings recall** rule:

   - Match against `skill_specific.prior_findings` by `(file, rule, symbol)` when
     all three are present, falling back to `title`.
   - If the prior status is `dismissed`: skip the interview silently. List it in
     the report under "previously dismissed" with the prior `rebuttal_text`
     verbatim. Track outcome `dismissed`.
   - If the prior status is `carded:#N` (or `linked_card_number` is set): skip the
     interview, reference the existing card #N, and track outcome `carded:#N`.
   - Otherwise fall through to the normal interview.

   For findings that fall through, print the full body (location(s), what,
   why-it-matters, suggested-action, plus your recommended verdict), then use
   `AskUserQuestion` with these options (recommended one first, suffixed
   " (Recommended)"):

   - **Card it (new)** — file this finding as a new fix card (see step 7).
   - **Cluster with #N: \<title\>** — fold into a fix card already agreed this
     session; append its location to that card's `### Related`. Only offer when a
     prior in-session card exists.
   - **Dismiss — context** — user explains why this isn't an issue. Capture the
     reason verbatim so a re-review shows it back. Suggest pinning load-bearing
     rebuttals as a project memory.
   - **Defer** — note it but don't card it now.

   **Track every finding** in a session log per the "Track findings during
   triage" sub-step of `Findings Recording Procedure`. Identity fields: `file` =
   the subagent's `file`, `rule` = the finding's `pass` (e.g. `Correctness`,
   `Acceptance`), `symbol` = the subagent's `symbol`. Map choices to pending
   outcomes: **Card it (new)** and **Cluster with #N** → `pending-card:<index>`;
   **Dismiss** → `dismissed`; **Defer** → `parked`.

7. **Push confirmed cards into the batch.** For each agreed card, create it per
   `Card Push Procedure` (temp file → `bishop card create` → remove temp file),
   then add it to the batch:

   ```
   bishop card create --lane "To Do" --title "<title>" --tag <pass-tag> --description-file ".bishop/tmp-card-<slug>.md" --bottom
   bishop batch add-card "<batch.name>" <new-card-number>
   ```

   Fix cards belong to the batch so they ship in the same delivery — do not leave
   them loose in the backlog. Use `--bottom`; review pushes must not jump ahead of
   prioritised work.

8. **Print the summary table:**

   | Card | Pass | Title | Severity |
   |------|------|-------|----------|
   | #N | Correctness | ... | high |

   Then tell the user:

   > These fix cards are on batch `<batch.name>`. Work them (e.g. in the batch
   > worktree), then re-run `/bish-review-batch <batch.name>` before
   > `bishop batch merge`. Dismissed findings carry their rebuttal forward — the
   > prior reason surfaces and the skill won't re-interview you about them.

9. **Record this run** by following `Findings Recording Procedure` with
   `--skill bish-review-batch --batch "<batch.name>"` and the branch-tip SHA from
   step 2. Before emitting, resolve any `pending-card:<n>` markers to
   `carded:#<N>` using the card numbers from step 7, so every tracked finding
   carries one final outcome (`carded:#<N>` / `dismissed` / `parked`). The
   `--batch` flag is what makes the memory per-batch: the same finding dismissed
   here will not resurface on the next review of this batch.

</what-to-do>

<guardrails>

- Do NOT review anything outside the batch diff. Findings in untouched files
  belong to `bish-arch` / `bish-security` over the whole solution, not here.
- Do NOT push cards before the user confirms each finding via triage.
- Do NOT propose findings without `file:line` citations inside the diff. Reject
  subagent output that omits locations or cites files outside the touched set.
- Do NOT edit the worktree, stage, commit, or merge. This skill is read-only over
  the code; it only writes cards and findings through `bishop`.
- Do NOT forget `--batch "<name>"` on the findings record — without it the run is
  recorded as a whole-solution run and the per-batch dismissal memory breaks.
- Do NOT re-raise a finding the prior run recorded as `dismissed` or `carded` —
  the Prior-findings recall rule in step 6 is the gate.
- Always add confirmed fix cards to the batch with `bishop batch add-card`; a fix
  card left in the backlog defeats the point of reviewing the delivery as a unit.
- If the acceptance pass finds a Done member card whose acceptance the diff does
  not meet, surface it — do not assume a closed card is satisfied.

</guardrails>
