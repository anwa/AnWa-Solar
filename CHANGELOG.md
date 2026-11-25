# Changelog

All user-visible changes of AnWa-Solar.

## v0.19.0 (2026-11-25)

- New: Projects. Every design is a JSON file in `%LOCALAPPDATA%\AnWa-Solar\Projects` with name, customer, address and notes, the parameters, the inverter, the strings, the cable settings and the battery. Inverter, modules and battery are stored with their key, datasheet file and a copy of their values; the master data entry is used when it exists, otherwise the copy, with a message. Changes are saved automatically.
- New: At the start, a dialog opens the last project (preselected, Enter), one of the five most recent projects, another project file or a new project. The design of the previous version becomes the project "My project".
- New: Project card with Edit, Projects (switch or create) and Export ZIP; the window title and the report show the project. The ZIP contains one folder with the report as PDF, the imported datasheets, the project file and a list of entries without datasheet.
- Changed: The settings file keeps only the theme and the recent projects; the design moved into the project.

## v0.18.1 (2026-11-25)

- Changed: The battery cable values in `Data/CableTables.json` (section `BatteryCable`) are checked by the user and marked confirmed; the cable card, the report and the copied text no longer show the note about unconfirmed battery cable values. The values themselves are unchanged.

## v0.18.0 (2026-11-25)

- New: Module leads. The string voltage drop includes the plus and minus leads of every module of the string; the lengths and cross-section come from the module datasheet (new optional master data values, read by the datasheet import, for example "4,0 mm², Hochformat 1100/1100 mm"), otherwise from a default in the cable settings (0.5 m per lead, 4 mm²). The result card shows them per MPPT.
- New: The cross-section of the string cables can be chosen (for example 4 or 6 mm²) or sized automatically; a chosen cross-section is checked: too low a current-carrying capacity is an error, a voltage drop above the limit a warning.
- New: Battery cables. With a busbar, each battery has a cable to the busbar and a main cable runs to the inverter; without busbar, each battery has its own cable to the inverter. Each cable gets a fuse (Ib ≤ In ≤ Iz); cross-sections automatic or chosen; voltage drop of the nominal bank voltage; a warning when there are more battery cables than battery inputs of the inverter.
- Changed: The status line also points to warnings of the cables and fuses.
- New: Section `BatteryCable` in `Data/CableTables.json` with current-carrying capacities of single-core copper cables 25–150 mm² (IEC 60364-5-52 Table B.52.12, method F), fuse ratings 32–500 A and a voltage drop limit of 1 %; common values entered at the user's request, marked unconfirmed until the user has checked them.

## v0.17.2 (2026-11-25)

- Changed: The standard tables for cables and fuses in `Data/CableTables.json` are checked by the user and marked confirmed; the card, the report and the copied text no longer show the note about unconfirmed values. The values themselves are unchanged.

## v0.17.1 (2026-11-25)

- Fixed: Changing the inverter removed the PV modules and reset the strings. Each MPPT now keeps its module, modules per string, parallel strings and enabled state; values the new inverter does not allow stay set and are shown as violations instead of being changed silently. Strings of MPPTs the new inverter does not have come back when an inverter with more MPPTs is selected again.

## v0.17.0 (2026-11-25)

- New: Battery storage compatibility. A configuration card for hybrid inverters with a battery dialog (battery module, modules in series and in parallel), a result card and a report section check the bank voltage against the battery voltage range of the inverter (outside the range is an error; a lower bank voltage that the inverter cannot use is a warning), the module counts against the battery datasheet, and show the charge and discharge current (the lower of bank and inverter) with the power and the nominal and usable energy. A manual page describes it.
- New: Battery master data `Data/Batteries.json` with the Deye SE-F12, SE-F12 Max, SE-F16, SE-F16 Max and the Felicity FLA48314-EU from their datasheets; own batteries through the datasheet import, which now reads battery datasheets (also with model names as graphics and ranges like "44,8 V ~ 57,6 V").
- New: Inverter battery values (battery voltage range, max. charge and discharge current, battery inputs) in the master data and the datasheet import; the shipped Deye SUN-15K/18K/20K-SG05LP3-EU-SM2 have them from the datasheet.

## v0.16.0 (2026-11-25)

- New: Cables and fuses. A configuration card with a settings dialog takes the cable lengths and installation conditions; a result card and a report section size the string cables per MPPT (design current 1.25 × Isc, voltage drop at the MPP), decide on string fuses after IEC 62548 (needed when (Np − 1) × 1.25 × Isc exceeds the module's max. series fuse rating, rated 1.5–2.4 × Isc), and size the AC circuit breaker and line from the max. AC current.
- New: Standard tables in `Data/CableTables.json` with their sources: current-carrying capacities of solar cables (EN 50618) and of PVC copper cables (IEC 60364-5-52 / DIN VDE 0298-4), temperature and grouping factors, copper conductivity, standard fuse and breaker ratings, voltage drop limits (DC 1 %, AC 3 %). They are common values that the user still has to confirm; until then the card and the report say so.
- New: Module datasheet value max. series fuse rating, in the master data and the datasheet import.

## v0.15.0 (2026-11-24)

- New: AC side and grid connection. A result card and a report section show the PV peak power, the DC/AC ratio against a recommended range (default 0.9–1.3), the apparent power against the limit of the simplified grid registration (default 30 kVA), the unbalanced load limit of single-phase inverters (default 4.6 kVA, a violated limit), the AC current per phase and an optional feed-in limit. The limits are set in the parameter dialog.
- New: Inverter AC datasheet values (rated AC power, max. apparent power, rated and max. AC current per phase, phases) in the master data and in the datasheet import; values like "27.3/26.1" are read for 230/400 V. The shipped Deye SUN-15K/18K/20K-SG05LP3-EU-SM2 have them from the datasheet; the SUN-10K-G06P3-EU-AM2 has none yet.
- New master data: inverter Deye SUN-10K-G06P3-EU-AM2, modules Ja-Solar JAM54D40-460/LB and Trina TSM-450-NEG9R.25.
- Fixed: The temperature coefficient of Isc of all shipped modules (Ja-Solar JAM54D40-460/LB, Trina TSM-455-NEG9R.28 and TSM-450-NEG9R.25, AIKO-A500-MAH60Db) had a wrong minus sign; Isc rises with the temperature, as the datasheets state. With the default cell temperatures (-20 and 70 °C, both 45 K from 25 °C) the highest current was the same; with other temperatures the current when hot was too low. A test now requires a positive Isc coefficient for every shipped module.

## v0.14.0 (2026-11-24)

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
