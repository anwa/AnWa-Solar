# Datenblatt-Import

AnWa-Solar kann die Werte eines PV-Moduls, eines Wechselrichters oder einer Batterie aus ihrem PDF-Datenblatt lesen. Die meisten Datenblätter beschreiben eine ganze Serie mit mehreren Leistungsklassen; der Import speichert die Serie mit allen Leistungsklassen, die du behältst. Du prüfst und ergänzt die Werte in einem Dialog; **Übernehmen** speichert sie zusammen mit einer Kopie des Datenblatts in deinen eigenen Stammdaten. Die Leistungsklassen lassen sich danach wie die mitgelieferten wählen.

## Ein Datenblatt importieren

1. Klicke auf **Datenblatt importieren** in der Titelleiste des Hauptfensters oder auf **Importieren…** im Modul-, Wechselrichter- oder Batteriedialog.
2. Wähle die PDF-Datei des Datenblatts.
3. Prüfe den Dialog:
   - **PV-Modul**, **Wechselrichter** oder **Batterie**: AnWa-Solar erkennt die Art des Datenblatts; stelle sie um, wenn sie falsch ist.
   - **Modell oder Leistungsklasse im Datenblatt**: Viele Datenblätter beschreiben mehrere Leistungsklassen oder Modelle in Spalten. Wähle eine Leistungsklasse, um ihre Werte zu sehen und zu korrigieren; deine Änderungen bleiben erhalten, wenn du zu einer anderen wechselst.
   - **Serie**: der Name der Serie, vorgeschlagen aus den Modellnamen (zum Beispiel `SUN-SG05LP3-EU-SM2` für SUN-14K … SUN-20K-SG05LP3-EU-SM2) oder aus dem Dateinamen. Alle gespeicherten Leistungsklassen bilden diese Serie.
   - **Diese Leistungsklasse speichern**: Nimm den Haken bei Leistungsklassen heraus, die du nicht behalten willst; die Zeile darunter zählt die gespeicherten. Der Hersteller gilt für die ganze Serie.
   - Jeder Wert hat rechts eine Markierung:

     | Markierung | Bedeutung |
     |---|---|
     | Grüner Haken | Aus dem Datenblatt gelesen. Der Tooltip zeigt die Zeile des Datenblatts, aus der der Wert stammt. |
     | Orange Warnung | Nicht gefunden. Trage den Wert aus dem Datenblatt ein. |
     | Graue Info | Von dir eingegeben oder geändert, oder ein optionaler Wert, der nicht gefunden wurde. |

4. Klicke auf **Datenblatt öffnen**, um das PDF neben dem Dialog anzuzeigen und jeden Wert zu vergleichen.
5. Klicke auf **Übernehmen**. Fehlt ein Wert einer gespeicherten Leistungsklasse oder passt er nicht, listet der Dialog mit der Leistungsklasse, was zu korrigieren ist.

> Die Werte werden automatisch aus dem Text des PDFs gelesen. Datenblätter sind unterschiedlich aufgebaut, deshalb kann ein Wert fehlen oder selten auch falsch sein. Die Stringprüfung verlässt sich auf diese Werte: Prüfe jeden, besonders Voc, den Temperaturkoeffizienten von Voc und die maximale DC-Eingangsspannung.

## Prüfungen

Zusätzlich zu den [Prüfungen beim Laden](master-data.md#prüfungen-beim-laden) verlangt der Import:

- Die Temperaturkoeffizienten von Pmax und Voc sind negativ (ein verlorenes Minuszeichen würde die Spannung bei Kälte zu niedrig machen),
- die minimale Betriebstemperatur liegt unter der maximalen,
- der MPPT-Spannungsbereich liegt unter der maximalen DC-Eingangsspannung.

Zahlen dürfen einen Dezimalpunkt oder ein Dezimalkomma haben.

## Werte, die Aufmerksamkeit brauchen

- **Max. DC-Eingangsleistung**: Manche Wechselrichter-Datenblätter nennen zusätzlich eine höhere „max. PV-Anschlussleistung“. AnWa-Solar verwendet die DC-Eingangsleistung des Wechselrichters.
- **Ströme pro MPPT**: Datenblätter schreiben oft „36+36“ für zwei MPPTs. Trage den Wert eines MPPT ein, also 36.
- **MPPT-Anzahl und Strings**: „2/2+2“ bedeutet 2 MPPTs mit je bis zu 2 Strings.
- **AC-Ströme**: Datenblätter nennen oft zwei Werte wie „27.3/26.1“ für 220/380 V und 230/400 V. AnWa-Solar nimmt den zweiten Wert, für 230/400 V.
- **Modulanschlusskabel**: „Photovoltaikkabel: 4,0 mm²“ und „Hochformat: 1100/1100 mm“ ergeben den Querschnitt und die Plus- und Minuslänge; die Längen im Hochformat werden genommen, nicht die im Querformat. Sie sind optional.
- **Max. Absicherung** (zum Beispiel „Maximale Absicherung 20 A“) ist optional; ohne sie gibt die Prüfung der Strangsicherungen nur einen Hinweis.
- **Batteriewerte eines Hybrid-Wechselrichters** (Batteriespannungsbereich, max. Lade- und Entladestrom, Batterieeingänge) sind optional; lasse sie bei Wechselrichtern ohne Batterieeingang leer.
- **Batterieströme**: Trage den Dauer-Lade- und -Entladestrom eines Moduls ein, nicht den Spitzenwert („Spitzenwert 280 A (10 Sek.)“).
- **Batterie-Modellnamen** sind in manchen Datenblättern Grafiken. Die Spalten heißen dann „Spalte 1 · 12.03 kWh“; trage den Modellnamen selbst ein.
- **Nutzbare Energie und Entladetiefe**: Die meisten Batteriedatenblätter nennen eine Entladetiefe statt einer nutzbaren Energie. Trage ein, was das Datenblatt nennt.
- **AC-Phasen** werden aus dem Netzanschluss erkannt („dreiphasig“, „3L+N+PE“); prüfe sie.
- **Temperaturkoeffizienten in mV/°C oder mA/°C** werden nicht umgerechnet; trage den Wert in %/°C aus dem Datenblatt ein oder berechne ihn aus den Datenblattwerten.
- **Gescannte Datenblätter** enthalten keinen Text. Der Dialog öffnet sich dann leer, und du trägst alle Werte von Hand ein; das Datenblatt wird mit ihnen gespeichert.

## Eigene Stammdaten

Importierte Einträge werden je Windows-Benutzer in `%LOCALAPPDATA%\AnWa-Solar\Data` gespeichert:

| Datei | Inhalt |
|---|---|
| `Modules\<Hersteller>\<Serie>.json` (und `Inverters`, `Batteries`) | Deine Serien, im selben Format wie die mitgelieferten [Stammdaten](master-data.md) |
| `…\<Serie>.pdf` | Das importierte Datenblatt, neben seiner Seriendatei |

Eine eigene Serie mit gleichem Hersteller und Namen wie eine mitgelieferte ersetzt diese in der Auswahl; die mitgelieferten Dateien werden nicht geändert. Ein erneuter Import der Serie ersetzt deine Serie und ihr Datenblatt.

## Ein Datenblatt anzeigen

Einträge mit importiertem Datenblatt zeigen einen PDF-Knopf:

- in der Modul- und Wechselrichterauswahl (**Datenblatt**),
- im Hauptfenster neben dem Wechselrichter und neben jedem gewählten Modul.

Das Datenblatt öffnet sich in einem eigenen Fenster mit Zoom, Suche und Druck.
