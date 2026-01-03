Ziel
- Entwicklung und fortlaufende Erweiterung der WPF-App AnWa-Solar.

Technischer Rahmen
- IDE: Microsoft Visual Studio Community 2026, Version 18.1.1
- Zielplattform: Microsoft .NET 10
- Sprache: C#
- GUI: WPF mit MaterialDesignInXamlToolkit (MaterialDesignThemes 5.3.0, Material Design 3)
- Markdown: Markdig 0.44.0
- JSON: System.Text.Json (keine Newtonsoft-Abhängigkeit)
- App-Name: AnWa-Solar
- In-App-Sprache (UI-Texte, Kommentare im Code, Logging): Deutsch
- Logging: Ausführliches, strukturiertes Logging mit Levels (INFO, WARNING, ERROR, etc.), verständliche deutschen Meldungen

UI und Designrichtlinien
- Konsistente Gestaltung über alle Fenster, Dialoge und Tabs, angelehnt an Material Design 3.
- Unbedingt Dark- und Light-Mode gleichermaßen gut bedienbar machen.
- Referenzdesign: MaterialDesign3.Demo.Wpf
  - https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/tree/master/src/MaterialDesign3.Demo.Wpf
- Konsistente Abstände, Typografie, Farbkontraste, Fokus-Status, States für Buttons, Listenelemente und Tabs.
- Bei Abweichungen vom Styleguide: klare Begründung im Code-Kommentar (deutsch) und minimal-invasive Anpassungen.

Funktionaler Rahmen (High-Level, zur Orientierung)
Als erstes will ich ein TAB-Orientiertes Design und der erste TAB soll die Solar String Berechnung enthalten.
Dazu soll auf daten in 2 json Files zugegriffen werden. Bitte erstelle eine PV-Module.json und eine Wechselrichter.json die alle dafür notwendigen Daten (Felder) enthält.
Dazu gehören bei den Solarmodulen:
- Hersteller: Trina
- Model: TSM-455-NEG9R.28
- Nominalleistung-PMAX (Wp): 455
- Spannung im MPP-UMPP (V): 45,0
- Strom im MPP-IMPP (A): 10,11
- Leerlaufspannung-UOC (V): 53,4
- Kurzschlusstrom-ISC (A): 10,77
- Modulwirkungsgrad η m (%): 22,8
- Temperaturkoeffzient von PMAX (%/°C): -0,29
- Temperaturkoeffzient von VOC (%/°C): -0,24
- Temperaturkoeffzient von ISC (%/°C): - 0,04

Bei den Wechselrichter:
- Hersteller: Deye
- Model: SUN-18K-SG05LP3-EU-SM2
- Max.DC-Eingangsleistung (W): 28800
- Max.DC-Eingangsspannung (V): 800
- Startspannung (V): 160
- MPPT-Spannungsbereich (V): 160-650
- Nenn-DC-Eingangsspannung (V): 550
- Max. Betriebs-PV-Eingangsstrom (A): 36
- Max. Eingangs-Kurzschlussstrom (A): 54
- Anzahl der MPP Trackers/Anzahl der Strings MPP Tracker: 2

Die Werte entsprechen dem ersten Beispiel
Beim Starten der Anwendung sollen diese beiden Dateien eingelesen werden.

Das Tab soll volgendermaßen aufgebaut sein:
Zuerst kommt der Wechselrichter:
Es gibt eine "Wähle Wechselrichter" Button, darauf hin öffnet sich ein kleines Fenster wo man den Wechselrichter auswählen kann.
Dazu gibt es ein Dropdown für den Hersteller, und eines mit dem Model. Modell enthält nur die Modelle des Herstellers.
Nach Auswahl werden die Daten in einer Tabelle dargestellt zur überprüfung.
Mit Übernehmen bestätigt man die Auswahl des Wechselrichters (Fenster schließen), mit Abbrechen schließt man das Fenster ohne zu übernehmen.
Anschließend wird der ausgewählte WR im TAB Abschnitt Wechselrichter mit seinen Wichtigsten Daten angezeigt.

Genau das gleiche soll für die PV Module gemacht werden im Abschnitt PV-Module.

Gut, das wäre jetzt erstmal ein guter Start mit den grundlegenden Funktionen. Lass uns das sauber umsetzten um dann weiter daran zu arbeiten.

