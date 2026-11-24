using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>True when Tmin &lt; Tmax and the margin is within 0–50 %, as the parameter dialog requires.</summary>
    public bool IsValid()
        => double.IsFinite(MinTempC) && double.IsFinite(MaxTempC) && MinTempC < MaxTempC
           && SafetyMarginPct is >= 0 and <= 50;

    public CalculationParameters Clone() => new()
    {
        MinTempC = MinTempC,
        MaxTempC = MaxTempC,
        SafetyMarginPct = SafetyMarginPct,
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
}

public interface ISettingsService
{
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
