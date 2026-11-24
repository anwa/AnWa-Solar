using System.Text.Json;

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

    /// <summary>Problems of the last <see cref="Load"/>; skipped entries are listed individually.</summary>
    IReadOnlyList<DataLoadIssue> Issues { get; }

    void Load();
}

public interface IDataPathProvider
{
    string GetFilePath(string fileName);
}

public class DataPathProvider : IDataPathProvider
{
    private readonly string _dataDir;

    public DataPathProvider()
    {
        // The Data folder is shipped next to the executable.
        _dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
    }

    public string GetFilePath(string fileName) => Path.Combine(_dataDir, fileName);
}

/// <summary>
/// Loads the master data files. Every entry is read and validated on its own, so one invalid entry does not discard
/// the whole file; skipped entries and unreadable files are reported in <see cref="Issues"/>.
/// </summary>
public class JsonDataStore : IDataStore
{
    public const string ModuleFileName = "Modules.json";
    public const string InverterFileName = "Inverters.json";

    private readonly ILogger<JsonDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDataPathProvider _paths;

    private List<PVModule> _modules = [];
    private List<Inverter> _inverters = [];
    private List<DataLoadIssue> _issues = [];

    public JsonDataStore(ILogger<JsonDataStore> logger, JsonSerializerOptions jsonOptions, IDataPathProvider paths)
    {
        _logger = logger;
        _jsonOptions = jsonOptions;
        _paths = paths;
    }

    public IReadOnlyList<PVModule> Modules => _modules;

    public IReadOnlyList<Inverter> Inverters => _inverters;

    public IReadOnlyList<DataLoadIssue> Issues => _issues;

    public void Load()
    {
        var issues = new List<DataLoadIssue>();
        _modules = LoadEntries<PVModule>(ModuleFileName, "Modules", ValidateModule, m => (m.Manufacturer, m.Model), issues);
        _inverters = LoadEntries<Inverter>(InverterFileName, "Inverters", ValidateInverter, w => (w.Manufacturer, w.Model), issues);
        _issues = issues;

        _logger.LogInformation("Master data loaded: {ModuleCount} PV modules, {InvCount} inverters.",
            _modules.Count, _inverters.Count);
        foreach (DataLoadIssue issue in issues)
        {
            _logger.LogWarning("Master data: {Issue}", issue.ToString());
        }
    }

    private List<T> LoadEntries<T>(
        string fileName,
        string arrayProperty,
        Func<T, string?> validate,
        Func<T, (string Manufacturer, string Model)> key,
        List<DataLoadIssue> issues)
        where T : class
    {
        var result = new List<T>();
        string path = _paths.GetFilePath(fileName);
        if (!File.Exists(path))
        {
            issues.Add(new DataLoadIssue(fileName, null, "File not found."));
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

                (string manufacturer, string model) = key(item);
                if (!seen.Add((manufacturer.ToUpperInvariant(), model.ToUpperInvariant())))
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Entry skipped: duplicate manufacturer and model."));
                    continue;
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

        return null;
    }
}
