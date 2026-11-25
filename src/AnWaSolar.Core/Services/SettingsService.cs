using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using AnWaSolar.Core.Calculation;

using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Services;

#region Models

/// <summary>Temperature range and safety margin of the string calculation.</summary>
public class CalculationParameters
{
    /// <summary>Lowest module (cell) temperature in °C; at sunrise it is close to the ambient temperature.</summary>
    public double MinTempC { get; set; } = -20.0;

    /// <summary>Highest module (cell) temperature in °C; in summer cells reach 60–70 °C.</summary>
    public double MaxTempC { get; set; } = 70.0;

    /// <summary>Safety margin in percent that tightens all inverter limits.</summary>
    public double SafetyMarginPct { get; set; } = 10.0;

    /// <summary>Highest AC apparent power of a single-phase inverter (unbalanced load limit, VDE-AR-N 4105), in VA.</summary>
    public double UnbalancedLoadLimitVA { get; set; } = 4600.0;

    /// <summary>System size up to which the simplified grid registration applies, in VA.</summary>
    public double SimplifiedRegistrationLimitVA { get; set; } = 30000.0;

    /// <summary>Feed-in limit in percent of the PV peak power; 100 means no limit.</summary>
    public double FeedInLimitPct { get; set; } = 100.0;

    /// <summary>Lower end of the recommended DC/AC ratio (PV peak power to rated AC power).</summary>
    public double DcAcRatioMin { get; set; } = 0.9;

    /// <summary>Upper end of the recommended DC/AC ratio.</summary>
    public double DcAcRatioMax { get; set; } = 1.3;

    /// <summary>True when the values are within the ranges the parameter dialog allows.</summary>
    public bool IsValid()
        => double.IsFinite(MinTempC) && double.IsFinite(MaxTempC) && MinTempC < MaxTempC
           && SafetyMarginPct is >= 0 and <= 50
           && UnbalancedLoadLimitVA > 0 && SimplifiedRegistrationLimitVA > 0
           && FeedInLimitPct is > 0 and <= 100
           && DcAcRatioMin > 0 && DcAcRatioMin < DcAcRatioMax;

    public CalculationParameters Clone() => new()
    {
        MinTempC = MinTempC,
        MaxTempC = MaxTempC,
        SafetyMarginPct = SafetyMarginPct,
        UnbalancedLoadLimitVA = UnbalancedLoadLimitVA,
        SimplifiedRegistrationLimitVA = SimplifiedRegistrationLimitVA,
        FeedInLimitPct = FeedInLimitPct,
        DcAcRatioMin = DcAcRatioMin,
        DcAcRatioMax = DcAcRatioMax,
    };
}

/// <summary>Reference to a master data entry by manufacturer and model.</summary>
public class SelectedRef
{
    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}

/// <summary>Saved configuration of one MPPT.</summary>
public class MpptStringSelection
{
    public int MpptIndex { get; set; }

    public SelectedRef? Module { get; set; }

    public int ModulesPerString { get; set; } = 10;

    public int ParallelStrings { get; set; } = 1;

    public bool IsEnabled { get; set; } = true;
}

/// <summary>Last selected inverter and string configuration.</summary>
public class LastSelection
{
    public SelectedRef? Inverter { get; set; }

    public List<MpptStringSelection> Strings { get; set; } = [];
}

/// <summary>Content of the user settings file.</summary>
public sealed class UserSettings
{
    /// <summary>Version 2 uses English property names; version 1 (0.5.0–0.6.0) used German ones.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public CalculationParameters Parameters { get; set; } = new();

    public LastSelection LastSelection { get; set; } = new();

    /// <summary>Cable runs and installation conditions; optional.</summary>
    public CableSettings Cables { get; set; } = new();

    /// <summary>Planned battery bank; optional.</summary>
    public BatterySettings Battery { get; set; } = new();

