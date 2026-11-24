using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

/// <summary>Inverter datasheet values from <c>Data/Inverters.json</c>.</summary>
public class Inverter
{
    [JsonPropertyName("Manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("Model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("Max. DC input power (W)")]
    public double MaxDcInputPowerW { get; set; }

    [JsonPropertyName("Max. DC input voltage (V)")]
    public double MaxDcInputVoltageV { get; set; }

    [JsonPropertyName("Start voltage (V)")]
    public double StartVoltageV { get; set; }

    /// <summary>Range as text, for example "160-650"; parsed by <c>StringCalculator.TryParseVoltageRange</c>.</summary>
    [JsonPropertyName("MPPT voltage range (V)")]
    public string MpptVoltageRangeV { get; set; } = string.Empty;

    [JsonPropertyName("Nominal DC input voltage (V)")]
    public double NominalDcInputVoltageV { get; set; }

    /// <summary>Maximum operating input current per MPPT.</summary>
    [JsonPropertyName("Max. input current per MPPT (A)")]
    public double MaxInputCurrentA { get; set; }

    /// <summary>Maximum input short-circuit current per MPPT.</summary>
    [JsonPropertyName("Max. short-circuit current per MPPT (A)")]
    public double MaxShortCircuitCurrentA { get; set; }

    [JsonPropertyName("MPPT count")]
    public int MpptCount { get; set; }

    /// <summary>Maximum parallel strings per MPPT; 0 when the datasheet does not state it.</summary>
    [JsonPropertyName("Max. strings per MPPT")]
    public int MaxStringsPerMppt { get; set; }

    [JsonIgnore]
    public string DisplayName => $"{Manufacturer} {Model} (MPPT {MpptVoltageRangeV} V)";
}
