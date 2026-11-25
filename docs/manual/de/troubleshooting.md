# Fehlerbehebung

## „Einige Stammdaten konnten nicht geladen werden“

Nach dem Start listet eine Meldung die übersprungenen Stammdaten-Einträge mit ihrer Seriendatei und dem Grund. Korrigiere den Eintrag wie unter [Stammdaten](master-data.md#prüfungen-beim-laden) beschrieben, zum Beispiel in der [Komponentendatenbank](component-database.md). Die übrigen Einträge stehen zur Verfügung.

## Wechselrichter oder Modul eines Projekts fehlt in den Stammdaten

Ein Projekt verweist auf Hersteller und Modell und speichert eine Kopie der Werte. Fehlt ein Eintrag in den Stammdaten, verwendet AnWa-Solar die Kopie und nennt den Eintrag in einer Meldung; importiere das Datenblatt, um ihn wieder aufzunehmen, siehe [Projekte](projects.md#speichern).

## „Kein gültiger Bereich“ bei den Modulen pro String

Das Modul passt nicht zum Wechselrichter: Entweder bleibt die Stringspannung bei Tmax selbst mit den meisten Modulen, die die maximale DC-Spannung erlaubt, unter der MPPT-Untergrenze oder der Startspannung, oder umgekehrt. Wähle ein anderes Modul oder einen anderen Wechselrichter, oder prüfe die Parameter und den Sicherheitsabschlag.

## Berichtsvorschau oder Handbuch bleiben leer

Beide verwenden die Microsoft Edge WebView2 Runtime, die zu Windows 11 gehört. Fehlt sie oder ist sie beschädigt, zeigt AnWa-Solar eine Meldung, und die Registerkarte Bericht zeigt den Markdown-Quelltext. Installiere oder repariere die „Microsoft Edge WebView2 Runtime“ von Microsoft und starte AnWa-Solar neu.

## Einstellungen wurden zurückgesetzt

Ist `settings.json` nicht lesbar, startet AnWa-Solar mit den Standardwerten und behält die alte Datei als `settings.json.invalid-<Datum>` in `%LOCALAPPDATA%\AnWa-Solar`, siehe [Einstellungen, Design und Dateien](settings.md#gespeicherte-einstellungen).

## Logdateien

AnWa-Solar schreibt pro Tag eine Logdatei nach `%LOCALAPPDATA%\AnWa-Solar\logs`. Hänge das Log des Tages an, wenn du dem Autor ein Problem meldest (siehe **Über**). Die Logs sind englisch.
