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
