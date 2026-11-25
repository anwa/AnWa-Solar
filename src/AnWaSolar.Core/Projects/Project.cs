using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Projects;

/// <summary>
/// A design with everything entered in the application: project data, parameters, the inverter, the strings, the cable
/// settings and the battery. Inverter, modules and battery are stored with their key (manufacturer and model), their
/// datasheet file and a copy of their datasheet values; when loading, the current master data entry is used and the
/// copy only when the entry is missing.
/// </summary>
public sealed class Project
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Name { get; set; } = string.Empty;

    public string? Customer { get; set; }

    /// <summary>Address of the PV system.</summary>
    public string? Address { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public CalculationParameters Parameters { get; set; } = new();

    /// <summary>Copy of the selected inverter, or null.</summary>
    public Inverter? Inverter { get; set; }

    public List<ProjectString> Strings { get; set; } = [];

    public CableSettings Cables { get; set; } = new();

    public BatterySettings Battery { get; set; } = new();

    /// <summary>Copy of the selected battery module, or null.</summary>
    public Battery? BatteryData { get; set; }

    /// <summary>Project data without the design, for lists and the report.</summary>
    public ProjectInfo Info(string path) => new(path, Name, Customer, Address, Notes, ModifiedAt);
}

/// <summary>Configuration of one MPPT in a project.</summary>
public sealed class ProjectString
{
    public int MpptIndex { get; set; }

    /// <summary>Copy of the selected module, or null.</summary>
    public PVModule? Module { get; set; }

    public int ModulesPerString { get; set; } = 10;

    public int ParallelStrings { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Project data for lists and the report.</summary>
public sealed record ProjectInfo(string Path, string Name, string? Customer, string? Address, string? Notes, DateTime ModifiedAt);

/// <summary>Creates projects from the settings of versions before projects existed.</summary>
public static class ProjectMigration
{
    /// <summary>
    /// The design of the settings file (parameters, last selection, cables, battery) as a project, with copies of the
    /// master data entries; null when the settings contain no inverter.
    /// </summary>
    public static Project? FromSettings(ISettingsService settings, IDataStore store, string name, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(store);
        LastSelection selection = settings.LoadLastSelection();
        if (selection.Inverter is not { } inverterRef)
        {
            return null;
        }

        BatterySettings battery = settings.LoadBattery();
        return new Project
        {
            Name = name,
            CreatedAt = now,
            ModifiedAt = now,
            Parameters = settings.LoadParameters(),
            Inverter = Find(store.Inverters, inverterRef),
            Strings = [.. selection.Strings.Select(s => new ProjectString
            {
                MpptIndex = s.MpptIndex,
                Module = s.Module is null ? null : Find(store.Modules, s.Module),
                ModulesPerString = s.ModulesPerString,
                ParallelStrings = s.ParallelStrings,
                IsEnabled = s.IsEnabled,
            })],
            Cables = settings.LoadCables(),
            Battery = battery,
            BatteryData = battery.Battery is null ? null : Find(store.Batteries, battery.Battery),
        };
    }

    private static T? Find<T>(IEnumerable<T> entries, SelectedRef reference)
        where T : class, IMasterDataEntry =>
        entries.FirstOrDefault(e => string.Equals(e.Manufacturer, reference.Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Model, reference.Model, StringComparison.OrdinalIgnoreCase));
}
