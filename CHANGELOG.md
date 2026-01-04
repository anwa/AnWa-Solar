Kurzbeschreibung als Header (bitte ganz oben einfügen)

# AnWa-Solar – Solaranlagen- und String-Berechnung
AnWa-Solar ist ein WPF-Tool zur Auslegung und Berechnung von PV-Strings mit Wechselrichtern und Batteriespeicher-Bezug. Die App bietet eine moderne Material Design 3 Oberfläche, sofortige Live-Berechnungen bei Änderungen sowie eine komfortable Markdown-Vorschau für Berichte. Einstellungen und letzte Auswahl werden in appsettings.json gespeichert und beim Start wiederhergestellt.

Unveröffentlich
- Neu: In den String-Abschnitten steuern Slider die Anzahl der Module pro String und die Anzahl paralleler Strings. Grenzen werden dynamisch aus den Berechnungsergebnissen abgeleitet.
- Neu: ToggleButton pro String zum schnellen Aktivieren/Deaktivieren, ohne die Daten zu verlieren.
- Verbesserung: Sofortige Aktualisierung der Slider-Bereiche bei jeder Änderung von WR, Modulen und Parametern.
- Neu: Der Aktivierungsstatus (Toggle) pro MPPT wird in appsettings.json gespeichert und beim Start automatisch wiederhergestellt.
- Verbesserung: Komfortsteigerung beim Arbeiten mit temporär deaktivierten Strings ohne Datenverlust.
- Verbesserung: „Anzahl Module“ belegt 3/4 der Breite, „Anzahl paralleler Strings“ 1/4 für bessere Bedienbarkeit.
- Neu: Slider „Anzahl Module“ mit fester Grenzen 1..(zulässiges Max + 5) und Markierung des zulässigen Bereichs (SelectionStart/End).
- Neu: Buttontexte passen sich dem Zustand an („PV-Modul auswählen“ → „PV-Modul ändern“), zusätzlich „Modul entfernen“ pro MPPT.
- Verbesserung: Wechselrichter-Button passt sich nach Auswahl zu „Wechselrichter ändern“ an.

v0.1.2
- Neu: Bericht-Tab mit automatisch erzeugtem Markdown-Bericht, umschaltbar zwischen Markdown-Quelle und HTML-Vorschau.
- Neu: Buttons zum Kopieren und Speichern des Markdown-Quelltexts.
- Verbesserung: Bericht enthält strukturierte Berechnungshinweise und pro-MPPT-Auswertung, basierend auf der aktuellen Konfiguration.
- Fehlerbehebung: Build-Fehler behoben, alte Markdown-Controls entfernt; Berichtsvorschau stabilisiert.

v0.1.1
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
