# Erste Schritte

## Installation

Installiere `AnWaSolar-Setup-X.Y.Z.msi`. AnWa-Solar wird für alle Benutzer nach `C:\Program Files\AnWa-Solar` installiert und bekommt einen Eintrag im Startmenü. Eine neuere Version ersetzt die installierte; deine Einstellungen bleiben erhalten. AnWa-Solar läuft unter Windows 11 (x64) und braucht keine eigene .NET-Installation.

## Das Hauptfenster

Beim Start fragt der Dialog **Projekt öffnen** nach dem Projekt: **Enter** setzt das letzte Projekt fort, oder du legst ein neues Projekt an, siehe [Projekte](projects.md).

Die blaue Titelleiste zeigt die Version und den Namen des offenen Projekts. Rechts liegen sechs Knöpfe, danach die Fensterknöpfe (minimieren, maximieren, schließen):

| Knopf | Funktion |
|---|---|
| Datenblatt importieren | Liest ein PDF-Datenblatt in deine eigenen Stammdaten ein, siehe [Datenblatt-Import](datasheet-import.md). |
| Komponentendatenbank | Zeigt und bearbeitet die Modul-, Wechselrichter- und Batterieserien, siehe [Komponentendatenbank](component-database.md). |
| Design | Wechselt zwischen Windows-Design, hellem und dunklem Modus, siehe [Einstellungen, Design und Dateien](settings.md#design). |
| Handbuch | Öffnet dieses Handbuch (F1). |
| Einstellungen | Sprache, Design, Ordner und die Standardwerte neuer Projekte, siehe [Einstellungen, Design und Dateien](settings.md#einstellungsseite). |
| Über | Zeigt Version, Installationsordner, Datenordner, Kontakt und Lizenz. |

Das Hauptfenster hat zwei Registerkarten:

- **Stringberechnung**: links die **Konfiguration** (Projekt, Wechselrichter, Parameter, Kabel, Batterie und eine Karte pro MPPT), rechts die **Ergebnisse**.
- **Bericht**: ein Markdown-Bericht der aktuellen Auslegung, siehe [Bericht](report.md).

## Eine erste Auslegung in fünf Schritten

1. Klicke in der Karte **Wechselrichter** auf **Wählen**, wähle Hersteller, Typ, Serie und Leistungsklasse und klicke auf **Übernehmen**.
2. Klicke in jeder MPPT-Karte auf **Modul wählen** und wähle das Modul.
3. Stelle mit dem ersten Schieberegler **Module pro String** ein. Der markierte Teil des Reglers ist der zulässige Bereich.
4. Stelle mit dem zweiten Schieberegler **Parallele Strings** ein.
5. Lies rechts die **Ergebnisse**. Das Band oben zeigt das Gesamtergebnis: grün „Alle Grenzen eingehalten“ oder rot mit der Zahl der verletzten Grenzen. Die MPPT-Karten darunter zeigen, welcher Wert welche Grenze überschreitet und welche Anzahl zulässig ist.

Die Statusleiste unten zeigt immer das Gesamtergebnis.

AnWa-Solar speichert deine Auslegung automatisch im offenen Projekt.
