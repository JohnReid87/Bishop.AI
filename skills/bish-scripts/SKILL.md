---
name: bish-scripts
description: Interviews the user about what they want to automate, drafts a PowerShell script, and saves it to the Bishop scripts folder so it immediately appears in the launcher.
allowed-tools: Write, Read, AskUserQuestion, Bash(bishop:*)
bishop.category: discuss
---

> Recommended model: Sonnet 4.6 — structured script authoring; extended reasoning not required.

## What this skill is

A **script authoring conversation**. The user describes what they want to automate; the skill interviews, drafts a PowerShell `.ps1`, and saves it to `%AppData%\Bishop.AI\scripts\` so it appears in the ScriptsPage launcher on next navigation.

Contract:
- One script per session.
- Write new scripts only — do not edit existing ones.
- Ask, draft, confirm, write — in that order.
- No card is created; the output is the `.ps1` file.

---

## Interview

Open with:

> What do you want the script to do?

Then gather the remaining details one question at a time:

1. **Name** — if not stated, suggest a kebab-case name derived from the purpose and confirm before writing.
2. **Parameters** — if the purpose implies inputs, propose a parameter list and confirm. If ambiguous, ask: "Does it need any parameters?"
3. **Extra specifics** — invite the user to mention flags, error handling preferences, or target machines. If they say nothing, proceed.

Use `AskUserQuestion` where the answer has a discrete set of choices; free-text for open-ended responses.

---

## Draft

Write a PowerShell `.ps1` draft that:

- Starts with a `# Synopsis:` comment line.
- Uses `[CmdletBinding()]` and a `param(...)` block when parameters were specified.
- Uses descriptive variable names matching the user's language.
- Ends with output appropriate to the stated goal (`Write-Host`, `Write-Output`, or returned objects).

Print the draft inside a fenced code block and ask for confirmation:

> **Draft:** `<name>.ps1`
>
> ```powershell
> <draft>
> ```
>
> Does this look right? Say **save** to write it, or describe any changes.

Iterate until the user confirms with "save", "yes", "write it", "looks good", or similar.

---

## Save

Resolve the APPDATA path by running `Get-Item env:APPDATA | Select-Object -ExpandProperty Value` via PowerShell.

Write the confirmed script to:

```
<APPDATA>\Bishop.AI\scripts\<name>.ps1
```

Use the `Write` tool. After writing, confirm:

> **Saved:** `<APPDATA>\Bishop.AI\scripts\<name>.ps1`
> The script will appear in the ScriptsPage launcher on next navigation.

ARGUMENTS: $ARGUMENTS
