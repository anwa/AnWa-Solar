using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

public class Wechselrichter
{
    [JsonPropertyName("Hersteller")]
    public string Hersteller { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Max.DC-Eingangsleistung (W)")]
    public double MaxDcEingangsleistungW { get; set; }

    [JsonPropertyName("Max.DC-Eingangsspannung (V)")]
    public double MaxDcEingangsspannungV { get; set; }

    [JsonPropertyName("Startspannung (V)")]
    public double StartspannungV { get; set; }

    // Range as text, for example "160-650"; parsed by StringCalculator.TryParseVoltageRange.
    [JsonPropertyName("MPPT-Spannungsbereich (V)")]
    public string MpptSpannungsbereichV { get; set; } = string.Empty;

    [JsonPropertyName("Nenn-DC-Eingangsspannung (V)")]
    public double NennDcEingangsspannungV { get; set; }

    [JsonPropertyName("Max. Betriebs-PV-Eingangsstrom (A)")]
    public double MaxBetriebsPvEingangsstromA { get; set; }

    [JsonPropertyName("Max. Eingangs-Kurzschlussstrom (A)")]
    public double MaxEingangsKurzschlussstromA { get; set; }

    [JsonPropertyName("Anzahl der MPP Trackers")]
    public int AnzahlDerMpptTrackers { get; set; }

    [JsonPropertyName("Max. Strings pro MPPT")]
    public int MaxStringsProMppt { get; set; }

    [JsonIgnore]
    public string DisplayName => $"{Hersteller} {Model} (MPPT {MpptSpannungsbereichV} V)";
}

public class WechselrichterList
{
    [JsonPropertyName("Wechselrichter")]
    public List<Wechselrichter> Wechselrichter { get; set; } = new();
}