Arbeitsweise und Antwortformat
- Klärungsprinzip: Wenn Informationen fehlen oder Annahmen nötig sind, zunächst gezielt nachfragen, bevor Code geliefert wird.
- Vorgehen vor Code:
  - Kurze Lösungs-Skizze mit betroffenen Klassen Dateien Komponenten.
  - Eindeutige Beschreibung, wo der Code eingefügt wird.
- Code-Lieferung:
  - Immer die komplette geänderte Funktion Methode zurückgeben, nicht nur Diff-Fragmente.
  - Wenn eine neue Klasse Methode Funktion erstellt wird:
    - Exakt angeben: Projekt und Pfad im Solution-Baum, Ziel-Datei, Ziel-Namespace.
    - Angeben, unter welcher #region die neue Funktion Methode Variable eingefügt werden soll.
    - Position einhalten: Vorhandene Funktionen Methoden nicht verschieben.
  - #region #endregion zur logischen Strukturierung konsequent verwenden.
  - Vorhandene Kommentare nur ändern, wenn an der Klasse Funktion Methode inhaltlich etwas geändert wurde. Für neue Funktionen Kommentare analog zur bestehenden Kommentierweise verfassen.
- Sprache:
  - Alle In-App-Strings, Code-Kommentare und Log-Meldungen in Deutsch halten.
  - Chatsprache bleibt Deutsch.
- Bitte in allen Programmcode-Antworten keine Escape-Sequenzen (Backslash-n, Backslash-r, Backslash-t, Backslash-Anführungszeichen) verwenden.
  - Stattdessen (char)10/(char)13/(char)9 oder char '"', oder z. B. TrimEnd('\u000D', '\u000A'). 
  - Für Zeilenumbrüche Environment.NewLine bzw. StringBuilder.AppendLine() nutzen.
  - In Kommentaren, Commit-Messages und README/CHANGELOG sind normale Zeilenumbrüche und Escape-Sequenzen erlaubt.
- Logging:
  - Aussagekräftige deutsche Meldungen und kontextreiche Parameter. Klare Levels: INFO, WARNING, ERROR etc.
  - Fehlerpfade mit robustem Exception-Handling, gezielter Message, ggf. Benutzerhinweis im UI.
- Serialisierung:
  - System.Text.Json verwenden, passend konfigurieren (Case, Indent, Encoder falls nötig).
- Commit Message:
  - Format wie im Beispiel:
    - style(scope): kurze, prägnante Zusammenfassung
    - Danach stichpunktartig die Änderungen mit konkreten Klassen Komponenten.
    - in Deutsch
- CHANGELOG:
  - CHANGELOG.md in Deutsch aktualisieren (nur geänderte Einträge), in einem für Endnutzer verständlichen Stil. Kurz, funktional, ohne Entwicklerjargon.

Qualität und Robustheit
- Pfad- und Symlink-Operationen:
  - Existenzprüfungen, klare Fehlerhinweise, robuste Handhabung fehlender Berechtigungen.
  - Windows-Symlinks: Prüfen, ob unter den gegebenen Nutzerrechten erstellt werden können; falls nicht, Benutzer führen (Hinweis auf Adminrechte oder Developer Mode).
- Markdown-Rendering:
  - Markdig-Pipeline konsistent konfigurieren; große Dateien performant laden; UI nicht blockieren (Async, falls nötig).
- JSON:
  - Fehlertolerant laden, klare Fehlermeldung und Rückfallstrategien (z. B. beim Parsen von settings.json).
- Theming:
  - Dark Light Theme zuverlässig durchgängig testen; keine unlesbaren Texte, ausreichende Kontraste.
- Es sind keine Unit Tests geplant.
- baseDrive ist ein gemapptes Laufwerk (z. B. X:)

Arbeitsablauf je Aufgabe
- Rückfragen klären.
- Kurzen Lösungsplan skizzieren.
- Implementieren mit vollständigen Methoden und präziser Einfügeposition (#region).
- Commit Message im geforderten Format liefern.
- CHANGELOG.md-Einträge beilegen.
- Hinweise zur Bedienung oder Migration (falls relevant) ergänzen.

Hier der großteil der Sourcen und Projekt/Setup Dateien und Infos.
Bitte sag Bescheid wenn du eine diese Sourcen für die Arbeit benötigst.
Aktuell handelt es sich nur um ein frisch erstelltes Visual Studio Projekt, mit sehr wenig anpassungen.
Es sollte restrukturiert werden in der form das die sourcen in subfolder erstellt werden wie:
- Helpers
- Models
- Resources
- Services
- ViewModels
- Views
usw.
