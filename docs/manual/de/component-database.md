# Komponentendatenbank

Die Komponentendatenbank zeigt alle PV-Module, Wechselrichter und Batterien der [Stammdaten](master-data.md) und lässt dich sie anlegen und ändern. Du öffnest sie mit dem Datenbank-Knopf in der Titelleiste des Hauptfensters.

## Das Fenster

Links listet ein Baum die Arten (**PV-Module**, **Wechselrichter**, **Batterien**), darunter die Hersteller und ihre Serien. Die Zahl in Klammern ist die Anzahl der Leistungsklassen; **eigene** markiert deine eigenen Serien.

Rechts zeigt der Editor die gewählte Serie:

- **Hersteller** und **Serie** der Serie.
- Eine Zeile, die sagt, ob es eine mitgelieferte oder eigene Serie ist und welche Datenblätter angehängt sind (Deutsch, Englisch).
- Eine Tabelle mit einer Zeile pro Wert und einer Spalte pro Leistungsklasse, so wie Datenblätter aufgebaut sind. Fette Zeilen sind Pflichtwerte; der Tooltip einer Zeile erklärt sie.

| Knopf | Funktion |
|---|---|
| Pfeil am Ende einer Zeile | Übernimmt den Wert der ersten Leistungsklasse in alle Leistungsklassen, für Werte, die in der ganzen Serie gleich sind. |
| Kopieren über einer Spalte | Fügt eine Kopie dieser Leistungsklasse hinzu; trage ihr Modell ein. |
| Löschen über einer Spalte | Entfernt diese Leistungsklasse. |
| Leistungsklasse hinzufügen | Fügt eine Leistungsklasse mit den Werten der letzten hinzu. |
| PDF anhängen… | Hängt ein **deutsches** oder **englisches** Datenblatt an die Serie; es wird beim Speichern neben der Seriendatei abgelegt und ersetzt das Datenblatt dieser Sprache. Siehe [Datenblätter in zwei Sprachen](datasheet-import.md#datenblätter-in-zwei-sprachen). |
| Datenblatt | Zeigt das angehängte Datenblatt; bei mehreren bietet ein Menü sie nach Sprache an. |

## Anlegen, ändern und löschen

- **Neue Serie** beginnt eine leere Serie der links gewählten Art. Trage Hersteller, Serie und die Werte jeder Leistungsklasse ein.
- **Importieren…** liest ein PDF-Datenblatt als neue Serie ein, siehe [Datenblatt-Import](datasheet-import.md).
- **Speichern** prüft jede Leistungsklasse nach denselben Regeln wie der Import und speichert die Serie in deinen eigenen Stammdaten. Die Liste der Probleme nennt die Leistungsklasse.
- Eine **mitgelieferte Serie** lässt sich im Installationsordner nicht ändern: Speichern legt eine eigene Kopie an, die sie in der Auswahl ersetzt.
- Benennst du eine Serie oder ihren Hersteller um, wandert deine Serie mit ihren Datenblättern zum neuen Namen.
- **Serie löschen** löscht eine eigene Serie mit ihren Datenblättern nach einer Rückfrage. Hatte sie eine mitgelieferte Serie ersetzt, erscheint diese wieder.
- **Änderungen verwerfen** kehrt zu den gespeicherten Werten zurück. Bevor du mit ungespeicherten Änderungen zu einer anderen Serie wechselst oder das Fenster schließt, fragt AnWa-Solar nach.

Nach dem Schließen des Fensters verwendet das Hauptfenster die geänderten Werte; Wechselrichter, Module und Batterie des offenen Projekts werden neu zugeordnet.
