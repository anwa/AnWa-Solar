# AnWa-Solar

AnWa-Solar ist eine WPF-Anwendung zur Auslegung und Berechnung von PV-Strings inklusive Wechselrichter- und Modul-Auswahl. Die App setzt auf Material Design 3 und bietet umfangreiches Logging. Calculation parameters and the last selection are stored per user in `%LOCALAPPDATA%\AnWa-Solar\settings.json`.

## Inhalt
- Überblick
- Funktionen
- Systemvoraussetzungen
- Installation und Start
- Datenquellen
- Bedienung
- Einstellungen und Persistenz
- Logging
- Design und Barrierefreiheit
- Architektur
- Roadmap
- Lizenz

## Überblick
Ziel der App ist es, alle wesentlichen Berechnungen rund um Solaranlagen bereitzustellen: von der Auswahl eines passenden Wechselrichters über die Konfiguration der Strings bis hin zur automatischen Prüfung von Spannungs-, Strom- und Leistungsgrenzen. Alle Änderungen werden sofort berechnet und übersichtlich dargestellt.

## Funktionen
- Vier Abschnitte im Tab „String-Berechnung“:
  - Wechselrichter: Auswahl per Dialog mit Hersteller-/Modell-Filter und Detailprüfung.
  - Strings: Dynamische Anzeige pro MPPT, Modulwahl pro MPPT, Anzahl Module pro String und parallele Strings mit Grenzprüfung.
  - Ergebnisse: Sofortige Live-Berechnung mit klaren Hinweisen und empfohlenen Bereichen.
  - Parameters: dialog for the minimum and maximum cell temperature (Tmin, default −20 °C; Tmax, default 70 °C) and the safety margin; stored in the user settings.
- Markdown-Vorschau: Markdown zu HTML-Konvertierung für Berichte.
- Persistenz:
  - Berechnungsparameter werden gespeichert und beim App-Start geladen.
  - Letzte Auswahl (Wechselrichter, Modul je MPPT, Module pro String, parallele Strings) wird gespeichert und automatisch wiederhergestellt.
- Strukturiertes Logging mit JSONL-Dateien, inkl. Leveln und aussagekräftigen Meldungen.

## Systemvoraussetzungen
- Windows 11 (x64)
- .NET 11 SDK (target framework `net11.0-windows`, SDK pinned in `global.json`)
- Visual Studio 2026 or the `dotnet` CLI

## Installation und Start
1. Clone the repository: https://github.com/anwa/AnWa-Solar.git
2. Build and test from the repository root:

   ```powershell
   dotnet build -warnaserror
   dotnet test
   ```

3. Start the application with `dotnet run --project src/AnWaSolar.App`, or open `AnWaSolar.slnx` in Visual Studio and start `AnWaSolar.App`.

## Datenquellen
- Folder `src/AnWaSolar.Core/Data/`, copied into the `Data` folder next to the executable
  - PV-Module.json: PV module datasheet values (power, UMPP/IMPP, UOC/ISC, temperature coefficients, operating temperature). The VMPP temperature coefficient (`Temperaturkoeffzient von VMPP (%/°C)`) is optional; without it the PMAX coefficient is used as a conservative approximation.
  - Wechselrichter.json: Stammdaten zu Wechselrichtern (DC-Spannungen, MPPT-Bereich, Ströme, Anzahl MPPT, max. Strings/MPPT).
- Both files are loaded at startup. Every entry is checked on its own (value types, MPPT range format such as `160-650`, positive values, UOC > UMPP, ISC ≥ IMPP, no duplicate manufacturer and model); invalid entries are skipped, and skipped entries or unreadable files are listed in a message after the start and in the log. Numeric datasheet values may be decimal numbers.

## Bedienung
- Wechselrichter:
  - Über „Wechselrichter auswählen“ Hersteller und Modell wählen; Details prüfen; „Übernehmen“ bestätigt.
  - Nach Auswahl erscheinen die MPPT-Abschnitte für Strings.
- Strings:
  - Pro MPPT ein Unterabschnitt.
  - „PV-Modul auswählen“ öffnet analoges Fenster für Modulwahl.
  - Anzahl Module pro String und parallele Strings via +/−-Buttons oder Direkteingabe anpassen.
  - „Max. Strings pro MPPT“ wird automatisch geprüft und ggf. begrenzt bzw. mit Hinweis versehen.
