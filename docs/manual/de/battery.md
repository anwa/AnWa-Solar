# Batteriespeicher

AnWa-Solar prüft, ob eine Batteriebank zum Batterieeingang eines Hybrid-Wechselrichters passt. Es bemisst die Batterie nicht nach deinem Verbrauch; du wählst das Batteriemodul und wie viele Module du anschließt.

## Batterie planen

Die Karte **Batterie** in der Konfiguration erscheint, wenn der gewählte Wechselrichter in den Stammdaten einen Batterieeingang hat oder schon eine Batterie geplant ist. **Ändern** öffnet den Batteriedialog:

| Feld | Bedeutung |
|---|---|
| Batteriespeicher planen | Schaltet die Batterieprüfung ein oder aus. |
| Hersteller, Serie, Leistungsklasse | Das Batteriemodul aus den [Stammdaten](master-data.md#batterie). **Importieren…** liest ein Batteriedatenblatt ein, siehe [Datenblatt-Import](datasheet-import.md). |
| Module in Reihe | 1 bei Niedervolt-Batterien. Bei Hochvolt-Batterien die Module eines Turms. |
| Module parallel | Parallel geschaltete Module oder Türme. |

Der untere Teil des Dialogs zeigt die Datenblattwerte eines Moduls. **Speichern** übernimmt die Konfiguration; sie wird im offenen [Projekt](projects.md) gespeichert.

## Ergebnisse

Die Karte **Batteriespeicher** in den Ergebnissen zeigt:

| Zeile | Bedeutung |
|---|---|
| Bankspannung | Betriebsspannungsbereich und Nennspannung der Bank (Module in Reihe × Modulwerte) gegen den Batteriespannungsbereich des Wechselrichters. |
| Module in Reihe, Module parallel | Die Anzahlen gegen das Maximum aus dem Batteriedatenblatt. |
| Ladestrom, Entladestrom | Der niedrigere Wert aus Bankstrom (Module parallel × Modulstrom) und Wechselrichterstrom, mit der Leistung bei Nennspannung der Bank. |
| Energie | Nennenergie aller Module und, wenn das Datenblatt sie nennt, die nutzbare Energie. |

Prüfungen und ihr Status:

| Prüfung | Status |
|---|---|
| Der Wechselrichter hat in den Stammdaten keinen Batterieeingang | Fehler |
| Die Nennspannung der Bank liegt außerhalb des Batteriespannungsbereichs des Wechselrichters | Fehler |
| Die höchste Bankspannung überschreitet die max. Batteriespannung des Wechselrichters | Fehler |
| Mehr Module in Reihe oder parallel, als das Batteriedatenblatt erlaubt | Fehler |
| Die niedrigste Bankspannung liegt unter der min. Batteriespannung des Wechselrichters: Der Wechselrichter beendet das Entladen früher, ein Teil der Kapazität bleibt ungenutzt | Warnung |
| Mehrere Module in Reihe oder parallel, aber das Batteriedatenblatt nennt kein Maximum | Warnung |
| Die Bank erlaubt einen niedrigeren Lade- oder Entladestrom als der Wechselrichter | Hinweis |

Die nutzbare Energie ist der Wert aus dem Datenblatt. Viele Datenblätter nennen stattdessen eine Entladetiefe (DoD); dann ist die nutzbare Energie die Nennenergie mal der Entladetiefe.

Das Batteriemanagement hält die Batteriespannung in ihrem Bereich, deshalb gilt der Sicherheitsabschlag der PV-Berechnung nicht für die Batterie. Die Prüfung verwendet nur die Datenblattwerte; die Kompatibilitätslisten der Hersteller (Kommunikation zwischen Wechselrichter und Batteriemanagement) gehören nicht dazu.

Die Batteriekabel, die Sammelschiene und die Sicherungen werden in der Karte **Kabel und Sicherungen** bemessen; gib ihre Längen in den [Kabeleinstellungen](string-calculation.md#kabel) ein.

Der [Bericht](report.md) enthält den Abschnitt **Batteriespeicher** mit denselben Werten, den Formeln und den Hinweisen.
