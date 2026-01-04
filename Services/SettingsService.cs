using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AnWaSolar;

#region Schnittstellen und Modelle

public class CalculationParameters
{
    public double MinTempC { get; set; } = -20.0;
    public double MaxTempC { get; set; } = 40.0;
    public double SicherheitsmargePct { get; set; } = 10.0;
}

public class SelectedRef
{
    public string Hersteller { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class MpptStringSelection
{
    public int MpptIndex { get; set; }
    public SelectedRef? Module { get; set; }
    public int ModuleProString { get; set; } = 10;
    public int ParalleleStrings { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
}

public class LastSelection
{
    public SelectedRef? Inverter { get; set; }
    public System.Collections.Generic.List<MpptStringSelection> Strings { get; set; } = new System.Collections.Generic.List<MpptStringSelection>();
}

public interface ISettingsService
{
    CalculationParameters LoadParameters();
    bool SaveParameters(CalculationParameters parameters);

    LastSelection LoadLastSelection();
    bool SaveLastSelection(LastSelection selection);
}

#endregion

#region Implementierung

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly IConfiguration _cfg;
    private readonly string _appSettingsPath;

    public SettingsService(ILogger<SettingsService> logger, IConfiguration cfg)
    {
        _logger = logger;
        _cfg = cfg;
        _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public CalculationParameters LoadParameters()
    {
        try
        {
            var p = new CalculationParameters
            {
                MinTempC = TryParseDouble(_cfg["AnWaSolar:Parameters:MinTempC"], -20.0),
                MaxTempC = TryParseDouble(_cfg["AnWaSolar:Parameters:MaxTempC"], 40.0),
                SicherheitsmargePct = TryParseDouble(_cfg["AnWaSolar:Parameters:SicherheitsmargePct"], 10.0)
            };
            _logger.LogInformation("Berechnungsparameter geladen: Tmin={Tmin} °C, Tmax={Tmax} °C, Sicherheitsmarge={Margin} %.",
                p.MinTempC, p.MaxTempC, p.SicherheitsmargePct);
            return p;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der Berechnungsparameter, es werden Standardwerte verwendet.");
            return new CalculationParameters();
        }
    }

    public bool SaveParameters(CalculationParameters parameters)
    {
        try
        {
            var root = LoadOrCreateRoot();
            var anWa = root["AnWaSolar"] as JsonObject ?? new JsonObject();
            root["AnWaSolar"] = anWa;

            var paramNode = new JsonObject
            {
                ["MinTempC"] = parameters.MinTempC,
                ["MaxTempC"] = parameters.MaxTempC,
                ["SicherheitsmargePct"] = parameters.SicherheitsmargePct
            };
            anWa["Parameters"] = paramNode;

            WriteIndented(root);
            _logger.LogInformation("Berechnungsparameter gespeichert in {Path}.", _appSettingsPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Berechnungsparameter in {Path}.", _appSettingsPath);
            return false;
        }
    }

    public LastSelection LoadLastSelection()
    {
        var result = new LastSelection();
        try
        {
            var json = File.Exists(_appSettingsPath) ? File.ReadAllText(_appSettingsPath) : null;
            if (string.IsNullOrWhiteSpace(json)) return result;

            var root = JsonNode.Parse(json) as JsonObject;
            var anWa = root?["AnWaSolar"] as JsonObject;
            var last = anWa?["LastSelection"] as JsonObject;
            if (last is null) return result;

            var invNode = last["Inverter"] as JsonObject;
            if (invNode is not null)
            {
                result.Inverter = new SelectedRef
                {
                    Hersteller = invNode["Hersteller"]?.GetValue<string>() ?? string.Empty,
                    Model = invNode["Model"]?.GetValue<string>() ?? string.Empty
                };
            }

            var stringsNode = last["Strings"] as JsonArray;
            if (stringsNode is not null)
            {
                foreach (var n in stringsNode)
                {
                    if (n is JsonObject o)
                    {
                        var sel = new MpptStringSelection
                        {
                            MpptIndex = o["MpptIndex"]?.GetValue<int>() ?? 0,
                            ModuleProString = o["ModuleProString"]?.GetValue<int>() ?? 10,
                            ParalleleStrings = o["ParalleleStrings"]?.GetValue<int>() ?? 1,
                            IsEnabled = o["IsEnabled"]?.GetValue<bool>() ?? true
                        };
                        var modRef = o["Module"] as JsonObject;
                        if (modRef is not null)
                        {
                            sel.Module = new SelectedRef
                            {
                                Hersteller = modRef["Hersteller"]?.GetValue<string>() ?? string.Empty,
                                Model = modRef["Model"]?.GetValue<string>() ?? string.Empty
                            };
                        }
                        result.Strings.Add(sel);
                    }
                }
            }

            _logger.LogInformation("Letzte Auswahl geladen: WR={Wr}, Strings={Count}.",
                result.Inverter is null ? "(none)" : $"{result.Inverter.Hersteller} {result.Inverter.Model}",
                result.Strings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Laden der letzten Auswahl. Es wird leerer Standard zurückgegeben.");
            return result;
        }
    }

    public bool SaveLastSelection(LastSelection selection)
    {
        try
        {
            var root = LoadOrCreateRoot();
            var anWa = root["AnWaSolar"] as JsonObject ?? new JsonObject();
            root["AnWaSolar"] = anWa;

            var last = new JsonObject();

            if (selection.Inverter is not null)
            {
                last["Inverter"] = new JsonObject
                {
                    ["Hersteller"] = selection.Inverter.Hersteller,
                    ["Model"] = selection.Inverter.Model
                };
            }

            var arr = new JsonArray();
            foreach (var s in selection.Strings)
            {
                var item = new JsonObject
                {
                    ["MpptIndex"] = s.MpptIndex,
                    ["ModuleProString"] = s.ModuleProString,
                    ["ParalleleStrings"] = s.ParalleleStrings,
                    ["IsEnabled"] = s.IsEnabled
                };
                if (s.Module is not null)
                {
                    item["Module"] = new JsonObject
                    {
                        ["Hersteller"] = s.Module.Hersteller,
                        ["Model"] = s.Module.Model
                    };
                }
                arr.Add(item);
            }
            last["Strings"] = arr;

            anWa["LastSelection"] = last;

            WriteIndented(root);
            _logger.LogInformation("Letzte Auswahl gespeichert in {Path}.", _appSettingsPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der letzten Auswahl in {Path}.", _appSettingsPath);
            return false;
        }
    }

    private JsonObject LoadOrCreateRoot()
    {
        if (File.Exists(_appSettingsPath))
        {
            var json = File.ReadAllText(_appSettingsPath);
            var root = JsonNode.Parse(json) as JsonObject;
            return root ?? new JsonObject();
        }
        return new JsonObject();
    }

    private void WriteIndented(JsonObject root)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonOut = root.ToJsonString(options);
        File.WriteAllText(_appSettingsPath, jsonOut);
    }

    private static double TryParseDouble(string? text, double fallback)
    {
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out v))
            return v;
        return fallback;
    }
}

#endregion
