# Getting Started

## Installation

Install `AnWaSolar-Setup-X.Y.Z.msi`. AnWa-Solar is installed for all users into `C:\Program Files\AnWa-Solar` and gets a Start menu entry. A newer version replaces the installed one; your settings are kept. AnWa-Solar runs on Windows 11 (x64) and needs no separate .NET installation.

## The main window

The title bar shows the version. On the right of the blue top bar are three buttons:

| Button | Function |
|---|---|
| Theme | Switches between the system theme, light and dark mode, see [Settings, Theme and Files](settings.md#theme). |
| Manual | Opens this manual (F1). |
| About | Shows version, installation folder, data folders, contact and license. |

The main window has two tabs:

- **String calculation**: the design with the sections Inverter, Strings, Results and Parameters.
- **Report**: a Markdown report of the current design, see [Report](report.md).

## A first design in five steps

1. Click **Select inverter**, choose manufacturer and model and click **Apply**.
2. For each MPPT click **Select PV module** and choose the module.
3. Set **Modules per string** with the left slider. The darker part of the slider marks the allowed range.
4. Set **Parallel strings** with the right slider.
5. Read the **Results** section. "All conditions are met." means that no limit is violated; otherwise the notes tell you which limit is exceeded and which count is allowed.

AnWa-Solar saves your design automatically and restores it at the next start.