    /// <summary>Paths of the most recently opened projects, the last one first; at most five.</summary>
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>Preferences of the settings page.</summary>
    public AppPreferences Preferences { get; set; } = new();

    /// <summary>Colour theme; optional, files without it use <see cref="AppTheme.System"/>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; set; } = AppTheme.System;
}

/// <summary>Preferences of the settings page: language, folders and the defaults of new projects.</summary>
public sealed class AppPreferences
{
    /// <summary>"de" or "en"; null follows the Windows display language.</summary>
    public string? Language { get; set; }

    /// <summary>Folder of the projects; null is <c>%LOCALAPPDATA%\AnWa-Solar\Projects</c>.</summary>
    public string? ProjectsFolder { get; set; }

    /// <summary>Folder the export dialogs start in; null is the last folder Windows remembers.</summary>
    public string? ExportFolder { get; set; }

    /// <summary>Calculation parameters of new projects.</summary>
    public CalculationParameters DefaultParameters { get; set; } = new();

    /// <summary>Cable settings of new projects.</summary>
    public CableSettings DefaultCables { get; set; } = new();

    public AppPreferences Clone() => new()
    {
        Language = Language,
        ProjectsFolder = ProjectsFolder,
        ExportFolder = ExportFolder,
        DefaultParameters = DefaultParameters.Clone(),
        DefaultCables = DefaultCables.Clone(),
    };
}

/// <summary>Colour theme of the application.</summary>
public enum AppTheme
{
    /// <summary>Follows the Windows app theme, also when it changes while the application runs.</summary>
    System,

    Light,

    Dark,
}

public interface ISettingsService
{
    AppTheme LoadTheme();

    AppPreferences LoadPreferences();

    bool SavePreferences(AppPreferences preferences);

    CableSettings LoadCables();

    bool SaveCables(CableSettings cables);

    BatterySettings LoadBattery();

    /// <summary>The most recently opened projects, the last one first.</summary>
    IReadOnlyList<string> LoadRecentProjects();

    /// <summary>Puts a project at the top of the recent projects; keeps at most five.</summary>
    bool AddRecentProject(string path);

    bool SaveBattery(BatterySettings battery);

    bool SaveTheme(AppTheme theme);

    CalculationParameters LoadParameters();

    bool SaveParameters(CalculationParameters parameters);

    LastSelection LoadLastSelection();

    bool SaveLastSelection(LastSelection selection);
}

/// <summary>Folder for per-user data (settings and logs).</summary>
public interface IUserDataLocation
{
    string DirectoryPath { get; }
}

/// <summary><c>%LOCALAPPDATA%\AnWa-Solar</c>.</summary>
public sealed class UserDataLocation : IUserDataLocation
{
    public string DirectoryPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnWa-Solar");
}

#endregion

#region Implementation

