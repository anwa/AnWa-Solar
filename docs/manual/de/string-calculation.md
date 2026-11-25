# Stringberechnung

Die Registerkarte **Stringberechnung** hat zwei Spalten: links die **Konfiguration**, rechts die **Ergebnisse**. Jede Änderung wird sofort berechnet.

## Wechselrichter

**Wählen** (später **Ändern**) in der Karte **Wechselrichter** öffnet den Auswahldialog. Wähle Hersteller, Typ (zum Beispiel „Dreiphasiger Hybrid-Wechselrichter“), Serie und Leistungsklasse; der Dialog zeigt die Datenblattwerte (MPPT-Spannungsbereich, max. DC-Eingangsspannung, Startspannung, Ströme, MPPT-Anzahl und max. Strings pro MPPT). **Übernehmen** übernimmt den Wechselrichter. **Importieren…** liest ein PDF-Datenblatt in deine eigenen Stammdaten ein, **Datenblatt** zeigt das gespeicherte Datenblatt des gewählten Eintrags, siehe [Datenblatt-Import](datasheet-import.md).

Nach der Auswahl zeigt die Karte die Werte des Wechselrichters, und unter **Strings** erscheint eine Karte pro MPPT-Eingang.

Ein Wechsel des Wechselrichters ändert die anderen Komponenten nicht: Jeder MPPT behält sein Modul, seine Anzahlen und ob er eingeschaltet ist, und Kabel, Batterie und Parameter bleiben, wie sie sind. Werte, die der neue Wechselrichter nicht erlaubt, zum Beispiel mehr parallele Strings als er annimmt, bleiben stehen und werden als Verletzung angezeigt. Hat der neue Wechselrichter weniger MPPTs, bleiben die Strings der übrigen MPPTs erhalten, solange AnWa-Solar offen ist, und kommen zurück, wenn du wieder einen Wechselrichter mit mehr MPPTs wählst.

## Strings

Jeder MPPT hat eine eigene Karte:

