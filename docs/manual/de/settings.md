# Einstellungen, Design und Dateien

## Einstellungsseite

Der Einstellungs-Knopf (Zahnrad) in der Titelleiste öffnet die Einstellungen:

| Einstellung | Bedeutung |
|---|---|
| Sprache | Deutsch oder Englisch oder die Windows-Sprache. Gilt nach einem Neustart; AnWa-Solar bietet den Neustart an. Zahlen und Datum folgen der Sprache (Deutsch 28,7 A und 25.11.2026, Englisch 28.7 A und 25/11/2026). Die Fenstertexte, die Ergebnisse, der Bericht, der Projektexport und dieses Handbuch verwenden die Sprache. |
| Design | Windows-Design, hell oder dunkel; dasselbe wie der Design-Knopf. |
| Projektordner | Wo Projekte gespeichert und gesucht werden, standardmäßig `%LOCALAPPDATA%\AnWa-Solar\Projects`. Gilt nach einem Neustart. **Standard** setzt wieder den Standardordner. |
| Exportordner | Wo die Speichern-Dialoge von Bericht und Projektexport starten. Leer: der Ordner, den Windows sich merkt. |
| Standardwerte neuer Projekte | **Parameter…** und **Kabel…** legen die Berechnungsparameter und Kabeleinstellungen fest, mit denen ein neues Projekt startet. Vorhandene Projekte behalten ihre eigenen Werte. |

## Design

Der Design-Knopf in der Titelleiste wechselt in dieser Reihenfolge:

1. **System**: folgt der Windows-Einstellung für Apps (hell oder dunkel) und wechselt mit, wenn du sie in Windows änderst.
2. **Hell**
3. **Dunkel**

Der Tooltip des Knopfs zeigt den aktuellen Modus. Die Berichtsvorschau und dieses Handbuch folgen dem Design. Die Wahl wird gespeichert.

## Gespeicherte Einstellungen

AnWa-Solar speichert deine Einstellungen je Windows-Benutzer in `%LOCALAPPDATA%\AnWa-Solar\settings.json`:

- das Design,
- die fünf zuletzt geöffneten Projekte,
- die Einstellungen der Einstellungsseite (Sprache, Ordner, Standardwerte neuer Projekte).

Deine Auslegung (Parameter, Wechselrichter, Strings, Kabel, Batterie) wird im offenen [Projekt](projects.md) gespeichert.

Änderungen werden automatisch gespeichert, spätestens eine halbe Sekunde nach der letzten Änderung und beim Schließen des Fensters. Ist die Datei nicht lesbar, behält AnWa-Solar sie als `settings.json.invalid-<Datum>` und startet mit den Standardwerten.

Einstellungen älterer Versionen werden beim ersten Start automatisch übernommen; die darin enthaltene Auslegung wird zum Projekt **Mein Projekt**.

## Dateien und Ordner

| Pfad | Inhalt |
|---|---|
| `C:\Program Files\AnWa-Solar` | Programm, die Stammdaten-Serien in `Data\Modules`, `Data\Inverters` und `Data\Batteries`, `Data\CableTables.json` (Normtabellen für Kabel und Sicherungen), `appsettings.json` |
| `%LOCALAPPDATA%\AnWa-Solar\settings.json` | Deine Einstellungen |
| `%LOCALAPPDATA%\AnWa-Solar\Projects` | Deine Projekte, je eine JSON-Datei, siehe [Projekte](projects.md) |
| `%LOCALAPPDATA%\AnWa-Solar\Data` | Deine eigenen Stammdaten und importierten Datenblätter, siehe [Datenblatt-Import](datasheet-import.md#eigene-stammdaten) |
| `%LOCALAPPDATA%\AnWa-Solar\logs` | Logdateien, eine pro Tag (`log-YYYY-MM-DD.jsonl`) |
| `%LOCALAPPDATA%\AnWa-Solar\WebView2` | Daten der Berichtsvorschau und dieses Handbuchs |

Das Fenster **Über** zeigt die genauen Pfade auf deinem Computer; **Kopieren** kopiert sie in die Zwischenablage.

Bei der Deinstallation von AnWa-Solar bleibt der Ordner `%LOCALAPPDATA%\AnWa-Solar` erhalten. Lösche ihn von Hand, um deine Einstellungen, Logs und eigenen Stammdaten zu entfernen.
