# Projekte

Jede Auslegung ist ein Projekt: eine Datei mit allem, was du eingegeben hast, den Projektdaten, den Parametern, dem Wechselrichter, den Strings mit ihren Modulen, den Kabeleinstellungen und der Batterie.

## Projekt beim Start wählen

Beim Start von AnWa-Solar erscheint der Dialog **Projekt öffnen**:

- **Letzte Projekte** listet die fünf zuletzt geöffneten Projekte. Das letzte Projekt ist vorausgewählt: **Enter** oder **Öffnen** setzt es fort. Ein Doppelklick öffnet ein anderes Projekt der Liste.
- **Neues Projekt…** fragt die Projektdaten ab und startet mit einer leeren Auslegung und den Standardwerten neuer Projekte aus den [Einstellungen](settings.md#einstellungsseite).
- **Andere…** öffnet eine Projektdatei, die nicht in der Liste steht.
- **Beenden** beendet AnWa-Solar.

Beim ersten Start einer Version mit Projekten wird deine bisherige Auslegung zum Projekt **Mein Projekt**.

## Die Projektkarte

Die Karte **Projekt** oben in der Konfiguration zeigt Name, Kunde und Adresse:

| Knopf | Funktion |
|---|---|
| Bearbeiten | Ändert Name, Kunde, Adresse der PV-Anlage und Notiz. Sie stehen am Anfang des [Berichts](report.md). |
| Projekte | Öffnet den Projektdialog, um zu einem anderen Projekt zu wechseln oder ein neues anzulegen. Das aktuelle Projekt wird vorher gespeichert. |
| ZIP exportieren | Speichert eine ZIP-Datei, siehe unten. |

Der Fenstertitel zeigt den Namen des offenen Projekts.

## Speichern

Jede Änderung wird automatisch im Projekt gespeichert, spätestens eine halbe Sekunde nach der letzten Änderung, beim Projektwechsel und beim Schließen des Fensters. Projekte liegen je Windows-Benutzer in `%LOCALAPPDATA%\AnWa-Solar\Projects`, eine JSON-Datei pro Projekt, benannt nach dem Projekt; in den [Einstellungen](settings.md#einstellungsseite) lässt sich ein anderer Projektordner festlegen.

Für den Wechselrichter, jedes Modul und die Batterie speichert das Projekt Hersteller und Modell, die Datenblattdatei und eine Kopie der Datenblattwerte. Beim Öffnen eines Projekts gelten die Einträge der [Stammdaten](master-data.md), damit korrigierte Werte wirken. Fehlt ein Eintrag, zum Beispiel auf einem anderen Computer, wird die Kopie aus dem Projekt verwendet und eine Meldung nennt diese Einträge; importiere ihre Datenblätter, um sie in die Stammdaten aufzunehmen.

## Export als ZIP

**ZIP exportieren** speichert eine ZIP-Datei mit einem Ordner, der nach dem Projekt benannt ist, in der Sprache der Anwendung (Bericht, Dateinamen und die Liste fehlender Datenblätter):

| Datei | Inhalt |
|---|---|
| `<Projekt> - Bericht.pdf` | Der Bericht als A4-PDF, beim Export erstellt. Ohne Berichtsvorschau (Microsoft Edge WebView2) kommt der Bericht stattdessen als Markdown ins ZIP. |
| `Datenblätter\<Hersteller> <Modell>.pdf` | Die Datenblätter des Wechselrichters, der Module und der Batterie, soweit sie importiert wurden. |
| `<Projekt>.json` | Die Projektdatei; **Andere…** im Projektdialog öffnet sie wieder. |
| `Fehlende Datenblätter.txt` | Nur wenn ein Eintrag kein Datenblatt hat, zum Beispiel bei mitgelieferten Stammdaten. |