/// <summary>
/// Stores calculation parameters and the last selection in <c>settings.json</c> in the per-user data folder.
/// The file is written atomically. Files of schema version 1 are migrated to the current version. When no file
/// exists yet, the values are imported once from the <c>appsettings.json</c> next to the executable, where versions
/// up to 0.4.0 stored them.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger<SettingsService> _logger;
    private readonly string _legacyAppSettingsPath;
    private readonly Lock _lock = new();
    private UserSettings? _settings;

    public SettingsService(ILogger<SettingsService> logger, IUserDataLocation location)
        : this(logger, location, Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
    {
    }

    internal SettingsService(ILogger<SettingsService> logger, IUserDataLocation location, string legacyAppSettingsPath)
    {
        ArgumentNullException.ThrowIfNull(location);
        _logger = logger;
        _legacyAppSettingsPath = legacyAppSettingsPath;
        SettingsFilePath = Path.Combine(location.DirectoryPath, FileName);
    }

    /// <summary>Full path of the settings file.</summary>
    public string SettingsFilePath { get; }

    public CalculationParameters LoadParameters()
    {
        lock (_lock)
        {
            CalculationParameters p = EnsureLoaded().Parameters.Clone();
            _logger.LogInformation("Calculation parameters loaded: Tmin={Tmin} °C, Tmax={Tmax} °C, safety margin={Margin} %.",
                p.MinTempC, p.MaxTempC, p.SafetyMarginPct);
            return p;
        }
    }

    public bool SaveParameters(CalculationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        lock (_lock)
        {
            EnsureLoaded().Parameters = parameters.Clone();
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Calculation parameters saved to {Path}.", SettingsFilePath);
            }

            return ok;
        }
    }

    public LastSelection LoadLastSelection()
    {
        lock (_lock)
        {
            LastSelection result = Clone(EnsureLoaded().LastSelection);
            _logger.LogInformation("Last selection loaded: inverter={Inverter}, strings={Count}.",
                result.Inverter is null ? "(none)" : $"{result.Inverter.Manufacturer} {result.Inverter.Model}",
                result.Strings.Count);
            return result;
        }
    }

    public bool SaveLastSelection(LastSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        lock (_lock)
        {
            EnsureLoaded().LastSelection = Clone(selection);
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Last selection saved to {Path}.", SettingsFilePath);
            }

            return ok;
        }
    }

    public CableSettings LoadCables()
    {
        lock (_lock)
        {
            return EnsureLoaded().Cables.Clone();
        }
    }

    public bool SaveCables(CableSettings cables)
    {
        ArgumentNullException.ThrowIfNull(cables);
        lock (_lock)
        {
            EnsureLoaded().Cables = cables.Clone();
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Cable settings saved to {Path}.", SettingsFilePath);
            }

            return ok;
        }
    }

    public BatterySettings LoadBattery()
    {
        lock (_lock)
        {
            return EnsureLoaded().Battery.Clone();
        }
    }

    public bool SaveBattery(BatterySettings battery)
    {
        ArgumentNullException.ThrowIfNull(battery);
        lock (_lock)
        {
            EnsureLoaded().Battery = battery.Clone();
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Battery settings saved to {Path}.", SettingsFilePath);
            }

            return ok;
        }
    }

    public const int MaxRecentProjects = 5;

    public IReadOnlyList<string> LoadRecentProjects()
    {
        lock (_lock)
        {
            return [.. EnsureLoaded().RecentProjects];
        }
    }

    public bool AddRecentProject(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (_lock)
        {
            List<string> recent = EnsureLoaded().RecentProjects;
            recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            recent.Insert(0, path);
            if (recent.Count > MaxRecentProjects)
            {
                recent.RemoveRange(MaxRecentProjects, recent.Count - MaxRecentProjects);
            }

            return TryWrite();
        }
    }

    public AppPreferences LoadPreferences()
    {
        lock (_lock)
        {
            return EnsureLoaded().Preferences.Clone();
        }
    }

    public bool SavePreferences(AppPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        lock (_lock)
        {
            EnsureLoaded().Preferences = preferences.Clone();
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Preferences saved to {Path}: language {Language}.", SettingsFilePath, preferences.Language ?? "(Windows)");
            }

            return ok;
        }
    }

    public AppTheme LoadTheme()
    {
        lock (_lock)
        {
            return EnsureLoaded().Theme;
        }
    }

    public bool SaveTheme(AppTheme theme)
    {
        lock (_lock)
        {
            EnsureLoaded().Theme = theme;
            bool ok = TryWrite();
            if (ok)
            {
                _logger.LogInformation("Theme {Theme} saved to {Path}.", theme, SettingsFilePath);
            }

            return ok;
        }
    }

    private UserSettings EnsureLoaded()
    {
        if (_settings is not null)
        {
            return _settings;
        }

        if (File.Exists(SettingsFilePath))
        {
            _settings = ReadSettingsFile() ?? new UserSettings();
        }
        else if (TryImportLegacy() is { } imported)
        {
            _settings = imported;
            if (TryWrite())
            {
                _logger.LogInformation("Settings imported from {Legacy} to {Path}.", _legacyAppSettingsPath, SettingsFilePath);
            }
        }
        else
        {
            _settings = new UserSettings();
        }

        if (!_settings.Parameters.IsValid())
        {
            _logger.LogWarning("Invalid calculation parameters in {Path}; the defaults are used.", SettingsFilePath);
            _settings.Parameters = new CalculationParameters();
        }

        return _settings;
    }

    // Reads settings.json and migrates older schema versions; an unreadable file is kept as a backup.
    private UserSettings? ReadSettingsFile()
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(SettingsFilePath)) is not JsonObject root)
            {
                throw new JsonException("The settings file does not contain a JSON object.");
            }

            int version = root["SchemaVersion"]?.GetValue<int>() ?? 1;
            UserSettings settings = ReadSettings(root["Parameters"] as JsonObject, root["LastSelection"] as JsonObject);
            if (Enum.TryParse(Text(root, "Theme"), ignoreCase: true, out AppTheme theme) && Enum.IsDefined(theme))
            {
                settings.Theme = theme;
            }

            if (root["Cables"] is JsonObject cables)
            {
                try
                {
                    settings.Cables = cables.Deserialize<CableSettings>() ?? new CableSettings();
                }
                catch (JsonException ex)
                {
                    // Invalid cable settings do not discard the other settings.
                    _logger.LogWarning(ex, "Cable settings in {Path} cannot be read; the defaults are used.", SettingsFilePath);
                }
            }

            // Without preferences (versions before 0.22.0) the last parameters and cables become the defaults.
            settings.Preferences.DefaultParameters = settings.Parameters.Clone();
            settings.Preferences.DefaultCables = settings.Cables.Clone();
            if (root["Preferences"] is JsonObject preferences)
            {
                try
                {
                    settings.Preferences = preferences.Deserialize<AppPreferences>() ?? new AppPreferences();
                    if (!settings.Preferences.DefaultParameters.IsValid())
                    {
                        settings.Preferences.DefaultParameters = new CalculationParameters();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Preferences in {Path} cannot be read; the defaults are used.", SettingsFilePath);
                }
            }

            if (root["RecentProjects"] is JsonArray recent)
            {
                settings.RecentProjects = [.. recent.Select(n => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : null)
                    .OfType<string>().Where(p => p.Length > 0).Take(MaxRecentProjects)];
            }

            if (root["Battery"] is JsonObject battery)
            {
                try
                {
                    settings.Battery = battery.Deserialize<BatterySettings>() ?? new BatterySettings();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Battery settings in {Path} cannot be read; the defaults are used.", SettingsFilePath);
                }
            }

            if (version < UserSettings.CurrentSchemaVersion)
            {
                _settings = settings;
                if (TryWrite())
                {
                    _logger.LogInformation("Settings in {Path} migrated from schema version {Old} to {New}.",
                        SettingsFilePath, version, UserSettings.CurrentSchemaVersion);
                }
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException)
        {
            string backup = $"{SettingsFilePath}.invalid-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
            try
            {
                File.Copy(SettingsFilePath, backup, overwrite: true);
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                backup = "(no backup possible)";
            }

            _logger.LogError(ex, "Settings in {Path} could not be read. The defaults are used; backup: {Backup}.",
                SettingsFilePath, backup);
            return null;
        }
    }

    private bool TryWrite()
    {
        string tempPath = SettingsFilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            string json = JsonSerializer.Serialize(_settings, s_jsonOptions);
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, SettingsFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Settings could not be saved to {Path}.", SettingsFilePath);
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(deleteEx, "Temporary file {Path} could not be deleted.", tempPath);
            }

            return false;
        }
    }

    // Reads parameters and last selection from the appsettings.json of versions up to 0.4.0.
    private UserSettings? TryImportLegacy()
    {
        try
        {
            if (!File.Exists(_legacyAppSettingsPath))
            {
                return null;
            }

            if (JsonNode.Parse(File.ReadAllText(_legacyAppSettingsPath))?["AnWaSolar"] is not JsonObject anWa)
            {
                return null;
            }

            var parameterNode = anWa["Parameters"] as JsonObject;
            var lastNode = anWa["LastSelection"] as JsonObject;
            return parameterNode is null && lastNode is null ? null : ReadSettings(parameterNode, lastNode);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Old settings in {Path} could not be imported.", _legacyAppSettingsPath);
            return null;
        }
    }

    // Reads both the current English property names and the German names of schema version 1 and of the
    // appsettings.json of versions up to 0.4.0.
    private static UserSettings ReadSettings(JsonObject? parameters, JsonObject? lastSelection)
    {
        var defaults = new CalculationParameters();
        var settings = new UserSettings
        {
            Parameters = new CalculationParameters
            {
                MinTempC = Value<double>(parameters, "MinTempC") ?? defaults.MinTempC,
                MaxTempC = Value<double>(parameters, "MaxTempC") ?? defaults.MaxTempC,
                SafetyMarginPct = Value<double>(parameters, "SafetyMarginPct", "SicherheitsmargePct") ?? defaults.SafetyMarginPct,
                UnbalancedLoadLimitVA = Value<double>(parameters, "UnbalancedLoadLimitVA") ?? defaults.UnbalancedLoadLimitVA,
                SimplifiedRegistrationLimitVA = Value<double>(parameters, "SimplifiedRegistrationLimitVA") ?? defaults.SimplifiedRegistrationLimitVA,
                FeedInLimitPct = Value<double>(parameters, "FeedInLimitPct") ?? defaults.FeedInLimitPct,
                DcAcRatioMin = Value<double>(parameters, "DcAcRatioMin") ?? defaults.DcAcRatioMin,
                DcAcRatioMax = Value<double>(parameters, "DcAcRatioMax") ?? defaults.DcAcRatioMax,
            },
            LastSelection = new LastSelection { Inverter = ReadRef(lastSelection?["Inverter"] as JsonObject) },
        };

        if (lastSelection?["Strings"] is JsonArray strings)
        {
            foreach (JsonObject item in strings.OfType<JsonObject>())
            {
                settings.LastSelection.Strings.Add(new MpptStringSelection
                {
                    MpptIndex = Value<int>(item, "MpptIndex") ?? 0,
                    Module = ReadRef(item["Module"] as JsonObject),
                    ModulesPerString = Value<int>(item, "ModulesPerString", "ModuleProString") ?? 10,
                    ParallelStrings = Value<int>(item, "ParallelStrings", "ParalleleStrings") ?? 1,
                    IsEnabled = Value<bool>(item, "IsEnabled") ?? true,
                });
            }
        }

        return settings;
    }

    private static SelectedRef? ReadRef(JsonObject? node)
    {
        if (node is null)
        {
            return null;
        }

        return new SelectedRef
        {
            Manufacturer = Text(node, "Manufacturer", "Hersteller") ?? string.Empty,
            Model = Text(node, "Model") ?? string.Empty,
        };
    }

    // Value of the first property that exists; null when none exists.
    private static T? Value<T>(JsonObject? node, params string[] names)
        where T : struct
    {
        foreach (string name in names)
        {
            if (node?[name] is JsonValue value)
            {
                return value.GetValue<T>();
            }
        }

        return null;
    }

    private static string? Text(JsonObject? node, params string[] names)
    {
        foreach (string name in names)
        {
            if (node?[name] is JsonValue value)
            {
                return value.GetValue<string>();
            }
        }

        return null;
    }

    private static LastSelection Clone(LastSelection selection)
        => JsonSerializer.Deserialize<LastSelection>(JsonSerializer.Serialize(selection, s_jsonOptions), s_jsonOptions) ?? new LastSelection();
}

#endregion
