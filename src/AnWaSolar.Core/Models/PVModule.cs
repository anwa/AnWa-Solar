using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

/// <summary>PV module datasheet values (STC, 25 °C) from <c>Data/Modules.json</c>.</summary>
public class PVModule : IMasterDataEntry
{
    [JsonPropertyName("Manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Pmax (W)")]
    public double PmaxW { get; set; }

    [JsonPropertyName("Vmpp (V)")]
    public double VmppV { get; set; }

    [JsonPropertyName("Impp (A)")]
    public double ImppA { get; set; }

    [JsonPropertyName("Voc (V)")]
    public double VocV { get; set; }

    [JsonPropertyName("Isc (A)")]
    public double IscA { get; set; }

    [JsonPropertyName("Module efficiency (%)")]
    public double EfficiencyPct { get; set; }

    [JsonPropertyName("Temperature coefficient of Pmax (%/°C)")]
    public double PmaxTempCoeffPctPerC { get; set; }

    [JsonPropertyName("Temperature coefficient of Voc (%/°C)")]
    public double VocTempCoeffPctPerC { get; set; }

    /// <summary>
    /// Optional Vmpp temperature coefficient. Most datasheets do not state it; the calculation then uses the Pmax
    /// coefficient as a conservative approximation.
    /// </summary>
    [JsonPropertyName("Temperature coefficient of Vmpp (%/°C)")]
    public double? VmppTempCoeffPctPerC { get; set; }

    [JsonPropertyName("Temperature coefficient of Isc (%/°C)")]
    public double IscTempCoeffPctPerC { get; set; }

    [JsonPropertyName("Operating temperature min (°C)")]
    public double OperatingTempMinC { get; set; }

    [JsonPropertyName("Operating temperature max (°C)")]
    public double OperatingTempMaxC { get; set; }

    /// <summary>
    /// Imported datasheet, relative to the user data folder (for example <c>Datasheets/Trina TSM-445.pdf</c>); only own
    /// entries have one.
    /// </summary>
    [JsonPropertyName("Datasheet file")]
    public string? DatasheetFile { get; set; }

    /// <summary>True for an entry from the own master data in the user data folder.</summary>
    [JsonIgnore]
    public bool IsUserEntry { get; set; }

    [JsonIgnore]
    public string DisplayName => $"{Manufacturer} {Model} ({PmaxW} W)";
}
