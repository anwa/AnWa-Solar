# Changelog

All user-visible changes of AnWa-Solar.

## v0.10.0 (2026-11-24)

- New: Dark mode. The theme button in the top bar switches between System (follows Windows), Light and Dark; the choice is saved. All windows, the report preview and the manual follow it.
- New: User manual (F1 or the book button) with page tree, search, links between pages and formulas.
- New: About window with version, build, runtime, folders, contact and license; the information can be copied.
- Changed: Report headings get anchors, and the report preview uses the same page style as the manual.

## v0.9.0 (2026-11-24)

- New: AnWa-Solar has its own icon (sun and PV module) for the window, the taskbar, the Start menu and Apps & features.
- New: The report preview uses Microsoft Edge WebView2 and shows formulas properly typeset (KaTeX, shipped with the application, works offline).
- Improved: Tables and formulas are enabled in the report by default.

## v0.8.0 (2026-11-24)

- New: Windows installer (MSI) that installs AnWa-Solar into `C:\Program Files\AnWa-Solar` with a Start menu shortcut and the license text. It contains everything the application needs, so no separate .NET installation is required.

## v0.7.0 (2026-11-24)

- Changed: The application, the report, the messages and the log are in English.
- Changed: The master data files are `Modules.json` and `Inverters.json` with English field names (for example `Manufacturer`, `Pmax (W)`, `Max. input current per MPPT (A)`); the shipped data was converted.
- Changed: `settings.json` uses English field names (schema version 2). Existing settings are converted automatically on the first start.
- Changed: The report is saved as `AnWa-Solar-Report.md` by default.

## v0.6.0 (2026-11-24)

- Improved: Datasheet values of inverters and the module power may be decimal numbers, for example 12.5 A.
- Improved: An invalid master data entry (wrong value type, invalid MPPT range, missing or inconsistent values, duplicate) is skipped on its own instead of discarding the whole file.
- New: After the start, a message lists master data entries and files that could not be loaded.

## v0.5.0 (2026-11-24)

- Changed: Calculation parameters and the last selection are stored per user in `%LOCALAPPDATA%\AnWa-Solar\settings.json` instead of the appsettings.json next to the program. Existing values are taken over automatically on the first start.
- Improved: While dragging a slider, the settings are saved once the input stops instead of on every step; they are also saved when the window closes.
- Improved: The settings file is written safely; an unreadable file is kept as a backup and the defaults are used.
- Fixed: Log lines are no longer lost when several messages are written at the same time.

## v0.4.0 (2026-11-24)

- Fixed: Disabled strings are no longer evaluated in the report.
- Fixed: The report also checks the maximum number of parallel strings per MPPT.
- Changed: The input current check uses the higher MPP current of both temperatures (Tmin and Tmax), as the report already did; the short-circuit current check also uses the higher value of both temperatures. Configurations close to the current limit may now show a warning.
- Changed: Tmin and Tmax are cell temperatures; the default Tmax is 70 °C instead of 40 °C. Check Tmax in the parameter dialog.
- Changed: Vmpp is calculated with the Pmax temperature coefficient instead of the Voc coefficient, or with the new optional Vmpp coefficient of the module. The MPPT and start voltage checks become stricter; the maximum DC voltage check is unchanged.

## v0.3.0 (2026-11-24)

- Changed: AnWa-Solar now requires .NET 11.
- New: The main window title shows the application version.

## v0.2.0

- New: Sliders in the string sections set the modules per string and the parallel strings. Their limits follow the calculation results.
- New: A switch per string enables or disables it without losing its values; the state is saved and restored at the next start.
- New: The modules slider ranges from 1 to the allowed maximum + 5 and marks the allowed range.
- New: Button texts follow the state ("Select PV module" → "Change PV module", "Select inverter" → "Change inverter"); "Remove module" is shown per MPPT when a module is selected.
- Improved: Slider ranges are updated immediately when the inverter, a module or the parameters change.
- Improved: The modules slider takes 3/4 of the width, the parallel strings slider 1/4.
- Improved: The report tab uses the whole window area.
- Improved: The buttons of a disabled string are disabled.
- Fixed: The saved state of a string switch is shown correctly at startup.

## v0.1.2

- New: Report tab with an automatically created Markdown report, switchable between Markdown source and HTML preview.
- New: Buttons to copy and save the Markdown source.
- Improved: The report contains calculation notes and an evaluation per MPPT.
- Fixed: Build errors and an unstable report preview.

## v0.1.1

- New: The last configuration (inverter, PV module per MPPT, modules per string and parallel strings) is saved and restored at startup.

## v0.1.0

- New: "String calculation" tab with the sections inverter, strings, results and parameters.
- New: Selection dialogs for inverters and PV modules with manufacturer and model filter and the datasheet values.
- New: Parameter dialog; the parameters are saved and loaded at startup.
- New: One section per MPPT of the inverter; the results are recalculated on every change.
- New: The maximum number of strings per MPPT limits the parallel strings.
- Design: Material Design 3 with light and dark mode.
