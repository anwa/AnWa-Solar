# Settings, Theme and Files

## Settings page

The settings button (cog) in the title bar opens the settings:

| Setting | Meaning |
|---|---|
| Language | German or English, or the Windows display language. Applies after a restart; AnWa-Solar offers to restart. Numbers and dates follow the language (German 28,7 A and 25.11.2026, English 28.7 A and 25/11/2026). The window texts, the results, the report, the project export and this manual use the language. |
| Theme | Windows theme, light or dark; the same as the theme button. |
| Projects folder | Where projects are saved and listed, by default `%LOCALAPPDATA%\AnWa-Solar\Projects`. Applies after a restart. **Default** sets the default folder again. |
| Export folder | Where the save dialogs of the report and the project export start. Empty: the folder Windows remembers. |
| Defaults of new projects | **Parameters…** and **Cables…** set the calculation parameters and cable settings that a new project starts with. Existing projects keep their own values. |

## Theme

The theme button in the title bar switches in this order:

1. **System**: follows the Windows setting for apps (light or dark) and changes when you change it in Windows.
2. **Light**
3. **Dark**

The tooltip of the button shows the current mode. The report preview and this manual follow the theme. The choice is saved.

## Saved settings

AnWa-Solar saves your settings per Windows user in `%LOCALAPPDATA%\AnWa-Solar\settings.json`:

- the theme,
- the five most recently opened projects,
- the settings of the settings page (language, folders, defaults of new projects).

Your design (parameters, inverter, strings, cables, battery) is saved in the open [project](projects.md).

Changes are saved automatically, at the latest half a second after the last change and when you close the window. When the file cannot be read, AnWa-Solar keeps it as `settings.json.invalid-<date>` and starts with the default values.

Settings of older versions are taken over automatically on the first start; the design they contained becomes the project **My project**.

## Files and folders

| Path | Content |
|---|---|
| `C:\Program Files\AnWa-Solar` | Program, the master data series in `Data\Modules`, `Data\Inverters` and `Data\Batteries`, `Data\CableTables.json` (standard tables for cables and fuses), `appsettings.json` |
| `%LOCALAPPDATA%\AnWa-Solar\settings.json` | Your settings |
| `%LOCALAPPDATA%\AnWa-Solar\Projects` | Your projects, one JSON file each, see [Projects](projects.md) |
| `%LOCALAPPDATA%\AnWa-Solar\Data` | Your own master data and imported datasheets, see [Datasheet Import](datasheet-import.md#your-own-master-data) |
| `%LOCALAPPDATA%\AnWa-Solar\logs` | Log files, one per day (`log-YYYY-MM-DD.jsonl`) |
| `%LOCALAPPDATA%\AnWa-Solar\WebView2` | Data of the report preview and of this manual |

The **About** window shows the exact paths on your computer; **Copy** copies them to the clipboard.

Uninstalling AnWa-Solar keeps the folder `%LOCALAPPDATA%\AnWa-Solar`. Delete it by hand to remove your settings, logs and own master data.
