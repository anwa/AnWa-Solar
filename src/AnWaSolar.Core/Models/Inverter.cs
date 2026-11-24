using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Models;

/// <summary>Inverter datasheet values from <c>Data/Inverters.json</c>.</summary>
public class Inverter : IMasterDataEntry
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

    /// <summary>Rated AC output (active) power; 0 when not stated.</summary>
    [JsonPropertyName("Rated AC output power (W)")]
    public double RatedAcPowerW { get; set; }

    /// <summary>Max. AC output apparent power; 0 when not stated.</summary>
    [JsonPropertyName("Max. AC apparent power (VA)")]
    public double MaxAcApparentPowerVA { get; set; }

    /// <summary>Rated AC output current per phase at the rated grid voltage; 0 when not stated.</summary>
    [JsonPropertyName("Rated AC output current (A)")]
    public double RatedAcCurrentA { get; set; }

    /// <summary>Max. AC output current per phase; 0 when not stated.</summary>
    [JsonPropertyName("Max. AC output current (A)")]
    public double MaxAcCurrentA { get; set; }

    /// <summary>Number of AC phases (1 or 3); 0 when not stated.</summary>
    [JsonPropertyName("AC phases")]
    public int AcPhases { get; set; }

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
    public string DisplayName => $"{Manufacturer} {Model} (MPPT {MpptVoltageRangeV} V)";
}