- Ergebnisse:
  - Live-Berechnung und Anzeige von Spannungen (OC/MPP), Strömen (ISC/IMPP), sowie Leistungsabschätzungen.
  - Prüfergebnisse zeigen Grenzverletzungen und Empfehlungen (Module pro String, parallele Strings).
- Parameter:
  - „Parameter ändern“ opens the dialog for Tmin, Tmax (cell temperatures) and the safety margin.
  - Werte werden gespeichert und gelten beim nächsten Start.

## Einstellungen und Persistenz
- User settings live in `%LOCALAPPDATA%\AnWa-Solar\settings.json`:
  - `Parameters`: MinTempC, MaxTempC, SicherheitsmargePct
  - `LastSelection`: inverter (Hersteller/Model) and per MPPT the module reference, ModuleProString, ParalleleStrings and IsEnabled
- Changes to inverter, modules and strings are saved automatically, at the latest 0.5 s after the last change and when the window closes. The file is written atomically.
- An unreadable `settings.json` is kept as `settings.json.invalid-<timestamp>` and the defaults are used.
- On the first start without `settings.json`, parameters and last selection are taken over once from the `appsettings.json` next to the executable, where versions up to 0.4.0 stored them.
- `appsettings.json` next to the executable only holds application defaults and is never written: `AnWaSolar:Markdown:UseAdvancedExtensions`, optionally `AnWaSolar:LogDirectory`.

## Logging
- Strukturierte JSONL-Dateien unter %LOCALAPPDATA%/AnWa-Solar/logs (konfigurierbar).
- Levels: INFO, WARNING, ERROR etc.
- Robuste Fehlerpfade: Parsing- und IO-Fehler werden geloggt, UI zeigt nutzerfreundliche Hinweise.

## Design und Barrierefreiheit
- Material Design 3 via MaterialDesignInXamlToolkit (BundledTheme, Defaults).
- Dark- und Light-Mode werden unterstützt (BaseTheme=Inherit).
- Konsistente Abstände, Typografie und Fokuszustände gemäß Referenz:
  - MaterialDesign3.Demo.Wpf
  - https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/tree/master/src/MaterialDesign3.Demo.Wpf
- States und Kontraste auf Lesbarkeit geprüft.

## Architektur
- `src/AnWaSolar.Core`: models, master data, string calculation (`Calculation/StringCalculator`), results text and Markdown report, services and logging, without WPF references. The live results and the report use the same calculation result.
- `src/AnWaSolar.App`: WPF application (windows, dependency injection through Microsoft.Extensions.Hosting).
- `tests/AnWaSolar.Core.Tests`: xUnit tests for Core.
- The version is defined once in `Directory.Build.props` and shown in the main window title; package versions are pinned in `Directory.Packages.props`.
- Services:
  - JsonDataStore: Laden der Stammdaten (System.Text.Json).
  - MarkdownService: Markdown→HTML via Markdig.
  - SettingsService: loads and saves parameters and the last selection in the user settings file.
- Logging:
  - Eigenes JsonFileLoggerProvider zur JSONL-Ausgabe.
- Modelle:
  - PVModule, Wechselrichter, StringConfiguration, CalculationParameters, LastSelection.

## Roadmap
- Erweiterte Ergebnisdarstellung (tabellarisch, farbliche Status für OK/WARN).
- Weitere Berechnungen (Batteriespeicher, AC-seitige Betrachtung, Kabel-/Sicherungsdimensionierung).
- Exportfunktionen (Markdown/HTML/PDF-Bericht).

$$V_{oc} (T_{STC}) * (1 + ((T_{min} - 25°C) * Temperaturkoeffizient))$$

$V_{oc} (T_{STC})= 45,02V$
$T_{min}= -20°C$
$Temperaturkoeffizient= -0,22\%/°C$

$V_{oc_{min}} = 45,02V * (1 + ((-20°C - 25°C) * -0,22\%/°C))$
$V_{oc_{min}} = 45,02V * (1 + ((-45°C) * -0,22\%/°C))$
$V_{oc_{min}} = 45,02V * (1 + (9,9\%)$
$V_{oc_{min}} = 45,02V * (1,099)$
$V_{oc_{min}} = 49,47698V$



## Lizenz
- Siehe LICENSE im Repository.
