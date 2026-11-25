using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;

using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Services;

/// <summary>A master data file or entry that could not be used.</summary>
/// <param name="FileName">Name of the master data file.</param>
/// <param name="Entry">Position of the entry (1-based) and, when readable, its manufacturer and model; null for the whole file.</param>
/// <param name="Message">Reason, as shown to the user.</param>
public sealed record DataLoadIssue(string FileName, string? Entry, string Message)
{
    public override string ToString() => Entry is null ? $"{FileName}: {Message}" : $"{FileName}, {Entry}: {Message}";
}

public interface IDataStore
{
    IReadOnlyList<PVModule> Modules { get; }

    IReadOnlyList<Inverter> Inverters { get; }

    IReadOnlyList<Battery> Batteries { get; }

    /// <summary>Problems of the last <see cref="Load"/>; skipped entries are listed individually.</summary>
    IReadOnlyList<DataLoadIssue> Issues { get; }

    /// <summary>Standard tables for cable and fuse sizing; null when the file is missing or invalid.</summary>
    CableTables? CableTables { get; }

    void Load();

    /// <summary>Full path of the datasheet of an entry, or null when it has none or the file is missing.</summary>
    string? GetDatasheetPath(IMasterDataEntry entry);

    /// <summary>
    /// Saves a module into the own master data, replacing an own entry with the same manufacturer and model, copies
    /// the datasheet (when given) into the user data folder and reloads the master data. Returns the loaded entry.
    /// </summary>
    /// <exception cref="IOException">The own master data or the datasheet cannot be written.</exception>
    PVModule SaveModule(PVModule module, string? datasheetSourcePath);

    /// <inheritdoc cref="SaveModule"/>
    Inverter SaveInverter(Inverter inverter, string? datasheetSourcePath);

    /// <inheritdoc cref="SaveModule"/>
    Battery SaveBattery(Battery battery, string? datasheetSourcePath);
}

public interface IDataPathProvider
{
    /// <summary>Path of a shipped master data file (read-only).</summary>
    string GetFilePath(string fileName);

    /// <summary>Folder of the own master data and the imported datasheets.</summary>
    string UserDirectory { get; }
}

public class DataPathProvider : IDataPathProvider
{
    private readonly string _dataDir;

    public DataPathProvider(IUserDataLocation userData)
    {
        // The shipped Data folder is next to the executable; the own master data is in the per-user data folder.
        _dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        UserDirectory = Path.Combine(userData.DirectoryPath, "Data");
    }

    public string UserDirectory { get; }

    public string GetFilePath(string fileName) => Path.Combine(_dataDir, fileName);
}

/// <summary>
/// Loads the shipped master data and the own master data from the user data folder. Every entry is read and validated
/// on its own, so one invalid entry does not discard the whole file; skipped entries and unreadable files are reported
/// in <see cref="Issues"/>. An own entry replaces a shipped entry with the same manufacturer and model. The shipped
/// files are never written.
/// </summary>
public class JsonDataStore : IDataStore
{
    public const string ModuleFileName = "Modules.json";
    public const string InverterFileName = "Inverters.json";
    public const string BatteryFileName = "Batteries.json";
    public const string DatasheetFolder = "Datasheets";

    private const string OwnSuffix = " (own data)";

    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<JsonDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDataPathProvider _paths;

    private List<PVModule> _modules = [];
    private List<Inverter> _inverters = [];
    private List<Battery> _batteries = [];
    private List<DataLoadIssue> _issues = [];

    public JsonDataStore(ILogger<JsonDataStore> logger, JsonSerializerOptions jsonOptions, IDataPathProvider paths)
    {
        _logger = logger;
        _jsonOptions = jsonOptions;
        _paths = paths;
    }

    public IReadOnlyList<PVModule> Modules => _modules;

    public IReadOnlyList<Inverter> Inverters => _inverters;

    public IReadOnlyList<Battery> Batteries => _batteries;

    public IReadOnlyList<DataLoadIssue> Issues => _issues;

    public CableTables? CableTables { get; private set; }

