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

public interface ISettingsService
{
    CalculationParameters LoadParameters();
    bool SaveParameters(CalculationParameters parameters);
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
            JsonNode root;

            if (File.Exists(_appSettingsPath))
            {
                var json = File.ReadAllText(_appSettingsPath);
                root = JsonNode.Parse(json) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var anWa = root["AnWaSolar"] as JsonObject ?? new JsonObject();
            root["AnWaSolar"] = anWa;

            var paramNode = new JsonObject
            {
                ["MinTempC"] = parameters.MinTempC,
                ["MaxTempC"] = parameters.MaxTempC,
                ["SicherheitsmargePct"] = parameters.SicherheitsmargePct
            };
            anWa["Parameters"] = paramNode;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonOut = root.ToJsonString(options);
            File.WriteAllText(_appSettingsPath, jsonOut);

            _logger.LogInformation("Berechnungsparameter gespeichert in {Path}.", _appSettingsPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der Berechnungsparameter in {Path}.", _appSettingsPath);
            return false;
        }
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
