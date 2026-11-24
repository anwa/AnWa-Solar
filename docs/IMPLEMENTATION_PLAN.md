# Implementation Plan

This plan brings AnWa-Solar in line with `AGENTS.md` and then extends it. Each milestone is implemented on its own branch and merged into `main` when the user confirms it is complete; the merge increases the minor version and is tagged (see `AGENTS.md`).

## M1 – Repository rules (`main`, documentation only)

- [x] `AGENTS.md`, `CLAUDE.md` and this plan
- [x] Remove the prompt tooling of the former chat workflow (`prePrompt.md`, `CreatePrompt.py`, its `.gitignore` entries)

## M2 – Project structure and build (`refactor/project-structure`, v0.3.0)

- [x] Target `net11.0-windows`; `global.json` pins the SDK
- [x] `Directory.Build.props` with `<Version>` as the single version source, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`; version shown in the main window title and logged at startup
- [x] `Directory.Packages.props` with central package versions (newest stable versions for .NET 11)
- [x] `.editorconfig` and `.gitattributes`
- [x] Split into `src/AnWaSolar.Core`, `src/AnWaSolar.App` and `tests/AnWaSolar.Core.Tests` (xUnit); namespaces match folders
- [x] Checks `dotnet build -warnaserror`, `dotnet test`, `dotnet format --verify-no-changes` pass

## M3 – Calculation core (`feature/calculation-core`, v0.4.0)

- [x] One string calculation in Core that returns a result object (temperature-corrected values, limit checks, allowed module and string ranges, messages); the live results and the report both use it
- [x] Disabled MPPTs are skipped in the report as in the live results
- [x] The report also checks the maximum number of strings per MPPT
- [x] Tests with worked examples from real datasheets for every check
- [x] Tmin and Tmax are cell temperatures (default Tmax 70 °C); optional VMPP temperature coefficient with the PMAX coefficient as fallback

## M4 – Settings and logging (`feature/user-settings`, v0.5.0)

- [x] Parameters and last selection in `%LOCALAPPDATA%\AnWa-Solar\settings.json`, written atomically and debounced; `appsettings.json` keeps only application defaults and is no longer written at runtime
- [x] One-time import of an existing selection from the old `appsettings.json` next to the executable
- [x] File logger serializes concurrent writes
- [x] Remove the committed personal selection from `appsettings.json`

## M5 – Master data robustness (`fix/master-data`)

- [x] Inverter currents and other datasheet values as decimal numbers
- [x] Invalid entries are skipped individually and reported in the GUI

## M6 – English translation (`refactor/english`)

- [ ] GUI text, log messages, comments and identifiers (for example `Wechselrichter` → `Inverter`)
- [ ] Master data property names in English, with a migration of the existing data files and of saved selections
- [ ] `README.md` and `CHANGELOG.md` in English; remove the formula experiment from the README

## M7 – Installer and release (`feature/installer`)

- [ ] WiX 5 installer project `installer/AnWaSolar.Installer` with a fixed `UpgradeCode`
- [ ] `tools/Build-Installer.ps1`: self-contained win-x64 publish, MSI and SHA-256 in `dist\installer\`
- [ ] `docs/DEPLOYMENT.md` with the build recipe and build records
- [ ] First release built from its tag in a clean worktree

## Later

- Report preview with formula rendering (replace the `WebBrowser` control)
- Further calculations: battery storage, AC side, cable and fuse sizing
- Report export (HTML/PDF)
