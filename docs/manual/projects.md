# Projects

Every design is a project: a file that contains everything you entered, the project data, the parameters, the inverter, the strings with their modules, the cable settings and the battery.

## Choosing a project at the start

When AnWa-Solar starts, the **Open project** dialog appears:

- **Recent projects** lists the five projects you opened last. The last project is preselected: press **Enter** or click **Open** to continue with it. Double-click opens any other project of the list.
- **New project…** asks for the project data and starts with an empty design and the defaults of new projects from the [settings](settings.md#settings-page).
- **Other…** opens a project file that is not in the list.
- **Exit** ends AnWa-Solar.

On the first start of a version with projects, your previous design becomes the project **My project**.

## The Project card

The **Project** card at the top of the configuration shows the name, the customer and the address:

| Button | Function |
|---|---|
| Edit | Changes name, customer, address of the PV system and notes. They appear at the beginning of the [report](report.md). |
| Projects | Opens the project dialog to switch to another project or to create a new one. The current project is saved first. |
| Export ZIP | Saves a ZIP file, see below. |

The window title shows the name of the open project.

## Saving

Every change is saved automatically into the project, at the latest half a second after the last change, when you switch the project and when you close the window. Projects are stored per Windows user in `%LOCALAPPDATA%\AnWa-Solar\Projects`, one JSON file per project named after the project; the [settings](settings.md#settings-page) can set another projects folder.

For the inverter, every module and the battery, the project stores manufacturer and model, the datasheet file and a copy of the datasheet values. When a project is opened, the entries of the [master data](master-data.md) are used, so that corrected values apply. When an entry is missing, for example on another computer, the copy from the project is used, and a message lists these entries; import their datasheets to add them to the master data.

## Export as ZIP

**Export ZIP** saves one ZIP file with a folder named after the project, in the language of the application (the report, the file names and the list of missing datasheets):

| File | Content |
|---|---|
| `<Project> - Report.pdf` | The report as A4 PDF, created at the export. Without the report preview (Microsoft Edge WebView2) the report is included as Markdown instead. |
| `Datasheets\<Manufacturer> <Model>.pdf` | The datasheets of the inverter, the modules and the battery, as far as they were imported: the one in the language of the application, otherwise the only one. |
| `<Project>.json` | The project file; **Other…** in the project dialog opens it again. |
| `Missing datasheets.txt` | Only when an entry has no datasheet, for example shipped master data. |
