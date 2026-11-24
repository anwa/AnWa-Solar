# Master Data

AnWa-Solar reads the PV modules and inverters from two JSON files in the `Data` folder of the installation (`C:\Program Files\AnWa-Solar\Data`):

- `Modules.json` with the list `Modules`,
- `Inverters.json` with the list `Inverters`.

Your own entries, for example from a [datasheet import](datasheet-import.md), are in `%LOCALAPPDATA%\AnWa-Solar\Data` in files of the same format. An own entry replaces a shipped entry with the same manufacturer and model.

Enter only values from the manufacturer datasheets. Numbers use a dot as decimal separator and may be decimal numbers, for example `12.5`.

## PV module

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

`"Temperature coefficient of Vmpp (%/°C)"` is optional. Without it the Pmax coefficient is used, see [How the Calculation Works](calculation.md#temperature-correction).

## Inverter

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
  "Max. strings per MPPT": 2
}
```

The MPPT voltage range is text in the form `"min-max"`. Currents are per MPPT input.

## Checks when loading

Every entry is checked on its own. An entry is skipped when

- a value has the wrong type, for example `"36 A"` instead of `36`,
- manufacturer or model is missing,
- a required value is 0 or negative,
- Voc is not higher than Vmpp, or Isc is lower than Impp,
- the MPPT voltage range is not in the form `"160-650"`,
- manufacturer and model appear twice.

Skipped entries and unreadable files are listed in the **Master data** message after the start and in the log; problems in your own files are marked "(own data)". The other entries are loaded normally.

> Changes in the installation folder need administrator rights and are replaced by the next update. Add your own entries through the [datasheet import](datasheet-import.md) instead; they are kept on updates.
