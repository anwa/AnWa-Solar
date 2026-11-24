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
    public double SicherheitsmargePct { get; set; } = 10.0;

    /// <summary>True when Tmin &lt; Tmax and the margin is within 0–50 %, as the parameter dialog requires.</summary>
    public bool IsValid()
        => double.IsFinite(MinTempC) && double.IsFinite(MaxTempC) && MinTempC < MaxTempC
           && SicherheitsmargePct is >= 0 and <= 50;

    public CalculationParameters Clone() => new()
    {
        MinTempC = MinTempC,
        MaxTempC = MaxTempC,
        SicherheitsmargePct = SicherheitsmargePct,
    };
}

/// <summary>Reference to a master data entry by manufacturer and model.</summary>
public class SelectedRef
{
    public string Hersteller { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}

/// <summary>Saved configuration of one MPPT.</summary>
public class MpptStringSelection
{
    public int MpptIndex { get; set; }

    public SelectedRef? Module { get; set; }

    public int ModuleProString { get; set; } = 10;

    public int ParalleleStrings { get; set; } = 1;

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
    public const int CurrentSchemaVersion = 1;

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
/// The file is written atomically. When it does not exist yet, the values are imported once from the
/// <c>appsettings.json</c> next to the executable, where versions before 0.5.0 stored them.
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
            _logger.LogInformation("Berechnungsparameter geladen: Tmin={Tmin} °C, Tmax={Tmax} °C, Sicherheitsmarge={Margin} %.",
                p.MinTempC, p.MaxTempC, p.SicherheitsmargePct);
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
                _logger.LogInformation("Berechnungsparameter gespeichert in {Path}.", SettingsFilePath);
            }

            return ok;
        }
    }

    public LastSelection LoadLastSelection()
    {
        lock (_lock)
        {
            LastSelection result = Clone(EnsureLoaded().LastSelection);
            _logger.LogInformation("Letzte Auswahl geladen: WR={Wr}, Strings={Count}.",
                result.Inverter is null ? "(none)" : $"{result.Inverter.Hersteller} {result.Inverter.Model}",
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
                _logger.LogInformation("Letzte Auswahl gespeichert in {Path}.", SettingsFilePath);
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
                _logger.LogInformation("Einstellungen aus {Legacy} nach {Path} übernommen.", _legacyAppSettingsPath, SettingsFilePath);
            }
        }
        else
        {
            _settings = new UserSettings();
        }

        if (!_settings.Parameters.IsValid())
        {
            _logger.LogWarning("Ungültige Berechnungsparameter in {Path}, es werden Standardwerte verwendet.", SettingsFilePath);
            _settings.Parameters = new CalculationParameters();
        }

        return _settings;
    }

    // Reads settings.json; an unreadable file is kept as a backup and replaced by defaults.
    private UserSettings? ReadSettingsFile()
    {
        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            UserSettings? settings = JsonSerializer.Deserialize<UserSettings>(json, s_jsonOptions);
            if (settings is not null)
            {
                settings.Parameters ??= new CalculationParameters();
                settings.LastSelection ??= new LastSelection();
                settings.LastSelection.Strings ??= [];
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            string backup = $"{SettingsFilePath}.invalid-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
            try
            {
                File.Copy(SettingsFilePath, backup, overwrite: true);
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                backup = "(keine Sicherung möglich)";
            }

            _logger.LogError(ex, "Einstellungen in {Path} konnten nicht gelesen werden. Standardwerte werden verwendet, Sicherung: {Backup}.",
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
            _logger.LogError(ex, "Einstellungen konnten nicht in {Path} gespeichert werden.", SettingsFilePath);
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(deleteEx, "Temporäre Datei {Path} konnte nicht gelöscht werden.", tempPath);
            }

            return false;
        }
    }

    // Reads parameters and last selection from the appsettings.json of versions before 0.5.0.
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
            if (parameterNode is null && lastNode is null)
            {
                return null;
            }

            var settings = new UserSettings();
            if (parameterNode is not null)
            {
                var defaults = new CalculationParameters();
                settings.Parameters = new CalculationParameters
                {
                    MinTempC = parameterNode["MinTempC"]?.GetValue<double>() ?? defaults.MinTempC,
                    MaxTempC = parameterNode["MaxTempC"]?.GetValue<double>() ?? defaults.MaxTempC,
                    SicherheitsmargePct = parameterNode["SicherheitsmargePct"]?.GetValue<double>() ?? defaults.SicherheitsmargePct,
                };
            }

            if (lastNode is not null)
            {
                settings.LastSelection = lastNode.Deserialize<LastSelection>(s_jsonOptions) ?? new LastSelection();
                settings.LastSelection.Strings ??= [];
            }

            return settings;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Alte Einstellungen in {Path} konnten nicht übernommen werden.", _legacyAppSettingsPath);
            return null;
        }
    }

    private static LastSelection Clone(LastSelection selection)
        => JsonSerializer.Deserialize<LastSelection>(JsonSerializer.Serialize(selection, s_jsonOptions), s_jsonOptions) ?? new LastSelection();
}

#endregion
