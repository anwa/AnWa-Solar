# Troubleshooting

## "Some master data could not be loaded"

After the start a message lists master data entries that were skipped, with their series file and the reason. Correct the entry as described in [Master Data](master-data.md#checks-when-loading). The remaining entries are available.

## The last inverter or module is missing

The saved selection refers to manufacturer and model. When an entry was renamed or removed from the master data, it cannot be restored; select it again.

## "No valid range" for modules per string

The module does not fit the inverter: either the string voltage at Tmax stays below the MPPT lower bound or start voltage even with the most modules that the maximum DC voltage allows, or the other way round. Choose another module or inverter, or check the parameters and the safety margin.

## The report preview or the manual stays empty

Both use the Microsoft Edge WebView2 Runtime, which is part of Windows 11. When it is missing or damaged, AnWa-Solar shows a message and the report tab shows the Markdown source. Install or repair the "Microsoft Edge WebView2 Runtime" from Microsoft and start AnWa-Solar again.

## Settings were reset

When `settings.json` cannot be read, AnWa-Solar starts with the defaults and keeps the old file as `settings.json.invalid-<date>` in `%LOCALAPPDATA%\AnWa-Solar`, see [Settings, Theme and Files](settings.md#saved-settings).

## Log files

AnWa-Solar writes a log file per day to `%LOCALAPPDATA%\AnWa-Solar\logs`. Add the log of the day when you report a problem to the author (see **About**).
