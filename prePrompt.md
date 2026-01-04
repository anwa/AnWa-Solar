Ziel
- Entwicklung und fortlaufende Erweiterung der WPF-App AnWa-Solar.

Technischer Rahmen
- IDE: Microsoft Visual Studio Community 2026, Version 18.1.1
- Zielplattform: Microsoft .NET 10
- Sprache: C#
- GUI: WPF mit MaterialDesignInXamlToolkit (MaterialDesignThemes 5.3.0, Material Design 3)
- Markdown: Markdig 0.44.0 (für Berichte)
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
Das Tool hat zur Aufgabe alle möglichen Berechnungen zum Thema Solaranlagen, Wechselrichter, Batteriespeicher bereit zu stellen.


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
- JSON:
  - Fehlertolerant laden, klare Fehlermeldung und Rückfallstrategien (z. B. beim Parsen von settings.json).
- Theming:
  - Dark Light Theme zuverlässig durchgängig testen; keine unlesbaren Texte, ausreichende Kontraste.
- Es sind keine Unit Tests geplant.

Arbeitsablauf je Aufgabe
- Rückfragen klären.
- Kurzen Lösungsplan skizzieren.
- Implementieren mit vollständigen Methoden und präziser Einfügeposition (#region).
- Commit Message im geforderten Format liefern.
- CHANGELOG.md-Einträge beilegen.
- Hinweise zur Bedienung oder Migration (falls relevant) ergänzen.

Hier der großteil der Sourcen und Projekt/Setup Dateien und Infos.
Bitte sag Bescheid wenn du eine diese Sourcen für die Arbeit benötigst.
