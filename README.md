# AnWa-Solar

AnWa-Solar ist eine WPF-Anwendung zur Auslegung und Berechnung von PV-Strings inklusive Wechselrichter- und Modul-Auswahl. Die App setzt auf Material Design 3, bietet umfangreiches Logging und speichert sowohl Berechnungsparameter als auch die letzte Auswahl in appsettings.json.

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
  - Parameter: Dialog für Tmin, Tmax und Sicherheitsmarge; Speicherung in appsettings.json.
- Markdown-Vorschau: Markdown zu HTML-Konvertierung für Berichte.
- Persistenz:
  - Berechnungsparameter werden gespeichert und beim App-Start geladen.
  - Letzte Auswahl (Wechselrichter, Modul je MPPT, Module pro String, parallele Strings) wird gespeichert und automatisch wiederhergestellt.
- Strukturiertes Logging mit JSONL-Dateien, inkl. Leveln und aussagekräftigen Meldungen.

## Systemvoraussetzungen
- Windows 11
- .NET 10 (TargetFramework net10.0-windows)
- Visual Studio Community 2026 (oder kompatibel)
- Keine Abhängigkeit zu Newtonsoft.Json

## Installation und Start
1. Repository klonen:
   - https://github.com/anwa/AnWa-Solar.git
2. Projekt öffnen:
   - AnWa-Solar.slnx oder AnWa-Solar.csproj in Visual Studio öffnen.
3. NuGet-Pakete werden automatisch wiederhergestellt.
4. Starten:
   - Konfiguration „Debug“ oder „Release“ auswählen und die App starten.

## Datenquellen
- Verzeichnis Data/
  - PV-Module.json: Stammdaten zu PV-Modulen (Leistung, UMPP/IMPP, UOC/ISC, Temperaturkoeffizienten, Betriebstemperatur).
  - Wechselrichter.json: Stammdaten zu Wechselrichtern (DC-Spannungen, MPPT-Bereich, Ströme, Anzahl MPPT, max. Strings/MPPT).
- Beide Dateien werden beim Start geladen. Bei Fehlern oder fehlenden Dateien wird dies geloggt und robust behandelt.

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
  - „Parameter ändern“ öffnet den Dialog zum Setzen von Tmin, Tmax und Sicherheitsmarge.
  - Werte werden gespeichert und gelten beim nächsten Start.

## Einstellungen und Persistenz
- appsettings.json (Ausgabeverzeichnis):
  - AnWaSolar.Markdown.UseAdvancedExtensions
  - AnWaSolar.LogDirectory
  - AnWaSolar.Parameters: MinTempC, MaxTempC, SicherheitsmargePct
  - AnWaSolar.LastSelection: Inverter (Hersteller/Model), Strings (je MPPT: Modul-Referenz, ModuleProString, ParalleleStrings)
- Beim Ändern von Wechselrichter, Modul oder String-Konfiguration werden die Angaben gespeichert.
- Hinweis: Das Schreiben in appsettings.json erfordert Schreibrechte im Ausgabeverzeichnis. Für produktive Umgebungen kann alternativ ein benutzerspezifischer Speicherpfad (z. B. %LOCALAPPDATA%/AnWa-Solar/user-settings.json) verwendet werden.

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
- WPF mit DI über Microsoft.Extensions.Hosting.
- Services:
  - JsonDataStore: Laden der Stammdaten (System.Text.Json).
  - MarkdownService: Markdown→HTML via Markdig.
  - SettingsService: Laden/Speichern von Parametern und letzter Auswahl in appsettings.json.
- Logging:
  - Eigenes JsonFileLoggerProvider zur JSONL-Ausgabe.
- Modelle:
  - PVModule, Wechselrichter, StringConfiguration, CalculationParameters, LastSelection.

## Roadmap
- Erweiterte Ergebnisdarstellung (tabellarisch, farbliche Status für OK/WARN).
- Weitere Berechnungen (Batteriespeicher, AC-seitige Betrachtung, Kabel-/Sicherungsdimensionierung).
- Exportfunktionen (Markdown/HTML/PDF-Bericht).
- Benutzerbezogene Settings-Datei im User-Profil (optional, produktionsgeeignet).

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
