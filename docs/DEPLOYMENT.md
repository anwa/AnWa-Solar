# Deployment

## Build Recipe

Release artifacts are built from the tagged commit in a clean, detached worktree outside the repository:

```powershell
git worktree add --detach <scratch>\release-vX.Y.Z vX.Y.Z
cd <scratch>\release-vX.Y.Z
dotnet build -warnaserror
dotnet test
pwsh tools/Build-Installer.ps1
```

`tools/Build-Installer.ps1` publishes the application self-contained for win-x64 into `dist\publish`, builds the WiX 5 MSI and writes `dist\installer\AnWaSolar-Setup-X.Y.Z.msi` and its `.sha256` file. It refuses to overwrite an existing MSI of the same version without `-Force`.

Smoke test: start `dist\publish\AnWa-Solar.exe` from the worktree and check that the main window opens and shows the expected version. Then copy the MSI and its `.sha256` file into the repository's git-ignored `dist\installer\`, compare the SHA-256 and remove the worktree.

The MSI installs per machine into `C:\Program Files\AnWa-Solar` with a Start menu shortcut. User settings and logs in `%LOCALAPPDATA%\AnWa-Solar` are not part of the installation and stay on uninstall. The `UpgradeCode` in `installer/AnWaSolar.Installer/Package.wxs` never changes, so a higher version replaces the installed one.

## Build Records

| Version | Commit | Date | SDK | MSI SHA-256 | Result |
|---|---|---|---|---|---|
| 0.8.0 | `2e707f9` | 2026-11-24 | 11.0.100 | `952003E4A647BB81E5B0BEC2714FCFE812681AED1864D16A01B1CB052A9B655F` | Built from the tag in a clean worktree. Build without warnings, 55 tests passed, published exe starts and shows version 0.8.0. MSI properties checked read-only (name, version 0.8.0, manufacturer, UpgradeCode `55C1C5AB-3178-461E-A8D7-DA8A3B306EF8`, per-machine, 438 files including the master data and LICENSE.txt, no .pdb, Start menu shortcut); not installed by the agent. Replaces the local test build of 0.8.0 in `dist\installer`. |
| 0.9.0 | `15065df` | 2026-11-24 | 11.0.100 | `8EF52B48336DB5681D643EF4A0883DDAC310ECF6D7A9C3D25827485976C00EB3` | Built from the tag in a clean worktree. Build without warnings, 67 tests passed, published exe starts and shows version 0.9.0; the report preview (WebView2) rendered the KaTeX formula on a screenshot. MSI properties checked read-only (version 0.9.0, unchanged UpgradeCode, per-machine, 470 files including KaTeX and the WebView2 libraries, application icon for Apps & features and the Start menu shortcut); not installed by the agent. |
| 0.10.0 | `e92fe6c` | 2026-11-24 | 11.0.100 | `F45C5328545B8F5A880A163C04F8164B1F5BA0E17C95FE875BD2CCFECFA17598` | Built from the tag in a clean worktree. Build without warnings, 77 tests passed, published exe starts and shows version 0.10.0; the About window shows build commit e92fe6c and andreas@anwa-soft.de. MSI properties checked read-only (version 0.10.0, unchanged UpgradeCode, per-machine, 470 files, no .pdb, application icon, Start menu shortcut); not installed by the agent. |
