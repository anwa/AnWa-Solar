# Component Database

The component database shows all PV modules, inverters and batteries of the [master data](master-data.md) and lets you create and change them. Open it with the database button in the title bar of the main window.

## The window

On the left, a tree lists the kinds (**PV modules**, **Inverters**, **Batteries**), below them the manufacturers and their series. The number in brackets is the number of power classes; **own** marks your own series.

On the right, the editor shows the selected series:

- **Manufacturer** and **Series** of the series.
- A line that tells whether it is a shipped or an own series and which datasheets are attached (German, English).
- A table with one row per value and one column per power class, the way datasheets are laid out. The power classes are ordered from left to right by ascending power (modules by Pmax, inverters by the rated AC power, batteries by the nominal energy), also in the selection dialogs and when saved. Bold rows are required values; the tooltip of a row explains it.

| Button | Function |
|---|---|
| Arrow at the end of a row | Copies the value of the first power class to all power classes, for values that are the same in the whole series. |
| Copy above a column | Adds a copy of this power class; enter its model. |
| Delete above a column | Removes this power class. |
| Add power class | Adds a power class with the values of the last one. |
| Attach PDF… | Attaches a **German** or **English** datasheet to the series; it is stored next to the series file when you save and replaces the datasheet in that language. See [Datasheets in two languages](datasheet-import.md#datasheets-in-two-languages). |
| Datasheet | Shows the attached datasheet; with several, a menu offers them by language. |

## Creating, changing and deleting

- **New series** starts an empty series of the kind selected on the left. Enter manufacturer, series and the values of each power class.
- **Import…** reads a PDF datasheet as a new series, see [Datasheet Import](datasheet-import.md).
- **Save** checks every power class with the same rules as the import and saves the series in your own master data. The list of problems names the power class.
- A **shipped series** cannot be changed in the installation folder: saving creates an own copy that replaces it in the selection.
- Renaming a series or its manufacturer moves your series with its datasheets to the new name.
- **Delete series** deletes an own series with its datasheets after a confirmation. When it replaced a shipped series, the shipped series comes back.
- **Discard changes** returns to the saved values. Before you switch to another series or close the window with unsaved changes, AnWa-Solar asks.

After the window is closed, the main window uses the changed values; inverter, modules and battery of the open project are resolved again.
