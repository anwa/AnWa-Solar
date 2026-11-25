# Datasheet Import

AnWa-Solar can read the values of a PV module, an inverter or a battery from its PDF datasheet. Most datasheets describe a whole series with several power classes; the import saves the series with all power classes you keep. You check and complete the values in a dialog; **Apply** saves them, together with a copy of the datasheet, in your own master data. The power classes can then be selected like the shipped ones.

## Importing a datasheet

1. Click **Import datasheet** in the title bar of the main window, or **Import…** in the module, inverter or battery dialog.
2. Choose the PDF file of the datasheet.
3. Check the dialog:
   - **PV module**, **Inverter** or **Battery**: AnWa-Solar recognises the kind of datasheet; switch it if it is wrong.
   - **Model or power class in the datasheet**: many datasheets describe several power classes or models in columns. Choose a power class to see and correct its values; your changes are kept when you switch to another one.
   - **Series**: the name of the series, suggested from the model names (for example `SUN-SG05LP3-EU-SM2` for SUN-14K … SUN-20K-SG05LP3-EU-SM2) or from the file name. All saved power classes form this series.
   - **Save this power class**: clear it for power classes you do not want to keep; the line below counts the saved ones. The manufacturer applies to the whole series.
   - **Datasheet language**: German or English, suggested from the text of the PDF (otherwise the language of the application). A series keeps one German and one English datasheet, see [Datasheets in two languages](#datasheets-in-two-languages).
   - Each value has a mark on the right:

     | Mark | Meaning |
     |---|---|
     | Green tick | Read from the datasheet. The tooltip shows the datasheet line it was read from. |
     | Orange warning | Not found. Enter the value from the datasheet. |
     | Grey info | Entered or changed by you, or an optional value that was not found. |

4. Click **Open datasheet** to show the PDF next to the dialog and compare every value.
5. Click **Apply**. When a value of a saved power class is missing or does not fit, the dialog lists what to correct, with the power class.

> The values are read automatically from the text of the PDF. Datasheets differ in layout, so a value can be missing or, rarely, wrong. The string check relies on these values: check every one, especially Voc, the temperature coefficient of Voc and the maximum DC input voltage.

## Checks

Besides the [checks when loading](master-data.md#checks-when-loading), the import requires:

- the temperature coefficients of Pmax and Voc are negative (a lost minus sign would make the voltage when cold too low),
- the minimum operating temperature is lower than the maximum,
- the MPPT voltage range lies below the maximum DC input voltage.

Numbers may use a decimal point or a decimal comma.

## Values that need attention

- **Max. DC input power**: some inverter datasheets also state a higher "max. PV access power". AnWa-Solar uses the DC input power of the inverter.
- **Currents per MPPT**: datasheets often write "36+36" for two MPPTs. Enter the value of one MPPT, 36.
- **MPPT count and strings**: "2/2+2" means 2 MPPTs with up to 2 strings each.
- **AC currents**: datasheets often state two values such as "27.3/26.1" for 220/380 V and 230/400 V. AnWa-Solar takes the second value, for 230/400 V.
- **Module leads**: "Photovoltaikkabel: 4,0 mm²" and "Hochformat: 1100/1100 mm" give the cross-section and the plus and minus lengths; the portrait lengths are taken, not the landscape ones. They are optional.
- **Max. series fuse rating** (for example "Maximale Absicherung 20 A") is optional; without it the string fuse check only gives a note.
- **Battery values of a hybrid inverter** (battery voltage range, max. charge and discharge current, battery inputs) are optional; leave them empty for inverters without battery input.
- **Battery currents**: enter the continuous charge and discharge current of one module, not the peak value ("Spitzenwert 280 A (10 Sek.)").
- **Battery model names** are graphics in some datasheets. The columns are then shown as "Column 1 · 12.03 kWh"; enter the model name yourself.
- **Usable energy and depth of discharge**: most battery datasheets state a depth of discharge instead of a usable energy. Enter what the datasheet states.
- **AC phases** are recognised from the grid connection ("three-phase", "3L+N+PE"); check them.
- **Temperature coefficients in mV/°C or mA/°C** are not converted; enter the value in %/°C from the datasheet or calculate it from the datasheet values.
- **Scanned datasheets** contain no text. The dialog then opens empty, and you enter all values by hand; the datasheet is stored with them.

## Your own master data

Imported entries are stored per Windows user in `%LOCALAPPDATA%\AnWa-Solar\Data`:

| File | Content |
|---|---|
| `Modules\<Manufacturer>\<Series>.json` (and `Inverters`, `Batteries`) | Your series, in the same format as the shipped [master data](master-data.md) |
| `…\<Series> (de).pdf`, `…\<Series> (en).pdf` | The German and the English datasheet, next to their series file |
| `…\<Series>.pdf` | A datasheet of a version before 1.0.0, whose language is not stated |

An own series with the same manufacturer and name as a shipped one replaces it in the selection; the shipped files are not changed. Importing the series again replaces your series and its datasheet in the chosen language; the datasheet in the other language stays.

## Datasheets in two languages

Many manufacturers publish a datasheet in German and one in English. A series keeps one of each: import the series a second time with the other datasheet and choose its language, or attach it in the [component database](component-database.md).

- When both exist, AnWa-Solar shows and exports the datasheet in the language of the application (see [Settings](settings.md#settings-page)).
- When there is only one datasheet, it is used whatever its language.
- A datasheet of a version before 1.0.0 has no stated language; it is used when there is none in the language of the application.

## Showing a datasheet

Entries with an imported datasheet show a PDF button:

- in the module and inverter selection (**Datasheet**),
- in the main window next to the inverter and next to each selected module.

The datasheet opens in its own window with zoom, search and print.
