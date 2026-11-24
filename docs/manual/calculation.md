# How the Calculation Works

## Temperature correction

Datasheet values apply at 25 °C cell temperature (STC). AnWa-Solar corrects them linearly with the temperature coefficient α of the datasheet (in %/°C):

$$
x(T) = x(25\,^{\circ}\mathrm{C}) \cdot \left(1 + \frac{\alpha}{100} \cdot (T - 25\,^{\circ}\mathrm{C})\right)
$$

Tmin and Tmax are cell temperatures, see [Parameters](string-calculation.md#parameters).

- Voc uses the Voc coefficient. The open-circuit voltage is highest at the lowest temperature.
- Vmpp uses the Vmpp coefficient of the module. Most datasheets do not state it; then the Pmax coefficient is used, which changes Vmpp more strongly than the Voc coefficient and is therefore the conservative choice.
- Isc uses the Isc coefficient; Pmax uses the Pmax coefficient.
- Impp is derived as Pmax / Vmpp at the respective temperature.

**Example:** Voc = 45.02 V, α(Voc) = −0.22 %/°C, Tmin = −20 °C:

$$
V_{oc}(-20\,^{\circ}\mathrm{C}) = 45.02\,\mathrm{V} \cdot (1 + 0.0022 \cdot 45) = 49.48\,\mathrm{V}
$$

## Safety margin

The safety margin m tightens every inverter limit: maximum values are multiplied by (1 − m), minimum values by (1 + m). With 5 % a max. DC input voltage of 800 V becomes 760 V and a start voltage of 160 V becomes 168 V.

## Checks

N is the number of modules per string, S the number of parallel strings.

| Check | Value | Limit |
|---|---|---|
| Max. DC voltage | N × Voc(Tmin) | max. DC input voltage |
| MPPT upper bound | N × Vmpp(Tmin) | upper bound of the MPPT range |
| MPPT lower bound | N × Vmpp(Tmax) | lower bound of the MPPT range |
| Start voltage | N × Vmpp(Tmax) | start voltage |
| Short-circuit current | S × Isc (higher value of Tmin and Tmax) | max. short-circuit current per MPPT |
| Input current | S × Impp (higher value of Tmin and Tmax) | max. input current per MPPT |
| Strings per MPPT | S | max. strings per MPPT |
| DC power | N × S × Pmax(Tmin) | max. DC input power / MPPT count |

The check of the maximum DC voltage protects the inverter: an exceeded value can damage it. The MPPT and start voltage checks concern the yield; the current and power checks concern overload and clipping.

## Allowed modules per string

The allowed range of modules per string is the range in which all voltage checks pass:

- lowest count: the higher of ⌈MPPT lower bound / Vmpp(Tmax)⌉ and ⌈start voltage / Vmpp(Tmax)⌉,
- highest count: the lower of ⌊max. DC voltage / Voc(Tmin)⌋ and ⌊MPPT upper bound / Vmpp(Tmin)⌋.

When the lowest count is higher than the highest count, the module does not fit the inverter ("no valid range").
