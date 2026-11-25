using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;

using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Services;

/// <summary>A master data file or entry that could not be used.</summary>
/// <param name="FileName">Name of the master data file, for a series file its path below the data folder.</param>
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
    /// Saves a series into the own master data (<c>Data\&lt;Kind&gt;\&lt;Manufacturer&gt;\&lt;Series&gt;.json</c>), replacing an
    /// own series with the same manufacturer and name; its models are removed from other own series. The datasheet
    /// (when given) is copied next to the series file. The master data is reloaded; returns the loaded variants.
    /// </summary>
    /// <exception cref="ArgumentException">The series or one of its variants is invalid.</exception>
    /// <exception cref="IOException">The own master data or the datasheet cannot be written.</exception>
    /// <param name="replaces">Manufacturer and name of the own series this one replaces when it was renamed; its file is removed.</param>
    IReadOnlyList<T> SaveSeries<T>(ComponentSeries<T> series, string? datasheetSourcePath, (string Manufacturer, string Series)? replaces = null)
        where T : class, IMasterDataEntry;

    /// <summary>Deletes an own series with its datasheet and reloads the master data; false when there is none.</summary>
    bool DeleteSeries<T>(string manufacturer, string series)
        where T : class, IMasterDataEntry;
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
/// Loads the shipped master data and the own master data from the user data folder. The master data is organised in
/// series: one JSON file per series in <c>Data\Modules</c>, <c>Data\Inverters</c> and <c>Data\Batteries</c>, below a
/// folder per manufacturer; each variant (power class) is one entry. Single-list files of earlier versions
/// (<c>Modules.json</c> and so on) are still read; the own ones are converted into series files once. Every entry is
/// read and validated on its own, so one invalid entry does not discard a file; skipped entries and unreadable files
/// are reported in <see cref="Issues"/>. An own series replaces a shipped series with the same manufacturer and name,
/// and an own entry a shipped entry with the same manufacturer and model. The shipped files are never written.
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

    private static readonly JsonDocumentOptions s_documentOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

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

    // A kind of master data: its folder of series files, its single-list file of earlier versions and its validation.
    private sealed record Kind<T>(string Folder, string LegacyFile, string ArrayProperty, Func<T, string?> Validate)
        where T : class, IMasterDataEntry;

    private static readonly Kind<PVModule> s_modules = new("Modules", ModuleFileName, "Modules", ValidateModule);
    private static readonly Kind<Inverter> s_inverters = new("Inverters", InverterFileName, "Inverters", ValidateInverter);
    private static readonly Kind<Battery> s_batteries = new("Batteries", BatteryFileName, "Batteries", ValidateBattery);

    private static Kind<T> KindOf<T>()
        where T : class, IMasterDataEntry =>
        (typeof(T) == typeof(PVModule) ? s_modules as Kind<T>
            : typeof(T) == typeof(Inverter) ? s_inverters as Kind<T>
            : s_batteries as Kind<T>) ?? throw new NotSupportedException($"No master data kind for {typeof(T).Name}.");

    public void Load()
    {
        var issues = new List<DataLoadIssue>();
        _modules = LoadKind(s_modules, issues);
        _inverters = LoadKind(s_inverters, issues);
        _batteries = LoadKind(s_batteries, issues);
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

    public IReadOnlyList<T> SaveSeries<T>(ComponentSeries<T> series, string? datasheetSourcePath, (string Manufacturer, string Series)? replaces = null)
        where T : class, IMasterDataEntry
    {
        ArgumentNullException.ThrowIfNull(series);
        Kind<T> kind = KindOf<T>();
        series.Manufacturer = series.Manufacturer.Trim();
        series.Series = series.Series.Trim();
        if (series.Manufacturer.Length == 0 || series.Series.Length == 0)
        {
            throw new ArgumentException("Manufacturer and series are required.", nameof(series));
        }

        if (series.Variants.Count == 0)
        {
            throw new ArgumentException("The series has no power class.", nameof(series));
        }

        var errors = new List<string>();
        foreach (T variant in series.Variants)
        {
            variant.Manufacturer = series.Manufacturer;
            if (kind.Validate(variant) is { } error)
            {
                errors.Add($"{variant.Model}: {error}");
            }
        }

        if (series.Variants.GroupBy(v => v.Model.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
        {
            errors.Add($"The model {duplicate.Key} appears twice.");
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException("The series is invalid: " + string.Join(" ", errors), nameof(series));
        }

        string folder = Path.Combine(_paths.UserDirectory, kind.Folder, FileNameOf(series.Manufacturer));
        string path = Path.Combine(folder, FileNameOf(series.Series) + ".json");

        // Read an existing file first, so that nothing is changed when it cannot be replaced.
        string? previousDatasheet = null;
        if (File.Exists(path))
        {
            try
            {
                using JsonDocument existing = JsonDocument.Parse(File.ReadAllText(path), s_documentOptions);
                previousDatasheet = existing.RootElement.TryGetProperty("Datasheet file", out JsonElement sheet) && sheet.ValueKind == JsonValueKind.String ? sheet.GetString() : null;
            }
            catch (JsonException ex)
            {
                throw new IOException($"The own series file {path} cannot be read ({ex.Message}). Correct or remove it first.", ex);
            }
        }

        // The datasheet is copied first: it may be the datasheet of a series that is removed below (renamed series).
        Directory.CreateDirectory(folder);
        series.DatasheetFile = datasheetSourcePath is null ? previousDatasheet : CopyDatasheet(datasheetSourcePath, Path.Combine(folder, FileNameOf(series.Series) + ".pdf"));
        RemoveModelsFromOtherSeries(kind, series, path);
        if (replaces is { } old && OwnSeriesPath(kind, old.Manufacturer, old.Series) is var oldPath
            && !string.Equals(Path.GetFullPath(oldPath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            DeleteSeriesFile(oldPath, keep: Path.Combine(folder, series.DatasheetFile ?? string.Empty));
        }

        WriteSeries(path, series);
        _logger.LogInformation("Own series saved: {Manufacturer} {Series} with {Count} variants in {Path}, datasheet {Datasheet}.",
            series.Manufacturer, series.Series, series.Variants.Count, path, series.DatasheetFile ?? "(none)");
        Load();

        HashSet<string> models = new(series.Variants.Select(v => v.Model), StringComparer.OrdinalIgnoreCase);
        return [.. Entries(kind).Where(e => e.IsUserEntry && SameManufacturer(e.Manufacturer, series.Manufacturer) && models.Contains(e.Model))];
    }

    public bool DeleteSeries<T>(string manufacturer, string series)
        where T : class, IMasterDataEntry
    {
        string path = OwnSeriesPath(KindOf<T>(), manufacturer, series);
        if (!File.Exists(path))
        {
            return false;
        }

        DeleteSeriesFile(path, keep: null);
        _logger.LogInformation("Own series {Manufacturer} {Series} deleted.", manufacturer, series);
        Load();
        return true;
    }

    private string OwnSeriesPath<T>(Kind<T> kind, string manufacturer, string series)
        where T : class, IMasterDataEntry =>
        Path.Combine(_paths.UserDirectory, kind.Folder, FileNameOf(manufacturer.Trim()), FileNameOf(series.Trim()) + ".json");

    // Deletes a series file and its datasheet (unless it is the file to keep); an empty manufacturer folder goes too.
    private static void DeleteSeriesFile(string path, string? keep)
    {
        if (!File.Exists(path))
        {
            return;
        }

        string? sheet = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path), s_documentOptions);
            sheet = document.RootElement.TryGetProperty("Datasheet file", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        }
        catch (JsonException)
        {
            // An unreadable file is deleted without its datasheet.
        }

        File.Delete(path);
        if (sheet is not null && Path.Combine(Path.GetDirectoryName(path)!, sheet) is var pdf && File.Exists(pdf)
            && (keep is null || !string.Equals(Path.GetFullPath(pdf), Path.GetFullPath(keep), StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(pdf);
        }

        string folder = Path.GetDirectoryName(path)!;
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
        }
    }

    /// <summary>File name of a stored datasheet: manufacturer and model without characters Windows does not allow.</summary>
    public static string DatasheetFileName(IMasterDataEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return FileNameOf($"{entry.Manufacturer} {entry.Model}") + ".pdf";
    }

    /// <summary>A file or folder name without characters Windows does not allow.</summary>
    public static string FileNameOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().TrimEnd('.');
        return clean.Length == 0 ? "_" : clean;
    }

    private IReadOnlyList<T> Entries<T>(Kind<T> kind)
        where T : class, IMasterDataEntry =>
        (IReadOnlyList<T>)(object)(kind.Folder switch
        {
            "Modules" => _modules,
            "Inverters" => _inverters,
            _ => (object)_batteries,
        });

    private static bool SameManufacturer(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SameKey(IMasterDataEntry a, IMasterDataEntry b) =>
        SameManufacturer(a.Manufacturer, b.Manufacturer) && string.Equals(a.Model, b.Model, StringComparison.OrdinalIgnoreCase);

    private List<T> LoadKind<T>(Kind<T> kind, List<DataLoadIssue> issues)
        where T : class, IMasterDataEntry
    {
        // Shipped: the series folder and, from earlier versions, the single-list file.
        string shippedFolder = _paths.GetFilePath(kind.Folder);
        string shippedLegacy = _paths.GetFilePath(kind.LegacyFile);
        var shipped = new List<T>();
        if (!Directory.Exists(shippedFolder) && !File.Exists(shippedLegacy))
        {
            issues.Add(new DataLoadIssue(kind.LegacyFile, null, "File not found."));
        }

        var seen = new HashSet<(string, string)>();
        shipped.AddRange(LoadEntries(kind, shippedLegacy, kind.LegacyFile, issues, own: false, seen));
        shipped.AddRange(LoadSeriesFolder(kind, shippedFolder, issues, own: false, seen));

        // Own: single-list files of earlier versions become series files.
        var ownSeen = new HashSet<(string, string)>();
        List<T> ownLegacy = MigrateOwnLegacy(kind, issues, ownSeen);
        List<T> own = [.. ownLegacy, .. LoadSeriesFolder(kind, Path.Combine(_paths.UserDirectory, kind.Folder), issues, own: true, ownSeen)];

        // An own series replaces the shipped series of the same name; an own entry the shipped entry of the same model.
        HashSet<(string, string)> ownSeries = [.. own.Select(e => (e.Manufacturer.ToUpperInvariant(), e.Series.ToUpperInvariant()))];
        var result = shipped.Where(s => !ownSeries.Contains((s.Manufacturer.ToUpperInvariant(), s.Series.ToUpperInvariant()))).ToList();
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

    // Converts an own single-list file of an earlier version into series files (one series per datasheet, or per
    // entry without datasheet) and keeps the old file as *.migrated. Returns the entries when the conversion failed.
    private List<T> MigrateOwnLegacy<T>(Kind<T> kind, List<DataLoadIssue> issues, HashSet<(string, string)> seen)
        where T : class, IMasterDataEntry
    {
        string legacy = Path.Combine(_paths.UserDirectory, kind.LegacyFile);
        if (!File.Exists(legacy))
        {
            return [];
        }

        var legacyIssues = new List<DataLoadIssue>();
        List<T> entries = LoadEntries(kind, legacy, kind.LegacyFile + OwnSuffix, legacyIssues, own: true, new HashSet<(string, string)>(), keepDatasheet: true);
        issues.AddRange(legacyIssues);
        if (legacyIssues.Any(i => i.Entry is null))
        {
            // The file cannot be read as a whole; it stays for the user to correct.
            return [.. entries.Where(e => seen.Add(Key(e)))];
        }

        try
        {
            foreach (IGrouping<string, T> group in entries.GroupBy(e => e.DatasheetFile ?? $"\u0000{e.Manufacturer}\u0000{e.Model}", StringComparer.OrdinalIgnoreCase))
            {
                T first = group.First();
                var series = new ComponentSeries<T> { Manufacturer = first.Manufacturer, Series = first.Model, Variants = [.. group] };
                string folder = Path.Combine(_paths.UserDirectory, kind.Folder, FileNameOf(series.Manufacturer));
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, FileNameOf(series.Series) + ".json");
                if (first.DatasheetFile is { } sheet && File.Exists(Path.Combine(_paths.UserDirectory, sheet)))
                {
                    series.DatasheetFile = CopyDatasheet(Path.Combine(_paths.UserDirectory, sheet), Path.Combine(folder, FileNameOf(series.Series) + ".pdf"));
                }

                if (!File.Exists(target))
                {
                    WriteSeries(target, series);
                }
            }

            File.Move(legacy, legacy + ".migrated", overwrite: true);
            _logger.LogInformation("Own master data {File} converted into series files.", kind.LegacyFile);
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Own master data {File} could not be converted into series files.", kind.LegacyFile);
            issues.Add(new DataLoadIssue(kind.LegacyFile + OwnSuffix, null, $"Could not be converted into series files ({ex.Message}); the entries are used as they are."));
            return [.. entries.Where(e => seen.Add(Key(e)))];
        }
    }

    // Removes the models of a saved series from the other own series of the manufacturer, so that a model belongs
    // to one series; a series without models is deleted with its datasheet.
    private void RemoveModelsFromOtherSeries<T>(Kind<T> kind, ComponentSeries<T> saved, string savedPath)
        where T : class, IMasterDataEntry
    {
        string folder = Path.Combine(_paths.UserDirectory, kind.Folder);
        if (!Directory.Exists(folder))
        {
            return;
        }

        HashSet<string> models = new(saved.Variants.Select(v => v.Model.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(savedPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(File.ReadAllText(file), documentOptions: s_documentOptions) as JsonObject;
            }
            catch (JsonException)
            {
                continue;
            }

            if (root is null || !SameManufacturer((string?)root["Manufacturer"] ?? string.Empty, saved.Manufacturer) || root["Variants"] is not JsonArray variants)
            {
                continue;
            }

            int before = variants.Count;
            for (int i = variants.Count - 1; i >= 0; i--)
            {
                if (variants[i] is JsonObject v && (string?)v["Model"] is { } model && models.Contains(model.Trim()))
                {
                    variants.RemoveAt(i);
                }
            }

            if (variants.Count == before)
            {
                continue;
            }

            if (variants.Count == 0)
            {
                File.Delete(file);
                if ((string?)root["Datasheet file"] is { } sheet && Path.Combine(Path.GetDirectoryName(file)!, sheet) is var pdf && File.Exists(pdf))
                {
                    File.Delete(pdf);
                }

                _logger.LogInformation("Own series {Path} removed; its models moved to {Series}.", file, saved.Series);
            }
            else
            {
                WriteAtomically(file, root.ToJsonString(s_writeOptions));
            }
        }
    }

    private static void WriteSeries<T>(string path, ComponentSeries<T> series)
        where T : class, IMasterDataEntry
    {
        var variants = new JsonArray();
        foreach (T variant in series.Variants)
        {
            // Manufacturer and datasheet belong to the series.
            JsonObject node = JsonSerializer.SerializeToNode(variant, s_writeOptions)!.AsObject();
            node.Remove("Manufacturer");
            node.Remove("Datasheet file");
            variants.Add(node);
        }

        var root = new JsonObject
        {
            ["Manufacturer"] = series.Manufacturer,
            ["Series"] = series.Series,
        };
        if (series.DatasheetFile is not null)
        {
            root["Datasheet file"] = series.DatasheetFile;
        }

        root["Variants"] = variants;
        WriteAtomically(path, root.ToJsonString(s_writeOptions));
    }

    // Copies a datasheet to the target (unless it is the target) and returns its file name.
    private static string CopyDatasheet(string sourcePath, string target)
    {
        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            string temp = target + ".tmp";
            File.Copy(sourcePath, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }

        return Path.GetFileName(target);
    }

    private static void WriteAtomically(string path, string content)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, path, overwrite: true);
    }

    private static (string, string) Key(IMasterDataEntry e) => (e.Manufacturer.ToUpperInvariant(), e.Model.ToUpperInvariant());

    // Series files below a folder; each variant is validated on its own.
    private List<T> LoadSeriesFolder<T>(Kind<T> kind, string folder, List<DataLoadIssue> issues, bool own, HashSet<(string, string)> seen)
        where T : class, IMasterDataEntry
    {
        var result = new List<T>();
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (string file in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.Combine(kind.Folder, Path.GetRelativePath(folder, file)).Replace('\\', '/') + (own ? OwnSuffix : string.Empty);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(file), s_documentOptions);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Error reading {Path}.", file);
                issues.Add(new DataLoadIssue(name, null, $"File cannot be read ({ex.Message})."));
                continue;
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                string? manufacturer = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Manufacturer", out JsonElement m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                string? seriesName = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Series", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                if (string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(seriesName)
                    || !root.TryGetProperty("Variants", out JsonElement variants) || variants.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new DataLoadIssue(name, null, "Manufacturer, series or the list \"Variants\" is missing."));
                    continue;
                }

                string? datasheet = null;
                if (own && root.TryGetProperty("Datasheet file", out JsonElement sheet) && sheet.ValueKind == JsonValueKind.String)
                {
                    datasheet = Path.GetRelativePath(_paths.UserDirectory, Path.Combine(Path.GetDirectoryName(file)!, sheet.GetString()!)).Replace('\\', '/');
                }

                int index = 0;
                foreach (JsonElement element in variants.EnumerateArray())
                {
                    index++;
                    string entry = DescribeEntry(element, index, manufacturer);
                    T? item;
                    try
                    {
                        item = element.Deserialize<T>(_jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        issues.Add(new DataLoadIssue(name, entry, $"Entry skipped: invalid value ({ex.Path?.TrimStart('$', '.')})."));
                        continue;
                    }

                    if (item is null)
                    {
                        issues.Add(new DataLoadIssue(name, entry, "Entry skipped: not an object."));
                        continue;
                    }

                    item.Manufacturer = manufacturer;
                    item.Series = seriesName;
                    item.DatasheetFile = datasheet;
                    item.IsUserEntry = own;
                    if (kind.Validate(item) is { } error)
                    {
                        issues.Add(new DataLoadIssue(name, entry, $"Entry skipped: {error}"));
                        continue;
                    }

                    if (!seen.Add(Key(item)))
                    {
                        issues.Add(new DataLoadIssue(name, entry, "Entry skipped: duplicate manufacturer and model."));
                        continue;
                    }

                    result.Add(item);
                }
            }
        }

        return result;
    }

    // A single-list file of an earlier version; its entries form a series of their own each.
    private List<T> LoadEntries<T>(Kind<T> kind, string path, string fileName, List<DataLoadIssue> issues, bool own, HashSet<(string, string)> seen, bool keepDatasheet = false)
        where T : class, IMasterDataEntry
    {
        var result = new List<T>();
        if (!File.Exists(path))
        {
            return result;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), s_documentOptions);
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
                || !document.RootElement.TryGetProperty(kind.ArrayProperty, out JsonElement array)
                || array.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new DataLoadIssue(fileName, null, $"List \"{kind.ArrayProperty}\" is missing."));
                return result;
            }

            int index = 0;
            foreach (JsonElement element in array.EnumerateArray())
            {
                index++;
                string entry = DescribeEntry(element, index, null);
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

                if (kind.Validate(item) is { } error)
                {
                    issues.Add(new DataLoadIssue(fileName, entry, $"Entry skipped: {error}"));
                    continue;
                }

                if (!seen.Add(Key(item)))
                {
                    issues.Add(new DataLoadIssue(fileName, entry, "Entry skipped: duplicate manufacturer and model."));
                    continue;
                }

                item.IsUserEntry = own;
                item.Series = item.Model;
                if (!own || !keepDatasheet)
                {
                    // Datasheets of single-list files are only kept for the conversion into series files.
                    item.DatasheetFile = own ? item.DatasheetFile : null;
                }

                result.Add(item);
            }
        }

        return result;
    }

    private static string DescribeEntry(JsonElement element, int index, string? manufacturer)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("Model", out JsonElement model) && model.ValueKind == JsonValueKind.String)
        {
            string? maker = manufacturer ?? (element.TryGetProperty("Manufacturer", out JsonElement m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null);
            return maker is null ? $"Entry {index} ({model.GetString()})" : $"Entry {index} ({maker} {model.GetString()})";
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
