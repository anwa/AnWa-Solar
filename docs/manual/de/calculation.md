# So wird gerechnet

## Temperaturkorrektur

Datenblattwerte gelten bei 25 °C Zelltemperatur (STC). AnWa-Solar korrigiert sie linear mit dem Temperaturkoeffizienten α des Datenblatts (in %/°C):

$$
x(T) = x(25\,^{\circ}\mathrm{C}) \cdot \left(1 + \frac{\alpha}{100} \cdot (T - 25\,^{\circ}\mathrm{C})\right)
$$

Tmin und Tmax sind Zelltemperaturen, siehe [Parameter](string-calculation.md#parameter).

- Voc verwendet den Voc-Koeffizienten. Die Leerlaufspannung ist bei der niedrigsten Temperatur am höchsten.
- Vmpp verwendet den Vmpp-Koeffizienten des Moduls. Die meisten Datenblätter nennen ihn nicht; dann wird der Pmax-Koeffizient verwendet, der Vmpp stärker verändert als der Voc-Koeffizient und deshalb die vorsichtige Wahl ist.
- Isc verwendet den Isc-Koeffizienten, Pmax den Pmax-Koeffizienten.
- Impp ergibt sich als Pmax / Vmpp bei der jeweiligen Temperatur.

**Beispiel:** Voc = 45,02 V, α(Voc) = −0,22 %/°C, Tmin = −20 °C:

$$
V_{oc}(-20\,^{\circ}\mathrm{C}) = 45{,}02\,\mathrm{V} \cdot (1 + 0{,}0022 \cdot 45) = 49{,}48\,\mathrm{V}
$$

## Sicherheitsabschlag

Der Sicherheitsabschlag m verschärft jede Grenze des Wechselrichters: Höchstwerte werden mit (1 − m) multipliziert, Mindestwerte mit (1 + m). Mit 5 % wird aus einer max. DC-Eingangsspannung von 800 V ein Wert von 760 V und aus einer Startspannung von 160 V ein Wert von 168 V.

## Prüfungen

N ist die Anzahl der Module pro String, S die Anzahl paralleler Strings.

| Prüfung | Wert | Grenze |
|---|---|---|
| Max. DC-Spannung | N × Voc(Tmin) | max. DC-Eingangsspannung |
| MPPT-Obergrenze | N × Vmpp(Tmin) | Obergrenze des MPPT-Bereichs |
| MPPT-Untergrenze | N × Vmpp(Tmax) | Untergrenze des MPPT-Bereichs |
| Startspannung | N × Vmpp(Tmax) | Startspannung |
| Kurzschlussstrom | S × Isc (höherer Wert von Tmin und Tmax) | max. Kurzschlussstrom pro MPPT |
| Eingangsstrom | S × Impp (höherer Wert von Tmin und Tmax) | max. Eingangsstrom pro MPPT |
| Strings pro MPPT | S | max. Strings pro MPPT |
| DC-Leistung | N × S × Pmax(Tmin) | max. DC-Eingangsleistung / MPPT-Anzahl |

Die Prüfung der maximalen DC-Spannung schützt den Wechselrichter: Ein überschrittener Wert kann ihn beschädigen. Die Prüfungen von MPPT-Bereich und Startspannung betreffen den Ertrag; die Strom- und Leistungsprüfungen betreffen Überlast und Clipping.

## Zulässige Module pro String

Der zulässige Bereich der Module pro String ist der Bereich, in dem alle Spannungsprüfungen bestehen:

- kleinste Anzahl: der höhere Wert von ⌈MPPT-Untergrenze / Vmpp(Tmax)⌉ und ⌈Startspannung / Vmpp(Tmax)⌉,
- größte Anzahl: der niedrigere Wert von ⌊max. DC-Spannung / Voc(Tmin)⌋ und ⌊MPPT-Obergrenze / Vmpp(Tmin)⌋.

Ist die kleinste Anzahl größer als die größte, passt das Modul nicht zum Wechselrichter („kein gültiger Bereich“).
