# String Calculation

The **String calculation** tab has two columns: the **Configuration** on the left and the **Results** on the right. Every change is calculated immediately.

## Inverter

**Select** (later **Change**) in the **Inverter** card opens the selection dialog. Choose the manufacturer and the model; the dialog shows the datasheet values (MPPT voltage range, max. DC input voltage, start voltage, currents, MPPT count and max. strings per MPPT). **Apply** takes over the inverter. **Import…** reads a PDF datasheet into your own master data, **Datasheet** shows the stored datasheet of the selected entry, see [Datasheet Import](datasheet-import.md).

After the selection the card shows the inverter values, and one card per MPPT input appears below **Strings**.

Changing the inverter does not change the other components: each MPPT keeps its module, its counts and whether it is enabled, and the cables, the battery and the parameters stay as they are. Values that the new inverter does not allow, for example more parallel strings than it accepts, stay set and are shown as violations. When the new inverter has fewer MPPTs, the strings of the other MPPTs are kept while AnWa-Solar is open and come back when you select an inverter with more MPPTs again.

## Strings

Each MPPT has its own card:

- **Switch** on the right of the MPPT title: disables the MPPT. A disabled MPPT keeps its module and counts but is skipped by the calculation and the report. Its card is dimmed and locked until you enable it again.
- **Select module** / **Change**: opens the module selection with the datasheet values of the module. The card then shows the module with Pmax, Voc, Vmpp and Impp.
- **Datasheet** (PDF icon): shows the imported datasheet of the module; only visible when it has one. The inverter card has the same button.
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

The **AC side and grid connection** card checks the inverter against the grid:

| Row | Meaning |
|---|---|
| PV peak power (STC) | Sum of the module power of all enabled MPPTs with a module. |
| Rated AC power | From the datasheet of the inverter. |
| DC/AC ratio | PV peak power divided by the rated AC power. Below the range the inverter is large for the PV power; above it the inverter limits the power on sunny days (clipping). Both are notes, not errors. |
| Apparent power | Max. AC apparent power from the datasheet (otherwise the rated AC power), compared with the limit of the simplified grid registration. Above it, ask your grid operator for the procedure. |
| Unbalanced load (single-phase) | Only for single-phase inverters: above the unbalanced load limit the inverter may not be connected single-phase; this counts as a violated limit. |
| AC current per phase | Rated and max. AC current from the datasheet; the basis for the AC line and its protection. |
| Feed-in limit | Only with a feed-in limit below 100 %: the highest power fed into the grid. |

AC values that the master data of the inverter does not contain are shown as "not stated" with a note; they are not estimated. Import the datasheet of the inverter to add them, see [Datasheet Import](datasheet-import.md).

The **Cables and fuses** card sizes the string cables of each MPPT and the AC line:

| Row | Meaning |
|---|---|
| MPPT n string cables | Smallest solar cable cross-section that carries the design current (1.25 × Isc, or the string fuse rating) with the temperature and grouping factors and keeps the voltage drop at the MPP below the limit. Each string has its own cable pair. |
| MPPT n string fuses | Needed when (parallel strings − 1) × 1.25 × Isc exceeds the max. series fuse rating of the module (IEC 62548); the fuse rating lies between 1.5 × Isc and 2.4 × Isc and not above the module rating. |
| AC circuit breaker | Next standard rating at or above the max. AC current of the inverter. |
| AC line | Smallest cross-section whose current-carrying capacity (installation method, temperature, grouping) is at least the breaker rating and whose voltage drop stays below the limit. |

The values come from the standard tables in `Data\CableTables.json` in the installation folder, with their sources. As long as these values are not confirmed (`"Confirmed": false`), the card shows a note. A cable is only sized when its length is entered. The result is a design aid; the installer remains responsible for the choice of cables and protective devices.

The **Inverter limits** card lists every limit from the datasheet and with the safety margin. **Copy** above the results copies them as text.

The meaning of every check is described in [How the Calculation Works](calculation.md#checks).

## Cables

**Change** in the **Cables** card opens the cable settings:

| Field | Meaning |
|---|---|
| Installation (DC) | Single solar cable in free air, on a surface, or two cables touching on a surface (EN 50618). |
| Cable length MPPT n | One-way length of the string cables of this MPPT; 0 means not entered. |
| Ambient temperature (DC) | Reference 60 °C; above it the current-carrying capacity is reduced. |
| Circuits laid together | Grouping factor, 1 to 9 circuits. |
| Max. voltage drop DC | Of the string voltage at the MPP; default 1 %. |
| Installation method (AC) | A1, B1, B2, C or E (IEC 60364-5-52 / DIN VDE 0298-4). |
| Length of the AC line | One way, from the inverter to the distribution board. |
| Ambient temperature (AC) | Reference 30 °C. |
| Max. voltage drop AC | Of 400 V (three-phase) or 230 V (single-phase); default 3 %. |

## Parameters

**Change** in the **Parameters** card opens the dialog with:

| Field | Meaning |
|---|---|
| Min. cell temperature (°C) | Lowest module temperature. At sunrise in winter it is close to the lowest ambient temperature, for example −20 °C. |
| Max. cell temperature (°C) | Highest module temperature. In summer cells reach 60–70 °C; the default is 70 °C. |
| Safety margin (%) | Tightens all inverter limits by this percentage, 0–50 %. |
| Unbalanced load limit (kVA) | Highest apparent power of a single-phase inverter; default 4.6 kVA (VDE-AR-N 4105). |
| Simplified registration up to (kVA) | System size of the simplified grid registration; default 30 kVA. |
| Feed-in limit (%) | Highest feed-in in percent of the PV peak power; 100 % (default) means no limit. |
| DC/AC ratio from / up to | Recommended range of the DC/AC ratio; default 0.9 to 1.3. |

The grid limits depend on your grid operator and the current rules; check the defaults before you rely on them. Tmin must be lower than Tmax. **Save** stores the values; they apply immediately and at the next start.
