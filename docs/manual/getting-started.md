# Getting Started

## Installation

Install `AnWaSolar-Setup-X.Y.Z.msi`. AnWa-Solar is installed for all users into `C:\Program Files\AnWa-Solar` and gets a Start menu entry. A newer version replaces the installed one; your settings are kept. AnWa-Solar runs on Windows 11 (x64) and needs no separate .NET installation.

## The main window

The blue title bar shows the version. On its right are three buttons, followed by the window buttons (minimize, maximize, close):

| Button | Function |
|---|---|
| Theme | Switches between the system theme, light and dark mode, see [Settings, Theme and Files](settings.md#theme). |
| Manual | Opens this manual (F1). |
| About | Shows version, installation folder, data folders, contact and license. |

The main window has two tabs:

- **String calculation**: on the left the **Configuration** (inverter, parameters and one card per MPPT), on the right the **Results**.
- **Report**: a Markdown report of the current design, see [Report](report.md).

## A first design in five steps

1. In the **Inverter** card click **Select**, choose manufacturer and model and click **Apply**.
2. In each MPPT card click **Select module** and choose the module.
3. Set **Modules per string** with the first slider. The marked part of the slider is the allowed range.
4. Set **Parallel strings** with the second slider.
5. Read the **Results** on the right. The banner on top shows the overall result: green "All limits are met", or red with the number of violated limits. The MPPT cards below show which value exceeds which limit and which count is allowed.

The status bar at the bottom always shows the overall result.

AnWa-Solar saves your design automatically and restores it at the next start.