- **Schalter** rechts neben dem MPPT-Titel: schaltet den MPPT aus. Ein ausgeschalteter MPPT behält Modul und Anzahlen, wird aber von Berechnung und Bericht übersprungen. Seine Karte ist abgeblendet und gesperrt, bis du ihn wieder einschaltest.
- **Modul wählen** / **Ändern**: öffnet die Modulauswahl (Hersteller, Serie, Leistungsklasse) mit den Datenblattwerten des Moduls. Die Karte zeigt dann das Modul mit Pmax, Voc, Vmpp und Impp.
- **Datenblatt** (PDF-Symbol): zeigt das importierte Datenblatt des Moduls; nur sichtbar, wenn es eines hat. Die Wechselrichterkarte hat denselben Knopf.
- **Entfernen** (Papierkorb): entfernt das Modul von diesem MPPT; nur sichtbar, wenn ein Modul gewählt ist.
- **Module pro String** (erster Schieberegler): reicht von 1 bis zur höchsten zulässigen Anzahl + 5. Rechts steht der aktuelle Wert; der markierte Bereich und die Zeile unter dem Regler zeigen den zulässigen Bereich dieses Moduls an diesem Wechselrichter, siehe [So wird gerechnet](calculation.md#zulässige-module-pro-string).
- **Parallele Strings** (zweiter Schieberegler): reicht von 1 bis zu den max. Strings pro MPPT des Wechselrichters.

## Ergebnisse

Das Band oben zeigt das Gesamtergebnis:

| Band | Bedeutung |
|---|---|
| Grün, „Alle Grenzen eingehalten“ | Auf keinem eingeschalteten MPPT ist eine Grenze verletzt. |
| Rot, „N Grenzen verletzt“ | Mindestens eine Grenze ist verletzt; die MPPT-Karten zeigen, welche. |
| Orange, „MPPT N hat kein PV-Modul“ | Ein eingeschalteter MPPT hat noch kein Modul. |
| Grau | Kein Wechselrichter gewählt, oder alle MPPTs sind ausgeschaltet. |

Darunter hat jeder MPPT eine Ergebniskarte mit einem Status (**OK**, **N Probleme**, **Kein Modul** oder **Ausgeschaltet**):

- **Spannungsfenster**: ein Balken ab 0 V. Das blaue Band ist der MPPT-Bereich, der grüne Balken die String-Vmpp über den Temperaturbereich, das Dreieck die höchste String-Voc und die senkrechte Linie die max. DC-Spannung, alles mit Sicherheitsabschlag. Ein Teil, der eine Grenze verletzt, wird rot.
- **Tabelle**: String-Voc und -Vmpp, Isc und Impp aller Strings, die DC-Leistung bei Kälte und die parallelen Strings, jeweils mit niedrigstem und höchstem Wert über den Temperaturbereich und der Grenze mit Sicherheitsabschlag. Ein verletzter Wert ist rot und mit einem roten Symbol markiert.
- **Zulässige Module pro String**: der Bereich, der alle Spannungsgrenzen erfüllt.
- **Hinweise**: jede verletzte Grenze mit Wert, Grenze und Empfehlung, zum Beispiel „Max. DC-Spannung überschritten: N×Voc(Tmin)=791,6 V > 760,0 V. Max. Module pro String: 15.“ Ein Hinweis sagt auch, wenn der Pmax-Koeffizient einen fehlenden Vmpp-Koeffizienten ersetzt.

Die Karte **AC-Seite und Netzanschluss** prüft den Wechselrichter gegen das Netz:

| Zeile | Bedeutung |
|---|---|
| PV-Spitzenleistung (STC) | Summe der Modulleistung aller eingeschalteten MPPTs mit Modul. |
| AC-Nennleistung | Aus dem Datenblatt des Wechselrichters. |
| DC/AC-Verhältnis | PV-Spitzenleistung geteilt durch die AC-Nennleistung. Unter dem Bereich ist der Wechselrichter für die PV-Leistung groß; darüber begrenzt er an sonnigen Tagen die Leistung (Clipping). Beides sind Hinweise, keine Fehler. |
| Scheinleistung | Max. AC-Scheinleistung aus dem Datenblatt (sonst die AC-Nennleistung), verglichen mit der Grenze der vereinfachten Netzanmeldung. Darüber frage deinen Netzbetreiber nach dem Verfahren. |
| Schieflast (einphasig) | Nur bei einphasigen Wechselrichtern: Über der Schieflastgrenze darf der Wechselrichter nicht einphasig angeschlossen werden; das zählt als verletzte Grenze. |
| AC-Strom pro Phase | Nenn- und max. AC-Strom aus dem Datenblatt; Grundlage für die AC-Leitung und ihren Schutz. |
| Einspeisebegrenzung | Nur bei einer Einspeisebegrenzung unter 100 %: die höchste ins Netz eingespeiste Leistung. |

AC-Werte, die in den Stammdaten des Wechselrichters fehlen, erscheinen als „nicht angegeben“ mit einem Hinweis; sie werden nicht geschätzt. Importiere das Datenblatt des Wechselrichters, um sie zu ergänzen, siehe [Datenblatt-Import](datasheet-import.md).

Die Karte **Kabel und Sicherungen** bemisst die Strangkabel jedes MPPT, die AC-Leitung und, wenn eine [Batterie](battery.md) geplant ist, die Batteriekabel:

| Zeile | Bedeutung |
|---|---|
| MPPT n Strangkabel | Das Kabel vom String zum Wechselrichter. Automatisch: der kleinste Solarkabel-Querschnitt, der den Bemessungsstrom (1,25 × Isc oder den Nennstrom der Strangsicherung) mit Temperatur- und Häufungsfaktoren trägt und den Spannungsfall im MPP einschließlich Modulanschlusskabel unter der Grenze hält. Ein gewählter Querschnitt wird geprüft: zu geringe Strombelastbarkeit ist ein Fehler, ein Spannungsfall über der Grenze eine Warnung. Jeder String hat ein eigenes Kabelpaar. |
| MPPT n Modulanschlusskabel | Die Anschlusskabel der Module eines Strings (Module × Plus- und Minuskabel) und ihr Spannungsfall. Längen und Querschnitt kommen aus dem Moduldatenblatt, sonst aus deiner Vorgabe. |
| MPPT n Strangsicherungen | Nötig, wenn (parallele Strings − 1) × 1,25 × Isc die max. Absicherung des Moduls überschreitet (IEC 62548); der Nennstrom liegt zwischen 1,5 × Isc und 2,4 × Isc und nicht über der Modul-Absicherung. |
| AC-Leitungsschutzschalter | Nächster Normnennstrom am oder über dem max. AC-Strom des Wechselrichters. |
| AC-Leitung | Kleinster Querschnitt, dessen Strombelastbarkeit (Verlegeart, Temperatur, Häufung) mindestens dem Nennstrom des LS-Schalters entspricht und dessen Spannungsfall unter der Grenze bleibt. |
| Batteriekabel | Ein Kabel pro Batterie, zur Sammelschiene oder ohne Sammelschiene zum Wechselrichter. Es trägt den Strom einer Batterie (ihren max. Lade- oder Entladestrom, höchstens den des Wechselrichters): Die Sicherung ist der nächste Nennstrom am oder darüber, das Kabel trägt den Nennstrom der Sicherung (Ib ≤ In ≤ Iz). |
| Hauptkabel Sammelschiene zum Wechselrichter | Nur mit Sammelschiene: trägt den Batteriestrom von Bank und Wechselrichter; eigene Sicherung. |
| Spannungsfall Batterie | Bezogen auf die Nennspannung der Bank, beim Batteriestrom des Wechselrichters, verteilt auf die Batteriekabel. Mit Sammelschiene hält ein automatischer Querschnitt jedes der beiden Kabel unter der halben Grenze. |

Die Werte stammen aus den Normtabellen in `Data\CableTables.json` im Installationsordner, mit ihren Quellen. Die Werte sind geprüft (`"Confirmed": true`), auch die der Batteriekabel (Abschnitt `BatteryCable`: Strombelastbarkeit einadriger Kabel von 25 bis 150 mm², Sicherungsnennströme, Spannungsfall-Grenze 1 %, mit eigenem `"Confirmed"`). Ein Abschnitt mit `"Confirmed": false`, zum Beispiel nach einer Wertänderung, lässt die Karte einen Hinweis zeigen. Ein Kabel wird nur bemessen, wenn seine Länge eingegeben ist. Das Ergebnis ist eine Planungshilfe; für die Wahl von Kabeln und Schutzorganen bleibt der Errichter verantwortlich.

Die Karte **Grenzwerte des Wechselrichters** listet jede Grenze aus dem Datenblatt und mit Sicherheitsabschlag. **Ergebnisse kopieren** über den Ergebnissen kopiert sie als Text.

Die Bedeutung jeder Prüfung beschreibt [So wird gerechnet](calculation.md#prüfungen).

## Kabel

**Ändern** in der Karte **Kabel** öffnet die Kabeleinstellungen:

| Feld | Bedeutung |
|---|---|
| Verlegeart (DC) | Einzelnes Solarkabel frei in Luft, auf einer Oberfläche oder zwei sich berührende Kabel auf einer Oberfläche (EN 50618). |
| Kabellänge MPPT n | Einfache Länge der Strangkabel dieses MPPT; 0 bedeutet nicht eingegeben. |
| Umgebungstemperatur (DC) | Bezug 60 °C; darüber sinkt die Strombelastbarkeit. |
| Gemeinsam verlegte Stromkreise | Häufungsfaktor, 1 bis 9 Stromkreise. |
| Querschnitt der Strangkabel | Automatisch oder ein gewählter Querschnitt, zum Beispiel 4 oder 6 mm². |
| Max. Spannungsfall DC | Der Stringspannung im MPP, Modulanschlusskabel eingeschlossen; Vorgabe 1 %. |
| Länge je Kabelende, Querschnitt der Anschlusskabel | Für Module, deren Datenblatt keine Anschlusskabel nennt; Vorgabe 0,5 m je Kabelende und 4 mm². |
| Verlegeart (AC) | A1, B1, B2, C oder E (IEC 60364-5-52 / DIN VDE 0298-4). |
| Länge der AC-Leitung | Einfache Länge vom Wechselrichter zum Verteiler. |
| Umgebungstemperatur (AC) | Bezug 30 °C. |
| Max. Spannungsfall AC | Von 400 V (dreiphasig) oder 230 V (einphasig); Vorgabe 3 %. |
| Batterien an einer Sammelschiene | Ein: Jede Batterie hat ein Kabel zur Sammelschiene, und ein Hauptkabel führt zum Wechselrichter. Aus: Jede Batterie hat ein eigenes Kabel zum Wechselrichter. |
| Länge jedes Batteriekabels, Querschnitt | Einfache Länge; automatisch oder gewählt (25 bis 150 mm²). |
| Sammelschiene zum Wechselrichter, Querschnitt | Mit Sammelschiene: Länge und Querschnitt des Hauptkabels. |
| Umgebungstemperatur (Batterie) | Bezug 30 °C. |
| Max. Spannungsfall (Batterie) | Der Nennspannung der Bank; Vorgabe 1 %. |

## Parameter

**Ändern** in der Karte **Parameter** öffnet den Dialog mit:

| Feld | Bedeutung |
|---|---|
| Min. Zelltemperatur (°C) | Niedrigste Modultemperatur. Bei Sonnenaufgang im Winter liegt sie nahe der niedrigsten Umgebungstemperatur, zum Beispiel −20 °C. |
| Max. Zelltemperatur (°C) | Höchste Modultemperatur. Im Sommer erreichen Zellen 60–70 °C; Vorgabe 70 °C. |
| Sicherheitsabschlag (%) | Verschärft alle Grenzen des Wechselrichters um diesen Prozentsatz, 0–50 %. |
| Schieflastgrenze (kVA) | Höchste Scheinleistung eines einphasigen Wechselrichters; Vorgabe 4,6 kVA (VDE-AR-N 4105). |
| Vereinfachte Anmeldung bis (kVA) | Anlagengröße der vereinfachten Netzanmeldung; Vorgabe 30 kVA. |
| Einspeisebegrenzung (%) | Höchste Einspeisung in Prozent der PV-Spitzenleistung; 100 % (Vorgabe) bedeutet keine Begrenzung. |
| DC/AC-Verhältnis von / bis | Empfohlener Bereich des DC/AC-Verhältnisses; Vorgabe 0,9 bis 1,3. |

Die Netzgrenzen hängen von deinem Netzbetreiber und den aktuellen Regeln ab; prüfe die Vorgaben, bevor du dich darauf verlässt. Tmin muss unter Tmax liegen. **Speichern** speichert die Werte im offenen Projekt; sie gelten sofort. Neue Projekte starten mit den Standardwerten aus den [Einstellungen](settings.md#einstellungsseite).
