using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

/// <summary>
/// Datasheet values of one battery module from <c>Data/Batteries.json</c>. A battery bank consists of modules in series
/// (for example the modules of a high-voltage tower) and in parallel.
/// </summary>
public class Battery : IMasterDataEntry
{
    [JsonPropertyName("Manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Nominal voltage (V)")]
    public double NominalVoltageV { get; set; }

    /// <summary>Operating voltage range of one module as text, for example "44.8-57.6".</summary>
    [JsonPropertyName("Operating voltage range (V)")]
    public string VoltageRangeV { get; set; } = string.Empty;

    /// <summary>Nominal capacity of one module; null when the datasheet does not state it.</summary>
    [JsonPropertyName("Nominal capacity (Ah)")]
    public double? NominalCapacityAh { get; set; }

    [JsonPropertyName("Nominal energy (kWh)")]
    public double NominalEnergyKWh { get; set; }

    /// <summary>Usable energy of one module; null when the datasheet does not state it.</summary>
    [JsonPropertyName("Usable energy (kWh)")]
    public double? UsableEnergyKWh { get; set; }

    /// <summary>Recommended or max. depth of discharge; null when the datasheet does not state it.</summary>
    [JsonPropertyName("Depth of discharge (%)")]
    public double? DepthOfDischargePct { get; set; }

    /// <summary>
    /// Usable energy of one module: the stated value, otherwise the nominal energy times the depth of discharge; null
    /// when the datasheet states neither.
    /// </summary>
    [JsonIgnore]
    public double? UsableEnergyPerModuleKWh => UsableEnergyKWh ?? (DepthOfDischargePct is { } dod ? NominalEnergyKWh * dod / 100 : null);

    /// <summary>Max. continuous charge current of one module.</summary>
    [JsonPropertyName("Max. charge current (A)")]
    public double MaxChargeCurrentA { get; set; }

    /// <summary>Max. continuous discharge current of one module.</summary>
    [JsonPropertyName("Max. discharge current (A)")]
    public double MaxDischargeCurrentA { get; set; }

    /// <summary>Max. modules in series; 0 when the datasheet does not state it.</summary>
    [JsonPropertyName("Max. modules in series")]
    public int MaxModulesInSeries { get; set; }

    /// <summary>Max. modules (or series strings) in parallel; 0 when the datasheet does not state it.</summary>
    [JsonPropertyName("Max. modules in parallel")]
    public int MaxModulesInParallel { get; set; }

    /// <inheritdoc cref="Inverter.DatasheetFile"/>
    [JsonPropertyName("Datasheet file")]
    public string? DatasheetFile { get; set; }

    [JsonIgnore]
    public bool IsUserEntry { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public string Series { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => $"{Manufacturer} {Model}";
}
