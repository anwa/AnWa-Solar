# Battery Storage

AnWa-Solar checks whether a battery bank fits the battery input of a hybrid inverter. It does not size the battery from your consumption; you choose the battery module and how many modules you connect.

## Planning a battery

The **Battery** card in the configuration appears when the selected inverter has a battery input in the master data, or when a battery is already planned. **Change** opens the battery dialog:

| Field | Meaning |
|---|---|
| Plan a battery storage | Switches the battery check on or off. |
| Manufacturer, Model | The battery module from the [master data](master-data.md#battery). **Import…** reads a battery datasheet, see [Datasheet Import](datasheet-import.md). |
| Modules in series | 1 for low-voltage batteries. For high-voltage batteries, the modules of one tower. |
| Modules in parallel | Modules, or towers, connected in parallel. |

The lower part of the dialog shows the datasheet values of one module. **Save** applies the configuration; it is kept in your [settings](settings.md#saved-settings).

## Results

The **Battery storage** card in the results shows:

| Row | Meaning |
|---|---|
| Bank voltage | Operating voltage range and nominal voltage of the bank (modules in series × module values) against the battery voltage range of the inverter. |
| Modules in series, in parallel | The counts against the maximum of the battery datasheet. |
| Charge current, discharge current | The lower of the bank current (modules in parallel × module current) and the inverter current, with the power at the nominal bank voltage. |
| Energy | Nominal energy of all modules and, when the datasheet states it, the usable energy. |

Checks and their state:

| Check | State |
|---|---|
| The inverter has no battery input in the master data | Error |
| The nominal bank voltage lies outside the battery voltage range of the inverter | Error |
| The highest bank voltage exceeds the max. battery voltage of the inverter | Error |
| More modules in series or in parallel than the battery datasheet allows | Error |
| The lowest bank voltage lies below the min. battery voltage of the inverter: the inverter stops discharging earlier, so part of the capacity is not used | Warning |
| Several modules in series or in parallel, but the battery datasheet states no maximum | Warning |
| The bank allows a lower charge or discharge current than the inverter | Note |

The usable energy is the value of the datasheet. Many datasheets state a depth of discharge (DoD) instead; then the usable energy is the nominal energy times the DoD.

The battery management system keeps the battery voltage in its range, so the safety margin of the PV calculation does not apply to the battery. The check uses the datasheet values only; the manufacturers' compatibility lists (communication between inverter and battery management) are not part of it.

The battery cables, the busbar and the fuses are sized in the **Cables and fuses** card; enter their lengths in the [cable settings](string-calculation.md#cables).

The [report](report.md) contains the section **Battery storage** with the same values, the formulas and the notes.
