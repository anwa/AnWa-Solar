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
    IReadOnlyList<PVModule> Module { get; }

    IReadOnlyList<Wechselrichter> Wechselrichter { get; }

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
    public const string ModuleFileName = "PV-Module.json";
    public const string InverterFileName = "Wechselrichter.json";

    private readonly ILogger<JsonDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDataPathProvider _paths;

    private List<PVModule> _module = [];
    private List<Wechselrichter> _wechselrichter = [];
    private List<DataLoadIssue> _issues = [];

    public JsonDataStore(ILogger<JsonDataStore> logger, JsonSerializerOptions jsonOptions, IDataPathProvider paths)
    {
        _logger = logger;
        _jsonOptions = jsonOptions;
        _paths = paths;
    }

    public IReadOnlyList<PVModule> Module => _module;

    public IReadOnlyList<Wechselrichter> Wechselrichter => _wechselrichter;

    public IReadOnlyList<DataLoadIssue> Issues => _issues;

    public void Load()
    {
        var issues = new List<DataLoadIssue>();
        _module = LoadEntries<PVModule>(ModuleFileName, "Module", ValidateModule, m => (m.Hersteller, m.Model), issues);
        _wechselrichter = LoadEntries<Wechselrichter>(InverterFileName, "Wechselrichter", ValidateInverter, w => (w.Hersteller, w.Model), issues);
        _issues = issues;

        _logger.LogInformation("Daten geladen: {ModuleCount} PV-Module, {InvCount} Wechselrichter.",
            _module.Count, _wechselrichter.Count);
        foreach (DataLoadIssue issue in issues)
        {
            _logger.LogWarning("Stammdaten: {Issue}", issue.ToString());
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
            issues.Add(new DataLoadIssue(fileName, null, "Datei nicht gefunden."));
            return result;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Fehler beim Lesen von {Path}.", path);
            issues.Add(new DataLoadIssue(fileName, null, $"Datei kann nicht gelesen werden ({ex.Message})."));
            return result;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(arrayProperty, out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new DataLoadIssue(fileName, null, $"Liste \"{arrayProperty}\" fehlt."));
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
                    issues.Add(new DataLoadIssue(fileName, entry, $"Eintrag übersprungen, ungültiger Wert ({ex.Path?.TrimStart('$', '.')})."));
                    continue;
                }

                if (item is null)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Eintrag übersprungen, kein Objekt."));
                    continue;
                }

                if (validate(item) is { } error)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, $"Eintrag übersprungen, {error}"));
                    continue;
                }

                (string manufacturer, string model) = key(item);
                if (!seen.Add((manufacturer.ToUpperInvariant(), model.ToUpperInvariant())))
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Eintrag übersprungen, Hersteller und Modell sind doppelt."));
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
            && element.TryGetProperty("Hersteller", out JsonElement manufacturer) && manufacturer.ValueKind == JsonValueKind.String
            && element.TryGetProperty("Model", out JsonElement model) && model.ValueKind == JsonValueKind.String)
        {
            return $"Eintrag {index} ({manufacturer.GetString()} {model.GetString()})";
        }

        return $"Eintrag {index}";
    }

    // Returns the reason why a module cannot be used, or null when it is valid.
    internal static string? ValidateModule(PVModule m)
    {
        if (string.IsNullOrWhiteSpace(m.Hersteller) || string.IsNullOrWhiteSpace(m.Model))
        {
            return "Hersteller oder Modell fehlt.";
        }

        if (m.NominalleistungPmaxWp <= 0 || m.SpannungImMppUmppV <= 0 || m.StromImMppImppA <= 0
            || m.LeerlaufspannungUocV <= 0 || m.KurzschlusstromIscA <= 0)
        {
            return "PMAX, UMPP, IMPP, UOC und ISC müssen größer als 0 sein.";
        }

        if (m.LeerlaufspannungUocV <= m.SpannungImMppUmppV || m.KurzschlusstromIscA < m.StromImMppImppA)
        {
            return "UOC muss größer als UMPP und ISC mindestens IMPP sein.";
        }

        return null;
    }

    // Returns the reason why an inverter cannot be used, or null when it is valid.
    internal static string? ValidateInverter(Wechselrichter w)
    {
        if (string.IsNullOrWhiteSpace(w.Hersteller) || string.IsNullOrWhiteSpace(w.Model))
        {
            return "Hersteller oder Modell fehlt.";
        }

        if (!StringCalculator.TryParseVoltageRange(w.MpptSpannungsbereichV, out _, out _))
        {
            return $"MPPT-Spannungsbereich \"{w.MpptSpannungsbereichV}\" ist ungültig (Format z. B. \"160-650\").";
        }

        if (w.MaxDcEingangsleistungW <= 0 || w.MaxDcEingangsspannungV <= 0 || w.MaxBetriebsPvEingangsstromA <= 0
            || w.MaxEingangsKurzschlussstromA <= 0 || w.AnzahlDerMpptTrackers <= 0)
        {
            return "Leistung, Spannung, Ströme und Anzahl der MPP-Tracker müssen größer als 0 sein.";
        }

        return null;
    }
}
