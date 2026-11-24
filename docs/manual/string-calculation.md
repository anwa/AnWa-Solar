# String Calculation

The **String calculation** tab has two columns: the **Configuration** on the left and the **Results** on the right. Every change is calculated immediately.

## Inverter

**Select** (later **Change**) in the **Inverter** card opens the selection dialog. Choose the manufacturer and the model; the dialog shows the datasheet values (MPPT voltage range, max. DC input voltage, start voltage, currents, MPPT count and max. strings per MPPT). **Apply** takes over the inverter.

After the selection the card shows the inverter values, and one card per MPPT input appears below **Strings**.

## Strings

Each MPPT has its own card:

- **Switch** on the right of the MPPT title: disables the MPPT. A disabled MPPT keeps its module and counts but is skipped by the calculation and the report. Its card is dimmed and locked until you enable it again.
- **Select module** / **Change**: opens the module selection with the datasheet values of the module. The card then shows the module with Pmax, Voc, Vmpp and Impp.
- **Remove** (bin icon): removes the module from this MPPT; only visible when a module is selected.
- **Modules per string** (first slider): ranges from 1 to the highest allowed count + 5. The current value is shown on the right; the marked range and the line below the slider show the allowed range of this module on this inverter, see [How the Calculation Works](calculation.md#allowed-modules-per-string).
- **Parallel strings** (second slider): ranges from 1 to the max. strings per MPPT of the inverter.

## Results

The banner on top shows the overall result:

| Banner | Meaning |
|---|---|
| Green, "All limits are met" | No limit is violated on any enabled MPPT. |
| Red, "N limits violated" | At least one limit is violated; the MPPT cards show which. |
| Orange, "MPPT N has no PV module" | An enabled MPPT has no module yet. |
| Grey | No inverter is selected, or all MPPTs are disabled. |

Below it, every MPPT has a result card with a status (**OK**, **N issues**, **No module** or **Disabled**):

- **Voltage window**: a bar from 0 V. The blue band is the MPPT range, the green bar the string Vmpp over the temperature range, the triangle the highest string Voc and the vertical line the max. DC voltage, all with the safety margin. A part that violates a limit turns red.
- **Table**: string Voc and Vmpp, Isc and Impp of all strings, the DC power when cold and the parallel strings, each with its lowest and highest value over the temperature range and the limit with the safety margin. A violated value is red and marked with a red icon.
- **Allowed modules per string**: the range that meets all voltage limits.
- **Notes**: every violated limit with the value, the limit and a recommendation, for example "Max. DC voltage exceeded: N×Voc(Tmin)=791.6 V > 760.0 V. Max. modules per string: 15." A note also tells you when the Pmax coefficient replaces a missing Vmpp coefficient.

The **Inverter limits** card lists every limit from the datasheet and with the safety margin. **Copy** above the results copies them as text.

The meaning of every check is described in [How the Calculation Works](calculation.md#checks).

## Parameters

**Change** in the **Parameters** card opens the dialog with:

| Field | Meaning |
|---|---|
| Min. cell temperature (°C) | Lowest module temperature. At sunrise in winter it is close to the lowest ambient temperature, for example −20 °C. |
| Max. cell temperature (°C) | Highest module temperature. In summer cells reach 60–70 °C; the default is 70 °C. |
| Safety margin (%) | Tightens all inverter limits by this percentage, 0–50 %. |

Tmin must be lower than Tmax. **Save** stores the values; they apply immediately and at the next start.
