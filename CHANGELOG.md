
- Neu: Die zuletzt gewählte Konfiguration (Wechselrichter, PV-Module je MPPT, Module pro String und parallele Strings) wird in appsettings.json gespeichert und beim Start automatisch geladen.
- Verbesserung: Komfortsteigerung durch automatische Wiederherstellung des Arbeitsstands.
- Hinweis: Schreiben in appsettings.json erfordert Schreibrechte im Ausgabeverzeichnis; für produktive Umgebungen kann ein benutzerspezifischer Speicherort verwendet werden.

v0.1.0
- Fehlerbehebung: Build-Probleme durch falsche MaterialDesign-Namespace-Verwendung in MainWindow.xaml.cs korrigiert.
- Fehlerbehebung: Warnung CS8633 im JsonFileLogger durch Hinzufügen des notnull-Constraints behoben.
- Verbesserung: Fensterkonstruktoren vereinfacht, Logging zentralisiert im MainWindow.
- Neu: Tab „String-Berechnung“ in vier klar getrennte Abschnitte strukturiert (Wechselrichter, Strings, Ergebnisse, Parameter).
- Neu: Auswahlfenster für Wechselrichter und PV-Module mit Hersteller-/Modell-Filter und Detailprüfung.
- Neu: Parameter-Dialog mit Persistenz in appsettings.json; Parameter werden beim Start geladen.
- Neu: Dynamische Anzeige je MPPT gemäß „Anzahl der MPP Trackers“; sofortige Berechnung bei Änderungen.
- Hinweis: „Max. Strings pro MPPT“ wird berücksichtigt und begrenzt parallele Strings entsprechend.
- Design: Material Design 3, konsistente Abstände und Dark/Light-Mode Unterstützung entsprechend dem bestehenden Theme.