    public void Load()
    {
        var issues = new List<DataLoadIssue>();
        _modules = Merge(
            LoadEntries<PVModule>(_paths.GetFilePath(ModuleFileName), ModuleFileName, "Modules", ValidateModule, issues, own: false),
            LoadEntries<PVModule>(UserFilePath(ModuleFileName), ModuleFileName + OwnSuffix, "Modules", ValidateModule, issues, own: true));
        _inverters = Merge(
            LoadEntries<Inverter>(_paths.GetFilePath(InverterFileName), InverterFileName, "Inverters", ValidateInverter, issues, own: false),
            LoadEntries<Inverter>(UserFilePath(InverterFileName), InverterFileName + OwnSuffix, "Inverters", ValidateInverter, issues, own: true));
        _batteries = Merge(
            LoadEntries<Battery>(_paths.GetFilePath(BatteryFileName), BatteryFileName, "Batteries", ValidateBattery, issues, own: false),
            LoadEntries<Battery>(UserFilePath(BatteryFileName), BatteryFileName + OwnSuffix, "Batteries", ValidateBattery, issues, own: true));
        CableTables = CableTables.Load(_paths.GetFilePath(CableTables.FileName), out string? tableError);
        if (tableError is not null)
        {
            issues.Add(new DataLoadIssue(CableTables.FileName, null, $"Cable tables cannot be used ({tableError}); cables and fuses are not sized."));
        }

        _issues = issues;

        _logger.LogInformation("Master data loaded: {ModuleCount} PV modules, {InvCount} inverters, {BatteryCount} batteries ({OwnModules}, {OwnInverters} and {OwnBatteries} own).",
            _modules.Count, _inverters.Count, _batteries.Count, _modules.Count(m => m.IsUserEntry), _inverters.Count(i => i.IsUserEntry), _batteries.Count(b => b.IsUserEntry));
        foreach (DataLoadIssue issue in issues)
        {
            _logger.LogWarning("Master data: {Issue}", issue.ToString());
        }
    }

    public string? GetDatasheetPath(IMasterDataEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.IsUserEntry || string.IsNullOrWhiteSpace(entry.DatasheetFile))
        {
            return null;
        }

