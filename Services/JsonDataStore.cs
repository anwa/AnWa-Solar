using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AnWaSolar.Models;

namespace AnWaSolar.Services;

public interface IDataStore
{
    IReadOnlyList<PVModule> Module { get; }
    IReadOnlyList<Wechselrichter> Wechselrichter { get; }
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
        // Data-Ordner liegt neben der .exe im Ausgabeverzeichnis
        _dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(_dataDir);
    }

    public string GetFilePath(string fileName) => Path.Combine(_dataDir, fileName);
}

public class JsonDataStore : IDataStore
{
    private readonly ILogger<JsonDataStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IDataPathProvider _paths;

    private List<PVModule> _module = new();
    private List<Wechselrichter> _wechselrichter = new();

    public JsonDataStore(ILogger<JsonDataStore> logger, JsonSerializerOptions jsonOptions, IDataPathProvider paths)
    {
        _logger = logger;
        _jsonOptions = jsonOptions;
        _paths = paths;
    }

    public IReadOnlyList<PVModule> Module => _module;
    public IReadOnlyList<Wechselrichter> Wechselrichter => _wechselrichter;

    public void Load()
    {
        LoadModules();
        LoadWechselrichter();
        _logger.LogInformation("Daten geladen: {ModuleCount} PV-Module, {InvCount} Wechselrichter.",
            _module.Count, _wechselrichter.Count);
    }

    private void LoadModules()
    {
        var path = _paths.GetFilePath("PV-Module.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Datei {Path} wurde nicht gefunden. Es werden keine PV-Module geladen.", path);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var wrapper = JsonSerializer.Deserialize<PVModuleList>(json, _jsonOptions);
            _module = wrapper?.Module ?? new List<PVModule>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der PV-Module aus {Path}.", path);
        }
    }

    private void LoadWechselrichter()
    {
        var path = _paths.GetFilePath("Wechselrichter.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Datei {Path} wurde nicht gefunden. Es werden keine Wechselrichter geladen.", path);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var wrapper = JsonSerializer.Deserialize<WechselrichterList>(json, _jsonOptions);
            _wechselrichter = wrapper?.Wechselrichter ?? new List<Wechselrichter>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Wechselrichter aus {Path}.", path);
        }
    }
}
