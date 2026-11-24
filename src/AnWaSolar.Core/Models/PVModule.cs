using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

public class PVModule
{
    [JsonPropertyName("Hersteller")]
    public string Hersteller { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Nominalleistung-PMAX (Wp)")]
    public int NominalleistungPmaxWp { get; set; }

    [JsonPropertyName("Spannung im MPP-UMPP (V)")]
    public double SpannungImMppUmppV { get; set; }

    [JsonPropertyName("Strom im MPP-IMPP (A)")]
    public double StromImMppImppA { get; set; }

    [JsonPropertyName("Leerlaufspannung-UOC (V)")]
    public double LeerlaufspannungUocV { get; set; }

    [JsonPropertyName("Kurzschlusstrom-ISC (A)")]
    public double KurzschlusstromIscA { get; set; }

    [JsonPropertyName("Modulwirkungsgrad η m (%)")]
    public double ModulwirkungsgradEtaMProzent { get; set; }

    [JsonPropertyName("Temperaturkoeffzient von PMAX (%/°C)")]
    public double TemperaturkoeffPmaxProzentProGradC { get; set; }

    [JsonPropertyName("Temperaturkoeffzient von VOC (%/°C)")]
    public double TemperaturkoeffVocProzentProGradC { get; set; }

    /// <summary>
    /// Optional VMPP temperature coefficient. Most datasheets do not state it; the calculation then uses the PMAX
    /// coefficient as a conservative approximation.
    /// </summary>
    [JsonPropertyName("Temperaturkoeffzient von VMPP (%/°C)")]
    public double? TemperaturkoeffVmppProzentProGradC { get; set; }

    [JsonPropertyName("Temperaturkoeffzient von ISC (%/°C)")]
    public double TemperaturkoeffIscProzentProGradC { get; set; }

    [JsonPropertyName("Betriebstemperatur-Min (°C)")]
    public double BetriebstemperaturMinC { get; set; }

    [JsonPropertyName("Betriebstemperatur-Max (°C)")]
    public double BetriebstemperaturMaxC { get; set; }

    [JsonIgnore]
    public string DisplayName => $"{Hersteller} {Model} ({NominalleistungPmaxWp} Wp)";
}

public class PVModuleList
{
    [JsonPropertyName("Module")]
    public List<PVModule> Module { get; set; } = new();
}
