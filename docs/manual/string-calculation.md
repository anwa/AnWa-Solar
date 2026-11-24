# String Calculation

The **String calculation** tab has four sections. Every change is calculated immediately.

## Inverter

**Select inverter** (later **Change inverter**) opens the selection dialog. Choose the manufacturer and the model; the dialog shows the datasheet values (MPPT voltage range, max. DC input voltage, start voltage, currents, MPPT count and max. strings per MPPT). **Apply** takes over the inverter.

After the selection the summary line shows the inverter values, and the Strings section shows one part per MPPT input.

## Strings

Each MPPT has its own part:

- **Switch** on the right of the MPPT title: disables the string. A disabled string keeps its module and counts but is skipped by the calculation and the report. Its buttons and sliders are locked until you enable it again.
- **Select PV module** / **Change PV module**: opens the module selection with the datasheet values of the module.
- **Remove module**: removes the module from this MPPT; only visible when a module is selected.
- **Modules per string** (left slider): ranges from 1 to the highest allowed count + 5. The marked range is the allowed range of this module on this inverter, see [How the Calculation Works](calculation.md#allowed-modules-per-string).
- **Parallel strings** (right slider): ranges from 1 to the max. strings per MPPT of the inverter.

## Results

The results show for every MPPT:

- the module and the settings,
- the string voltages Voc and Vmpp and the string currents Isc and Impp at the minimum and maximum temperature,
- the estimated PV power when cold,
- the allowed modules per string and the allowed parallel strings.

Violated limits are listed under **Notes** with the value, the limit and a recommendation, for example "Max. DC voltage exceeded: N×Voc(Tmin)=791.6 V > 760.0 V. Max. modules per string: 15." The last lines summarize the result.

The meaning of every check is described in [How the Calculation Works](calculation.md#checks).

## Parameters

**Change parameters** opens the dialog with:

| Field | Meaning |
|---|---|
| Min. cell temperature (°C) | Lowest module temperature. At sunrise in winter it is close to the lowest ambient temperature, for example −20 °C. |
| Max. cell temperature (°C) | Highest module temperature. In summer cells reach 60–70 °C; the default is 70 °C. |
| Safety margin (%) | Tightens all inverter limits by this percentage, 0–50 %. |

Tmin must be lower than Tmax. **Save** stores the values; they apply immediately and at the next start.
