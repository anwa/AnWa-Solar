# AnWa-Solar

AnWa-Solar is a Windows desktop application for designing PV strings. It checks the strings of each MPPT input of an inverter against the inverter limits (voltage, current, power, MPPT range) over a cell temperature range and creates a Markdown report. Calculation parameters and the last selection are stored per user.

It also checks the AC side and the grid connection, sizes cables and fuses, and checks a battery bank against the battery input of a hybrid inverter.

## Features

- **Projects**: every design is a project file in `%LOCALAPPDATA%\AnWa-Solar\Projects` with project data (name, customer, address, notes), all entries and copies of the master data values; a dialog at the start opens the last project (Enter), one of the five most recent or a new one; changes are saved automatically; **Export ZIP** saves the report as PDF, the datasheets and the project file in one folder.
- **String calculation** tab in two columns:
  - **Configuration**: the inverter with its datasheet values, the parameters (minimum and maximum cell temperature, Tmin default −20 °C and Tmax default 70 °C, and the safety margin) and one card per MPPT with module selection, sliders for modules per string and parallel strings, and a switch that disables an MPPT without losing its values. The module slider marks the allowed range.
  - **Results**: recalculated on every change. An overall status banner, one card per MPPT with a voltage window graphic, a table of the lowest and highest values against the limits, the allowed module count and the notes with recommendations, the AC side and grid connection (DC/AC ratio, apparent power against the simplified registration, unbalanced load of single-phase inverters, AC current, feed-in limit), cable and fuse sizing (string cables with the module leads, string fuses after IEC 62548, AC line and circuit breaker, battery cables with or without busbar and their fuses, from the standard tables in `Data/CableTables.json`), the battery storage check against the battery input of a hybrid inverter (bank voltage, module counts, charge and discharge current, energy), and the inverter limits from the datasheet and with the safety margin.
- **Report** tab: Markdown report of the current configuration with a preview (Microsoft Edge WebView2) that renders the formulas with KaTeX; the Markdown can be copied or saved, and the report can be exported as a single HTML file (formulas rendered, fonts embedded, works offline) or as an A4 PDF. The preview works offline; KaTeX is shipped with the application. WebView2 keeps its data in `%LOCALAPPDATA%\AnWa-Solar\WebView2`. The Microsoft Edge WebView2 Runtime is part of Windows 11; without it the report tab shows the Markdown source.
- Master data of PV modules, inverters and batteries in JSON files; invalid entries are skipped and reported.
- Datasheet import: reads a PDF datasheet of a PV module, inverter or battery (PdfPig, offline), shows the recognised values per power class or model for checking and completing, and saves them with a copy of the datasheet in the own master data in `%LOCALAPPDATA%\AnWa-Solar\Data`. Stored datasheets open in a viewer from the selection dialogs and the main window.
- Light and dark mode: the theme button in the title bar switches System → Light → Dark; System follows the Windows app theme. The choice is saved.
- User manual (F1 or the book button) with page tree, search and formulas; the pages are in [docs/manual](docs/manual) and embedded into the application.
- About window with version, build, runtime, installation and data folders, contact (Andreas Wansner, andreas@anwa-soft.de) and license.
- Own title bar and dialogs in the application style, in light and dark mode.
- Structured JSONL logging.

## Requirements for Building

- Windows 11 (x64)
- .NET 11 SDK (target framework `net11.0-windows`, SDK pinned in `global.json`)
- Visual Studio 2026 or the `dotnet` CLI

## Installation

Install the MSI `AnWaSolar-Setup-X.Y.Z.msi` (Windows 11, x64). It installs per machine into `C:\Program Files\AnWa-Solar`, adds a Start menu shortcut and needs no separate .NET installation. A newer MSI replaces the installed version; settings and logs in `%LOCALAPPDATA%\AnWa-Solar` are kept. How the MSI is built is described in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Build and Start

1. Clone the repository: https://github.com/anwa/AnWa-Solar.git
2. Provide the AnWa-Soft design library: `nuget.config` takes the package `AnWaSoft.Design.Wpf` from the local feed `D:\Workspace\Claude\Design\packages`, which `dotnet pack -c Release` in the Design repository fills. Without that feed the restore fails.
3. Build and test from the repository root:

   ```powershell
   dotnet build -warnaserror
   dotnet test
   ```

4. Start the application with `dotnet run --project src/AnWaSolar.App`, or open `AnWaSolar.slnx` in Visual Studio and start `AnWaSolar.App`.

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
- `Inverters.json` (list `Inverters`): `Manufacturer`, `Model`, `Max. DC input power (W)`, `Max. DC input voltage (V)`, `Start voltage (V)`, `MPPT voltage range (V)` (text such as `"160-650"`), `Nominal DC input voltage (V)`, `Max. input current per MPPT (A)`, `Max. short-circuit current per MPPT (A)`, `MPPT count`, `Max. strings per MPPT`, and the optional AC and battery values (`Battery voltage range (V)`, `Max. battery charge current (A)`, `Max. battery discharge current (A)`, `Battery inputs`).
- `Batteries.json` (list `Batteries`): one battery module with `Manufacturer`, `Model`, `Nominal voltage (V)`, `Operating voltage range (V)`, `Nominal energy (kWh)`, `Max. charge current (A)`, `Max. discharge current (A)` and the optional `Nominal capacity (Ah)`, `Usable energy (kWh)`, `Depth of discharge (%)`, `Max. modules in series`, `Max. modules in parallel`.

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

- AnWa-Soft design library `AnWaSoft.Design.Wpf` (palette, own title bar, cards, status styles, message dialog, light/dark/system theme service) on Material Design 3 through MaterialDesignThemes. `src/AnWaSolar.App/Resources/Theme.xaml` only adds the application icon and binds the result states to the status styles.
- Reference design: [MaterialDesign3.Demo.Wpf](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/tree/master/src/MaterialDesign3.Demo.Wpf).

## Architecture

- `src/AnWaSolar.Core`: models, master data (`JsonDataStore`), string calculation (`Calculation/StringCalculator`), results text and presentation (`ResultPresenter`), Markdown report, settings (`SettingsService`) and the JSONL logger, without WPF references. The live results and the report use the same calculation result.
- `src/AnWaSolar.App`: WPF application (windows on `ChromeWindow` of the design library, dependency injection through Microsoft.Extensions.Hosting). The report preview and the manual share one WebView2 setup.
- `docs/manual`: user manual (Markdown and `toc.json`), embedded into the executable; `ManualIndex` in Core builds the page tree and the search.
- `tests/AnWaSolar.Core.Tests`: xUnit tests for Core.
- `installer/AnWaSolar.Installer`: WiX 5 MSI project; `tools/Build-Installer.ps1` builds the MSI (see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)).
- The version is defined once in `Directory.Build.props` and shown in the main window title; package versions are pinned in `Directory.Packages.props`.

## Roadmap

- No further milestones are planned yet; see [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md).

## License

GNU General Public License v3.0, see [LICENSE](LICENSE).

Third-party software: the report preview uses [KaTeX](https://katex.org/) 0.19.1 (MIT License), shipped in `src/AnWaSolar.App/Assets/katex` with its license.
