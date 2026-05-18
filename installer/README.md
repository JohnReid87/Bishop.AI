# Bishop.AI installer

Wix v5 project that produces a single per-user MSI bundling `bishop.exe`,
`Bishop.UI.exe`, and the vendored skills.

## Prerequisites

- .NET 10 SDK
- Wix v5 dotnet tool: `dotnet tool install --global wix --version 5.0.2`
  (Wix v6+ requires Open Source Maintenance Fee EULA acceptance; v5 doesn't.)

## Build

From the repo root or this directory:

```powershell
pwsh installer/build.ps1                 # produces installer/bin/Release/Bishop.AI.msi
pwsh installer/build.ps1 -Version 0.2.0  # override the embedded ProductVersion
```

The script publishes `src/Bishop.Cli` and `src/Bishop.UI` to
`installer/publish-{cli,ui}/`, then invokes `dotnet build` on the
`Bishop.Installer.wixproj`. Both publish dirs are gitignored.

## What the MSI does

- Installs to `%LocalAppData%\Programs\Bishop.AI\{Cli,UI}\` (per-user, no UAC).
- Adds `%LocalAppData%\Programs\Bishop.AI\Cli` to the user PATH (HKCU\Environment),
  so `bishop` is available in any new shell after install.
- Creates a Start Menu shortcut `Bishop.AI` that launches `Bishop.UI.exe`.
- On uninstall, removes all installed files, the PATH entry, and the shortcut.

After installing, run `bishop install-skills` once to populate
`%USERPROFILE%\.claude\skills\` with the bundled skills.
