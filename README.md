# AnWa-Solar

AnWa-Solar is a Windows desktop application for designing PV strings. It checks the strings of each MPPT input of an inverter against the inverter limits (voltage, current, power, MPPT range) over a cell temperature range and creates a Markdown report. Calculation parameters and the last selection are stored per user.

Battery storage, AC-side and cable/fuse calculations are planned (see [Roadmap](#roadmap)).

## Features

- **String calculation** tab with four sections:
  - **Inverter**: selection dialog with manufacturer and model and the datasheet values.
  - **Strings**: one section per MPPT with module selection, sliders for modules per string and parallel strings, and a switch that disables a string without losing its values. The module slider marks the allowed range.
  - **Results**: recalculated on every change, with violated limits and recommended module and string counts.
  - **Parameters**: minimum and maximum cell temperature (Tmin, default −20 °C; Tmax, default 70 °C) and the safety margin.
- **Report** tab: Markdown report of the current configuration with a preview (Microsoft Edge WebView2) that renders the formulas with KaTeX; the Markdown can be copied or saved. The preview works offline; KaTeX is shipped with the application. WebView2 keeps its data in `%LOCALAPPDATA%\AnWa-Solar\WebView2`. The Microsoft Edge WebView2 Runtime is part of Windows 11; without it the report tab shows the Markdown source.
- Master data of PV modules and inverters in JSON files; invalid entries are skipped and reported.
- Light and dark mode: the theme button in the top bar switches System → Light → Dark; System follows the Windows app theme. The choice is saved.
- User manual (F1 or the book button) with page tree, search and formulas; the pages are in [docs/manual](docs/manual) and embedded into the application.
- About window with version, build, runtime, installation and data folders, contact (Andreas Wansner, andreas@anwa-soft.de) and license.
- Structured JSONL logging.

## Requirements for Building

- Windows 11 (x64)
- .NET 11 SDK (target framework `net11.0-windows`, SDK pinned in `global.json`)
- Visual Studio 2026 or the `dotnet` CLI

## Installation

Install the MSI `AnWaSolar-Setup-X.Y.Z.msi` (Windows 11, x64). It installs per machine into `C:\Program Files\AnWa-Solar`, adds a Start menu shortcut and needs no separate .NET installation. A newer MSI replaces the installed version; settings and logs in `%LOCALAPPDATA%\AnWa-Solar` are kept. How the MSI is built is described in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Build and Start

1. Clone the repository: https://github.com/anwa/AnWa-Solar.git
2. Build and test from the repository root:

   ```powershell
   dotnet build -warnaserror
   dotnet test
   ```

3. Start the application with `dotnet run --project src/AnWaSolar.App`, or open `AnWaSolar.slnx` in Visual Studio and start `AnWaSolar.App`.

## Calculation

For each enabled MPPT the module values are corrected linearly for the cell temperature: x(T) = x(25 °C) · (1 + α · (T − 25 °C)), with α in %/°C from the datasheet.

| Check | Value | Limit |
|---|---|---|
| Max. DC voltage | modules × Voc(Tmin) | max. DC input voltage |
| MPPT upper bound | modules × Vmpp(Tmin) | upper bound of the MPPT range |
| MPPT lower bound | modules × Vmpp(Tmax) | lower bound of the MPPT range |
| Start voltage | modules × Vmpp(Tmax) | start voltage |
| Short-circuit current | strings × Isc (higher value of Tmin and Tmax) | max. short-circuit current per MPPT |
| Input current | strings × Impp (higher value of Tmin and Tmax) | max. input current per MPPT |
| Strings per MPPT | parallel strings | max. strings per MPPT |
| DC power | modules × strings × Pmax(Tmin) | max. DC input power / MPPT count |

- The safety margin tightens every limit: maximum values are reduced and minimum values are raised by the margin.
- Vmpp uses the Vmpp temperature coefficient of the module. Most datasheets do not state it; then the Pmax coefficient is used as a conservative approximation. Impp is derived as Pmax / Vmpp.
- Example: Voc = 45.02 V, α(Voc) = −0.22 %/°C, Tmin = −20 °C gives Voc(Tmin) = 45.02 V · (1 + 0.22 % · 45) = 49.48 V per module.

## Master Data

The files in `src/AnWaSolar.Core/Data/` are copied into the `Data` folder next to the executable and are read-only at runtime.

- `Modules.json` (list `Modules`): `Manufacturer`, `Model`, `Pmax (W)`, `Vmpp (V)`, `Impp (A)`, `Voc (V)`, `Isc (A)`, `Module efficiency (%)`, the temperature coefficients of Pmax, Voc and Isc (`Temperature coefficient of … (%/°C)`), the optional `Temperature coefficient of Vmpp (%/°C)`, and the operating temperature range.
- `Inverters.json` (list `Inverters`): `Manufacturer`, `Model`, `Max. DC input power (W)`, `Max. DC input voltage (V)`, `Start voltage (V)`, `MPPT voltage range (V)` (text such as `"160-650"`), `Nominal DC input voltage (V)`, `Max. input current per MPPT (A)`, `Max. short-circuit current per MPPT (A)`, `MPPT count`, `Max. strings per MPPT`.

Values come from the manufacturer datasheets and may be decimal numbers. Every entry is checked on its own (value types, MPPT range format, positive values, Voc > Vmpp, Isc ≥ Impp, no duplicate manufacturer and model). Invalid entries are skipped; skipped entries and unreadable files are listed in a message after the start and in the log.

## Settings

- User settings are stored in `%LOCALAPPDATA%\AnWa-Solar\settings.json` (schema version 2):
  - `Parameters`: `MinTempC`, `MaxTempC`, `SafetyMarginPct`
  - `LastSelection`: the inverter (`Manufacturer`, `Model`) and per MPPT the module reference, `ModulesPerString`, `ParallelStrings` and `IsEnabled`
  - `Theme`: `System`, `Light` or `Dark`
- Changes are saved automatically, at the latest 0.5 s after the last change and when the window closes. The file is written atomically.
- An unreadable `settings.json` is kept as `settings.json.invalid-<timestamp>` and the defaults are used.
- Older settings are taken over automatically: `settings.json` of schema version 1 (versions 0.5.0 and 0.6.0) is converted on the first start, and without `settings.json` the values are imported once from the `appsettings.json` next to the executable, where versions up to 0.4.0 stored them.
- `appsettings.json` next to the executable only holds application defaults and is never written: `AnWaSolar:Markdown:UseAdvancedExtensions` (enables tables and formulas in the report, default `true`) and, optionally, `AnWaSolar:LogDirectory`.

## Logging

- Structured JSONL files in `%LOCALAPPDATA%\AnWa-Solar\logs`, one file per day (folder configurable with `AnWaSolar:LogDirectory`).
- Read and write errors are logged; the application shows a short message where the user has to act.

## Design

- Material Design 3 through MaterialDesignThemes (BundledTheme, `BaseTheme="Inherit"` follows the Windows light or dark mode).
- Reference design: [MaterialDesign3.Demo.Wpf](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/tree/master/src/MaterialDesign3.Demo.Wpf).

## Architecture

- `src/AnWaSolar.Core`: models, master data (`JsonDataStore`), string calculation (`Calculation/StringCalculator`), results text and Markdown report, settings (`SettingsService`) and the JSONL logger, without WPF references. The live results and the report use the same calculation result.
- `src/AnWaSolar.App`: WPF application (windows, theme service, dependency injection through Microsoft.Extensions.Hosting). The report preview and the manual share one WebView2 setup.
- `docs/manual`: user manual (Markdown and `toc.json`), embedded into the executable; `ManualIndex` in Core builds the page tree and the search.
- `tests/AnWaSolar.Core.Tests`: xUnit tests for Core.
- `installer/AnWaSolar.Installer`: WiX 5 MSI project; `tools/Build-Installer.ps1` builds the MSI (see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)).
- The version is defined once in `Directory.Build.props` and shown in the main window title; package versions are pinned in `Directory.Packages.props`.

## Roadmap

- Results as a table with OK/warning states.
- Further calculations: battery storage, AC side, cable and fuse sizing.
- Report export as HTML or PDF, and a report preview that renders formulas.

## License

GNU General Public License v3.0, see [LICENSE](LICENSE).

Third-party software: the report preview uses [KaTeX](https://katex.org/) 0.19.1 (MIT License), shipped in `src/AnWaSolar.App/Assets/katex` with its license.
