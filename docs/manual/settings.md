# Settings, Theme and Files

## Theme

The theme button in the title bar switches in this order:

1. **System**: follows the Windows setting for apps (light or dark) and changes when you change it in Windows.
2. **Light**
3. **Dark**

The tooltip of the button shows the current mode. The report preview and this manual follow the theme. The choice is saved.

## Saved settings

AnWa-Solar saves your settings per Windows user in `%LOCALAPPDATA%\AnWa-Solar\settings.json`:

- the calculation parameters (Tmin, Tmax, safety margin),
- the last selection: inverter, and per MPPT the module, the counts and whether the string is enabled,
- the theme.

Changes are saved automatically, at the latest half a second after the last change and when you close the window. When the file cannot be read, AnWa-Solar keeps it as `settings.json.invalid-<date>` and starts with the default values.

Settings of older versions are taken over automatically on the first start.

## Files and folders

| Path | Content |
|---|---|
| `C:\Program Files\AnWa-Solar` | Program, `Data\Modules.json`, `Data\Inverters.json`, `appsettings.json` |
| `%LOCALAPPDATA%\AnWa-Solar\settings.json` | Your settings |
| `%LOCALAPPDATA%\AnWa-Solar\logs` | Log files, one per day (`log-YYYY-MM-DD.jsonl`) |
| `%LOCALAPPDATA%\AnWa-Solar\WebView2` | Data of the report preview and of this manual |

The **About** window shows the exact paths on your computer; **Copy** copies them to the clipboard.

Uninstalling AnWa-Solar keeps the folder `%LOCALAPPDATA%\AnWa-Solar`. Delete it by hand to remove your settings and logs.
