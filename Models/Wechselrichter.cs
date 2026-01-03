using System.Text.Json.Serialization;

namespace AnWaSolar.Models;

public class Wechselrichter
{
    [JsonPropertyName("Hersteller")]
    public string Hersteller { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Max.DC-Eingangsleistung (W)")]
    public int MaxDcEingangsleistungW { get; set; }

    [JsonPropertyName("Max.DC-Eingangsspannung (V)")]
    public int MaxDcEingangsspannungV { get; set; }

    [JsonPropertyName("Startspannung (V)")]
    public int StartspannungV { get; set; }

    // Bereich als String "160-650" (kann später geparst werden)
    [JsonPropertyName("MPPT-Spannungsbereich (V)")]
    public string MpptSpannungsbereichV { get; set; } = string.Empty;

    [JsonPropertyName("Nenn-DC-Eingangsspannung (V)")]
    public int NennDcEingangsspannungV { get; set; }

    [JsonPropertyName("Max. Betriebs-PV-Eingangsstrom (A)")]
    public int MaxBetriebsPvEingangsstromA { get; set; }

    [JsonPropertyName("Max. Eingangs-Kurzschlussstrom (A)")]
    public int MaxEingangsKurzschlussstromA { get; set; }

    [JsonPropertyName("Anzahl der MPP Trackers")]
    public int AnzahlDerMpptTrackers { get; set; }

    // Für UI-Darstellung
    [JsonIgnore]
    public string DisplayName => $"{Hersteller} {Model} (MPPT {MpptSpannungsbereichV} V)";
}

public class WechselrichterList
{
    [JsonPropertyName("Wechselrichter")]
    public List<Wechselrichter> Wechselrichter { get; set; } = new();
}
