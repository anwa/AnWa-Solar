# Changelog

All user-visible changes of AnWa-Solar.

## Unreleased (v0.14.0)

- New: **Export HTML** saves the report as a single HTML file with rendered formulas and embedded fonts; it works offline and without scripts and always uses the light colours.
- New: **Export PDF** saves the report as an A4 PDF with title, date and page numbers in light colours.

## v0.13.0 (2026-11-24)

- New: Datasheet import. **Import datasheet** in the title bar and **Import…** in the module and inverter selection read a PDF datasheet, recognise the module or inverter values (German and English datasheets, several power classes or models per datasheet) and show them in a dialog to check, correct and complete. **Apply** saves the entry with a copy of the datasheet in your own master data in `%LOCALAPPDATA%\AnWa-Solar\Data`.
- New: Own master data. Entries in `%LOCALAPPDATA%\AnWa-Solar\Data\Modules.json` and `Inverters.json` are loaded with the shipped data; an own entry replaces a shipped entry with the same manufacturer and model.
- New: Stored datasheets open in a viewer from the selection dialogs and from the main window (next to the inverter and each module).
- New: The import requires negative temperature coefficients of Pmax and Voc and an MPPT range below the max. DC input voltage.

## v0.12.0 (2026-11-24)

- Changed: The user interface uses the AnWa-Soft design library (`AnWaSoft.Design.Wpf` 0.1.0). The look stays the same; four colours were adjusted to reach the WCAG AA contrast: warning text in light mode (`#B26A00` → `#9C5C00`), error text in dark mode (`#EF9A9A` → `#F2A5A5`), neutral and secondary text in dark mode (`#A9AEB8` → `#B3B8C1`), and the primary colour in dark mode (Indigo 300 `#7986CB` → Indigo 200 `#9FA8DA`).
- Changed: Message dialogs come from the design library.

## v0.11.0 (2026-11-24)

- Changed: New user interface. All windows have their own title bar in the application colours with a thin accent line instead of the Windows title bar.
- Changed: The main window shows the configuration on the left and the results on the right. The results start with an overall status, followed by one card per MPPT with a voltage window graphic, a table of the lowest and highest values against the limits, the allowed module count and the notes; the inverter limits are listed from the datasheet and with the safety margin. A status bar shows the overall result.
- Changed: Sliders show their value; the allowed module count is shown below the slider.
- Changed: Messages appear in dialogs in the application style; copy and save confirmations appear briefly at the bottom of the window.
- Changed: The report opens in the preview; Preview and Markdown switch the view.
- New: The results can be copied as text.

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
