# Project Rules

## Inherited Rules

This repository lives in a rule hierarchy. Apply these files in addition to the rules below, in this order of increasing precedence:

| Level | File |
|---|---|
| Global | `../../AGENTS.md` |
| Tools | `../AGENTS.md` |

They define language, Git authority, branches, commit format, versioning principles, documentation style, evidence and the shared tool conventions (configuration, GUI threading, release builds). This file contains only what is specific to this repository and takes precedence over the inherited files.

If the inherited files are not present, for example in a fresh clone: write all repository content in English, never commit secrets, and never push, tag or publish without the user's approval.

## Project

AnWa-Solar is a Windows desktop tool for designing photovoltaic systems. It currently checks PV string configurations against inverter limits (voltage, current, power, MPPT range) over a temperature range and produces a Markdown report. Battery storage, AC-side and cable/fuse calculations are planned.

- The migration to the rules of this file and the further milestones are tracked in `docs/IMPLEMENTATION_PLAN.md`. Read it before starting work; where the code does not yet follow a rule below, the plan names the milestone that changes it.
- Implement one milestone at a time on its branch. Merge into `main` only when the user confirms the milestone is complete; that merge increases the minor version and is tagged.
- Keep `README.md`, `CHANGELOG.md`, the user manual in `docs/manual` and the milestone checkboxes in `docs/IMPLEMENTATION_PLAN.md` current with every behavior change. The manual is written for users, in English, and describes the current GUI texts; its links and anchors are checked by a test.

## Language

- All repository content is English, including GUI text, log messages, identifiers, master data property names, `README.md` and `CHANGELOG.md`. The German property names that `SettingsService` still reads exist only to convert old settings files.
- Manufacturer and model names in master data keep their original spelling.

## Stack

- C# on .NET 11 (`net11.0-windows`), WPF, MaterialDesignThemes (Material Design 3) with the AnWa-Soft design library `AnWaSoft.Design.Wpf` from the local package feed `D:\Workspace\Claude\Design\packages` (`nuget.config`; colours and shared styles change in the Design repository, not here), Markdig for the report, Microsoft Edge WebView2 for the report preview with KaTeX shipped in `src/AnWaSolar.App/Assets/katex` (third-party files kept unchanged; update them only as a whole release with its license), `System.Text.Json` (no Newtonsoft.Json), `Microsoft.Extensions.Hosting` for dependency injection, configuration and logging.
- The Python sections of `../AGENTS.md` (Python environment, pytest/ruff checks) do not apply. Its configuration, GUI threading and release build rules apply with the .NET equivalents below.
- Projects: a Core library without WPF references (models, master data loading, calculations, report generation, settings), the WPF application, and an xUnit test project for Core. Calculation logic has exactly one owner in Core; the live results and the report both consume the same calculation result.
- `Nullable` and `ImplicitUsings` are enabled; build warnings are treated as errors.
- Package versions are pinned centrally in `Directory.Packages.props`. Use the newest stable version that supports the target framework; when the newest version needs a newer framework, stop and ask instead of raising the framework. Add a new dependency only when it is needed and name it in the commit body.
- User settings (calculation parameters, last selection) live in the per-user application data folder `%LOCALAPPDATA%\AnWa-Solar`, never next to the executable. Files are written atomically (write a temporary file, then replace). Writes triggered by continuous input such as sliders are debounced.
- Logs are structured JSONL files in `%LOCALAPPDATA%\AnWa-Solar\logs`. The file logger serializes concurrent writes.

## Version

- The single version source is `<Version>` in `Directory.Build.props` (`x.y.z`, without `v`). The application reads it from the assembly informational version and shows it in the main window title; no other file holds an independent version.
- Release tags `vX.Y.Z` exist from v0.1.0. Versions before the introduction of `Directory.Build.props` were recorded only in `CHANGELOG.md` and the tags.

## Checks

Run before every commit that changes code:

```text
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
```

## Installer and Release

- The MSI is built with WiX 5 (`installer/AnWaSolar.Installer`), deliberately not WiX 6 or newer, whose EULA requires the Open Source Maintenance Fee. Do not upgrade WiX without the user's decision.
- `tools/Build-Installer.ps1` is the pinned build recipe: self-contained win-x64 publish, MSI into `dist\installer\` with SHA-256. The installer project is not part of the solution, so `dotnet build` of the solution does not need a publish folder. `dist/` is git-ignored.
- The `UpgradeCode` in `Package.wxs` never changes once it is committed.
- For a release, build from the tagged commit as `../AGENTS.md` and `docs/DEPLOYMENT.md` describe, and record the build in `docs/DEPLOYMENT.md`.
- Every release gets a GitHub release for its tag (at the user's request, 2026-11-25): created with `gh release create` once the user has approved the push and the tag is on GitHub, with the `CHANGELOG.md` section of the version as notes and the MSI and its `.sha256` file as assets, as `docs/DEPLOYMENT.md` describes.
- Installing or uninstalling the MSI changes the machine; the user does it. Verify the MSI content read-only (properties, files, shortcut).

## Domain Rules

- The calculation decides whether a string may be connected to an inverter. The check of the open-circuit voltage at the minimum temperature against the maximum DC input voltage is safety-relevant: never weaken it, its safety margin or its temperature direction as a side effect of another change. Report questionable formulas or limits to the user instead of silently redefining them.
- Every change to a formula, limit or temperature assumption comes with Core tests that use worked examples with real datasheet values, and is named in `CHANGELOG.md`.
- The safety margin only tightens limits: maximum values are reduced, minimum values (start voltage, MPPT lower bound) are raised.
- Grid connection limits (unbalanced load, simplified registration, feed-in limit, DC/AC range) and, later, standard tables for cables and fuses are values the user sets or confirms; they are parameters or shipped data with their source, never values the agent picks. `Data/CableTables.json` holds common standard values entered at the user's request and checked by the user (`"Confirmed": true` since v0.17.2). When a value changes, set `"Confirmed": false`, so that the GUI and report say so, until the user has checked it again. Its section `BatteryCable` has its own `"Confirmed"` flag; its values were entered at the user's request in v0.18.0 and checked by the user (`"Confirmed": true` since v0.18.1). AC values of an inverter come from its datasheet; a missing value is shown as missing, not derived.
- Tmin and Tmax are module (cell) temperatures. VMPP uses the module's VMPP temperature coefficient; without one, the PMAX coefficient is the conservative approximation. Currents use the higher value of both temperatures.
- Master data (`Data/Modules.json`, `Data/Inverters.json`, `Data/Batteries.json`) contains values from manufacturer datasheets only. Never estimate or invent datasheet values; when a value is missing, ask the user. Property names carry their unit.
- Master data is shipped with the application and is read-only at runtime. The application never writes into its installation folder.
- Own master data (datasheet import) lives in `%LOCALAPPDATA%\AnWa-Solar\Data` with the imported PDFs in `Datasheets`; an own entry replaces a shipped entry with the same manufacturer and model. Values read from a datasheet are a proposal: the import dialog shows their source, leaves values that were not found empty and saves only what the user confirmed. Never fill a missing value with an estimate.
- A master data file that cannot be parsed is reported in the GUI, not only in the log; one invalid entry must not discard the whole file.

## GUI

- Material Design 3 through MaterialDesignThemes; light and dark mode are both fully usable (readable text, sufficient contrast, visible focus states).
- Network or other blocking work never runs on the UI thread; the current calculations are fast and synchronous.
- Visual verification of the GUI (layout, light/dark mode) is done by the user unless the agent actually started the application and inspected it; never claim GUI verification that was not performed.
