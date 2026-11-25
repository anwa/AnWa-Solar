# Stammdaten

AnWa-Solar ordnet die PV-Module, Wechselrichter und Batterien in **Serien**, so wie ein Datenblatt sie beschreibt: eine Serie eines Herstellers mit ihren Leistungsklassen. Jede Serie ist eine JSON-Datei im Ordner `Data` der Installation (`C:\Program Files\AnWa-Solar\Data`):

```text
Data\Modules\<Hersteller>\<Serie>.json
Data\Inverters\<Hersteller>\<Serie>.json
Data\Batteries\<Hersteller>\<Serie>.json
```

Eine Seriendatei nennt Hersteller und Serie und listet die Leistungsklassen unter `Variants`; jede Leistungsklasse enthält ihre vollständigen Werte, wie in den Beispielen unten (ohne den Hersteller). Die Feldnamen in den Dateien sind englisch, unabhängig von der Sprache der Anwendung:

```json
{
  "Manufacturer": "Deye",
  "Series": "SUN-SG05LP3-EU-SM2",
  "Datasheet file": "SUN-SG05LP3-EU-SM2.pdf",
  "Variants": [
    { "Model": "SUN-15K-SG05LP3-EU-SM2", "Max. DC input power (W)": 24000, "...": "..." },
    { "Model": "SUN-18K-SG05LP3-EU-SM2", "Max. DC input power (W)": 28800, "...": "..." }
  ]
}
```

Deine eigenen Serien, zum Beispiel aus einem [Datenblatt-Import](datasheet-import.md), liegen in `%LOCALAPPDATA%\AnWa-Solar\Data` in derselben Struktur, mit dem Datenblatt neben der Seriendatei. Eine eigene Serie ersetzt die mitgelieferte Serie mit gleichem Hersteller und Namen, und eine eigene Leistungsklasse die mitgelieferte mit gleichem Hersteller und Modell. Ein Modell gehört zu genau einer Serie: Das Speichern einer Serie nimmt ihre Modelle aus deinen anderen Serien heraus.

Die [Komponentendatenbank](component-database.md) zeigt alle Serien und lässt dich sie anlegen und ändern, ohne JSON-Dateien zu bearbeiten. Die Auswahldialoge folgen dieser Struktur: Hersteller, bei Wechselrichtern der Typ (zum Beispiel „Dreiphasiger Hybrid-Wechselrichter“, abgeleitet aus den AC-Phasen und dem Batterieeingang), Serie und Leistungsklasse.

Versionen vor 0.20.0 speicherten deine eigenen Einträge in `Modules.json`, `Inverters.json` und `Batteries.json` mit den Datenblättern in `Datasheets`; sie werden beim ersten Start in Seriendateien umgewandelt (eine Serie pro Eintrag), und die alten Dateien bleiben als `*.migrated` erhalten.

Trage nur Werte aus den Datenblättern der Hersteller ein. Zahlen in den Dateien verwenden den Punkt als Dezimaltrennzeichen und dürfen Dezimalzahlen sein, zum Beispiel `12.5`.

## PV-Modul

```json
{
  "Manufacturer": "AIKO",
  "Model": "AIKO-A500-MAH60Db",
  "Pmax (W)": 500,
  "Vmpp (V)": 37.9,
  "Impp (A)": 13.21,
  "Voc (V)": 45.02,
  "Isc (A)": 14.05,
  "Module efficiency (%)": 22.6,
  "Temperature coefficient of Pmax (%/°C)": -0.26,
  "Temperature coefficient of Voc (%/°C)": -0.22,
  "Temperature coefficient of Isc (%/°C)": 0.05,
  "Operating temperature min (°C)": -40,
  "Operating temperature max (°C)": 85
}
```