        // Only files inside the user data folder are opened.
        string root = Path.GetFullPath(_paths.UserDirectory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, entry.DatasheetFile));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
    }

    public PVModule SaveModule(PVModule module, string? datasheetSourcePath)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (ValidateModule(module) is { } error)
        {
            throw new ArgumentException($"The module is invalid: {error}", nameof(module));
        }

        Save(module, datasheetSourcePath, ModuleFileName, "Modules");
        return _modules.First(m => m.IsUserEntry && SameKey(m, module));
    }

    public Inverter SaveInverter(Inverter inverter, string? datasheetSourcePath)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        if (ValidateInverter(inverter) is { } error)
        {
            throw new ArgumentException($"The inverter is invalid: {error}", nameof(inverter));
        }

        Save(inverter, datasheetSourcePath, InverterFileName, "Inverters");
        return _inverters.First(i => i.IsUserEntry && SameKey(i, inverter));
    }

    public Battery SaveBattery(Battery battery, string? datasheetSourcePath)
    {
        ArgumentNullException.ThrowIfNull(battery);
        if (ValidateBattery(battery) is { } error)
        {
            throw new ArgumentException($"The battery is invalid: {error}", nameof(battery));
        }

        Save(battery, datasheetSourcePath, BatteryFileName, "Batteries");
        return _batteries.First(b => b.IsUserEntry && SameKey(b, battery));
    }

    /// <summary>File name of a stored datasheet: manufacturer and model without characters Windows does not allow.</summary>
    public static string DatasheetFileName(IMasterDataEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        char[] invalid = Path.GetInvalidFileNameChars();
        string name = new([.. $"{entry.Manufacturer} {entry.Model}".Select(c => invalid.Contains(c) ? '_' : c)]);
        return name.Trim().TrimEnd('.') + ".pdf";
    }

    private string UserFilePath(string fileName) => Path.Combine(_paths.UserDirectory, fileName);

    private static bool SameKey(IMasterDataEntry a, IMasterDataEntry b) =>
        string.Equals(a.Manufacturer, b.Manufacturer, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Model, b.Model, StringComparison.OrdinalIgnoreCase);

    // Shipped entries in their order; an own entry replaces the shipped one with the same key, the others follow.
    private static List<T> Merge<T>(List<T> shipped, List<T> own)
        where T : IMasterDataEntry
    {
        var result = new List<T>(shipped);
        foreach (T entry in own)
        {
            int index = result.FindIndex(s => SameKey(s, entry));
            if (index >= 0)
            {
                result[index] = entry;
            }
            else
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private void Save<T>(T entry, string? datasheetSourcePath, string fileName, string arrayProperty)
        where T : class, IMasterDataEntry
    {
        Directory.CreateDirectory(_paths.UserDirectory);
        string jsonPath = UserFilePath(fileName);

        // Read the own file first, so that nothing is copied when it cannot be updated.
        JsonObject root;
        if (File.Exists(jsonPath))
        {
            try
            {
                root = JsonNode.Parse(File.ReadAllText(jsonPath), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                    ?? throw new InvalidDataException("The file does not contain a JSON object.");
            }
            catch (Exception ex) when (ex is JsonException or InvalidDataException)
            {
                throw new IOException($"The own master data file {jsonPath} cannot be read ({ex.Message}). Correct or remove it first.", ex);
            }
        }
        else
        {
            root = [];
        }

        if (root[arrayProperty] is not JsonArray array)
        {
            array = [];
            root[arrayProperty] = array;
        }

        entry.DatasheetFile = datasheetSourcePath is null ? null : CopyDatasheet(entry, datasheetSourcePath);

        for (int i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is JsonObject existing
                && string.Equals((string?)existing["Manufacturer"], entry.Manufacturer, StringComparison.OrdinalIgnoreCase)
                && string.Equals((string?)existing["Model"], entry.Model, StringComparison.OrdinalIgnoreCase))
            {
                array.RemoveAt(i);
            }
        }

        array.Add(JsonSerializer.SerializeToNode(entry, s_writeOptions));
        WriteAtomically(jsonPath, root.ToJsonString(s_writeOptions));
        _logger.LogInformation("Own master data saved: {Manufacturer} {Model} in {Path}, datasheet {Datasheet}.",
            entry.Manufacturer, entry.Model, jsonPath, entry.DatasheetFile ?? "(none)");
        Load();
    }

    // Copies the datasheet into the user data folder and returns its path relative to that folder.
    private string CopyDatasheet(IMasterDataEntry entry, string sourcePath)
    {
        string folder = Path.Combine(_paths.UserDirectory, DatasheetFolder);
        Directory.CreateDirectory(folder);
        string name = DatasheetFileName(entry);
        string target = Path.Combine(folder, name);
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            string temp = target + ".tmp";
            File.Copy(sourcePath, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }

        return DatasheetFolder + "/" + name;
    }

    private static void WriteAtomically(string path, string content)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, path, overwrite: true);
    }

    private List<T> LoadEntries<T>(
        string path,
        string fileName,
        string arrayProperty,
        Func<T, string?> validate,
        List<DataLoadIssue> issues,
        bool own)
        where T : class, IMasterDataEntry
    {
        var result = new List<T>();
        if (!File.Exists(path))
        {
            // The own master data is optional.
            if (!own)
            {
                issues.Add(new DataLoadIssue(fileName, null, "File not found."));
            }

            return result;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error reading {Path}.", path);
            issues.Add(new DataLoadIssue(fileName, null, $"File cannot be read ({ex.Message})."));
            return result;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(arrayProperty, out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new DataLoadIssue(fileName, null, $"List \"{arrayProperty}\" is missing."));
                return result;
            }

            var seen = new HashSet<(string, string)>();
            int index = 0;
            foreach (JsonElement element in array.EnumerateArray())
            {
                index++;
                string entry = DescribeEntry(element, index);
                T? item;
                try
                {
                    item = element.Deserialize<T>(_jsonOptions);
                }
                catch (JsonException ex)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, $"Entry skipped: invalid value ({ex.Path?.TrimStart('$', '.')})."));
                    continue;
                }

                if (item is null)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Entry skipped: not an object."));
                    continue;
                }

                if (validate(item) is { } error)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, $"Entry skipped: {error}"));
                    continue;
                }

                if (!seen.Add((item.Manufacturer.ToUpperInvariant(), item.Model.ToUpperInvariant())))
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Entry skipped: duplicate manufacturer and model."));
                    continue;
                }

                item.IsUserEntry = own;
                if (!own)
                {
                    // Datasheets belong to the own master data only.
                    item.DatasheetFile = null;
                }

                result.Add(item);
            }
        }

        return result;
    }

    private static string DescribeEntry(JsonElement element, int index)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("Manufacturer", out JsonElement manufacturer) && manufacturer.ValueKind == JsonValueKind.String
            && element.TryGetProperty("Model", out JsonElement model) && model.ValueKind == JsonValueKind.String)
        {
            return $"Entry {index} ({manufacturer.GetString()} {model.GetString()})";
        }

        return $"Entry {index}";
    }

    // Returns the reason why a module cannot be used, or null when it is valid.
    internal static string? ValidateModule(PVModule m)
    {
        if (string.IsNullOrWhiteSpace(m.Manufacturer) || string.IsNullOrWhiteSpace(m.Model))
        {
            return "manufacturer or model is missing.";
        }

        if (m.PmaxW <= 0 || m.VmppV <= 0 || m.ImppA <= 0
            || m.VocV <= 0 || m.IscA <= 0)
        {
            return "Pmax, Vmpp, Impp, Voc and Isc must be greater than 0.";
        }

        if (m.VocV <= m.VmppV || m.IscA < m.ImppA)
        {
            return "Voc must exceed Vmpp, and Isc must be at least Impp.";
        }

        if (m.CableCrossSectionMm2 is <= 0 || m.CableLengthPlusMm is <= 0 || m.CableLengthMinusMm is <= 0)
        {
            return "the cable cross-section and lengths must be greater than 0 when they are given.";
        }

        return null;
    }

    // Returns the reason why an inverter cannot be used, or null when it is valid.
    internal static string? ValidateInverter(Inverter w)
    {
        if (string.IsNullOrWhiteSpace(w.Manufacturer) || string.IsNullOrWhiteSpace(w.Model))
        {
            return "manufacturer or model is missing.";
        }

        if (!StringCalculator.TryParseVoltageRange(w.MpptVoltageRangeV, out _, out _))
        {
            return $"MPPT voltage range \"{w.MpptVoltageRangeV}\" is invalid (expected format: \"160-650\").";
        }

        if (w.MaxDcInputPowerW <= 0 || w.MaxDcInputVoltageV <= 0 || w.MaxInputCurrentA <= 0
            || w.MaxShortCircuitCurrentA <= 0 || w.MpptCount <= 0)
        {
            return "power, voltage, currents and MPPT count must be greater than 0.";
        }

        if (w.RatedAcPowerW < 0 || w.MaxAcApparentPowerVA < 0 || w.RatedAcCurrentA < 0 || w.MaxAcCurrentA < 0)
        {
            return "AC power and currents must not be negative.";
        }

        if (w.AcPhases is not (0 or 1 or 3))
        {
            return "AC phases must be 1 or 3.";
        }

        if (w.RatedAcCurrentA > 0 && w.MaxAcCurrentA > 0 && w.MaxAcCurrentA < w.RatedAcCurrentA)
        {
            return "the max. AC current must be at least the rated AC current.";
        }

        if (w.MaxBatteryChargeCurrentA < 0 || w.MaxBatteryDischargeCurrentA < 0 || w.BatteryInputs < 0)
        {
            return "battery currents and inputs must not be negative.";
        }

        if (w.HasBatteryInput && !StringCalculator.TryParseVoltageRange(w.BatteryVoltageRangeV!, out _, out _))
        {
            return $"battery voltage range \"{w.BatteryVoltageRangeV}\" is invalid (expected format: \"40-60\").";
        }

        return null;
    }

    // Returns the reason why a battery cannot be used, or null when it is valid.
    internal static string? ValidateBattery(Battery b)
    {
        if (string.IsNullOrWhiteSpace(b.Manufacturer) || string.IsNullOrWhiteSpace(b.Model))
        {
            return "manufacturer or model is missing.";
        }

        if (!StringCalculator.TryParseVoltageRange(b.VoltageRangeV, out double min, out double max))
        {
            return $"operating voltage range \"{b.VoltageRangeV}\" is invalid (expected format: \"44.8-57.6\").";
        }

        if (b.NominalVoltageV <= 0 || b.NominalEnergyKWh <= 0 || b.MaxChargeCurrentA <= 0 || b.MaxDischargeCurrentA <= 0)
        {
            return "nominal voltage, nominal energy and the max. charge and discharge current must be greater than 0.";
        }

        if (b.NominalCapacityAh is <= 0)
        {
            return "the nominal capacity must be greater than 0 when it is given.";
        }

        if (b.DepthOfDischargePct is { } dod && (dod <= 0 || dod > 100))
        {
            return "the depth of discharge must be above 0 and at most 100 %.";
        }

        if (b.NominalVoltageV < min || b.NominalVoltageV > max)
        {
            return "the nominal voltage must lie within the operating voltage range.";
        }

        if (b.UsableEnergyKWh is { } usable && (usable <= 0 || usable > b.NominalEnergyKWh))
        {
            return "the usable energy must be greater than 0 and at most the nominal energy.";
        }

        if (b.MaxModulesInSeries < 0 || b.MaxModulesInParallel < 0)
        {
            return "the max. module counts must not be negative.";
        }

        return null;
    }
}