`"Cable cross-section (mm2)"`, `"Cable length plus (mm)"` und `"Cable length minus (mm)"` beschreiben die Modulanschlusskabel (Hochformat) und sind optional; ohne sie gilt deine Vorgabe aus den Kabeleinstellungen. `"Max. series fuse rating (A)"` ist optional; der Wert entscheidet, ob Strangsicherungen nötig sind, siehe [Stringberechnung](string-calculation.md#ergebnisse). `"Temperature coefficient of Vmpp (%/°C)"` ist optional. Ohne ihn wird der Pmax-Koeffizient verwendet, siehe [So wird gerechnet](calculation.md#temperaturkorrektur).

## Wechselrichter

```json
{
  "Manufacturer": "Deye",
  "Model": "SUN-18K-SG05LP3-EU-SM2",
  "Max. DC input power (W)": 28800,
  "Max. DC input voltage (V)": 800,
  "Start voltage (V)": 160,
  "MPPT voltage range (V)": "160-650",
  "Nominal DC input voltage (V)": 550,
  "Max. input current per MPPT (A)": 36,
  "Max. short-circuit current per MPPT (A)": 54,
  "MPPT count": 2,
  "Max. strings per MPPT": 2,
  "Rated AC output power (W)": 18000,
  "Max. AC apparent power (VA)": 19800,
  "Rated AC output current (A)": 26.1,
  "Max. AC output current (A)": 28.7,
  "AC phases": 3,
  "Battery voltage range (V)": "40-60",
  "Max. battery charge current (A)": 330,
  "Max. battery discharge current (A)": 330,
  "Battery inputs": 2
}
```

Die AC-Werte sind optional; ohne sie zeigt die AC-Seite „nicht angegeben“. AC-Ströme gelten pro Phase bei 230/400 V.

Die Batteriewerte gibt es nur bei Hybrid-Wechselrichtern. Mit einem Batteriespannungsbereich werden der max. Lade- und Entladestrom gebraucht; ohne ihn hat der Wechselrichter keinen Batterieeingang.

Der MPPT-Spannungsbereich ist Text in der Form `"min-max"`. Ströme gelten pro MPPT-Eingang.

## Batterie

Ein Batteriemodul, zum Beispiel die Deye SE-F16:

```json
{
  "Manufacturer": "Deye",
  "Model": "SE-F16",
  "Nominal voltage (V)": 51.2,
  "Operating voltage range (V)": "44.8-57.6",
  "Nominal capacity (Ah)": 314,
  "Nominal energy (kWh)": 16,
  "Depth of discharge (%)": 90,
  "Max. charge current (A)": 160,
  "Max. discharge current (A)": 230,
  "Max. modules in parallel": 32
}
```

`"Nominal capacity (Ah)"`, `"Usable energy (kWh)"`, `"Depth of discharge (%)"`, `"Max. modules in series"` und `"Max. modules in parallel"` sind optional. Die Ströme sind die Dauerwerte, nicht die Spitzenwerte. Die Nennspannung muss im Betriebsspannungsbereich liegen. Siehe [Batteriespeicher](battery.md).

## Prüfungen beim Laden

Jeder Eintrag wird einzeln geprüft. Ein Eintrag wird übersprungen, wenn

- ein Wert den falschen Typ hat, zum Beispiel `"36 A"` statt `36`,
- Hersteller oder Modell fehlt,
- ein Pflichtwert 0 oder negativ ist,
- Voc nicht über Vmpp liegt oder Isc kleiner als Impp ist,
- der MPPT-Spannungsbereich nicht die Form `"160-650"` hat,
- Hersteller und Modell doppelt vorkommen.

Eine Seriendatei wird nur dann ganz übersprungen, wenn sie nicht lesbar ist oder Hersteller, Serie oder `Variants` fehlen; sonst wird jede Leistungsklasse einzeln geprüft.

Übersprungene Einträge und nicht lesbare Dateien stehen in der Meldung **Stammdaten** nach dem Start und im Log; Probleme in deinen eigenen Dateien sind mit „(own data)“ markiert. Die übrigen Einträge werden normal geladen.

> Änderungen im Installationsordner brauchen Administratorrechte und werden beim nächsten Update ersetzt. Lege eigene Einträge stattdessen über den [Datenblatt-Import](datasheet-import.md) oder die [Komponentendatenbank](component-database.md) an; sie bleiben bei Updates erhalten.
