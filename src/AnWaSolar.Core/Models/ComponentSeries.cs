using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AnWaSolar.Core.Models;

/// <summary>
/// A series of one manufacturer as one datasheet describes it: the series name, the datasheet and its power classes
/// (variants). Each variant holds its complete values, so that the calculation sees single devices. Stored as one JSON
/// file per series in <c>Data\&lt;Kind&gt;\&lt;Manufacturer&gt;\&lt;Series&gt;.json</c>, the datasheet next to it.
/// </summary>
public sealed class ComponentSeries<T>
    where T : class, IMasterDataEntry
{
    [JsonPropertyName("Manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("Series")]
    public string Series { get; set; } = string.Empty;

    /// <summary>Datasheet PDF, relative to the folder of the series file; null when there is none.</summary>
    [JsonPropertyName("Datasheet file")]
    public string? DatasheetFile { get; set; }

    [JsonPropertyName("Variants")]
    public List<T> Variants { get; set; } = [];
}

/// <summary>Names of series and the type of inverters.</summary>
public static partial class SeriesNames
{
    /// <summary>
    /// Suggests a series name from the model names of a datasheet: the common start and end without the power class,
    /// for example "SUN-15K-SG05LP3-EU-SM2" and "SUN-18K-SG05LP3-EU-SM2" give "SUN-SG05LP3-EU-SM2". Returns the only model
    /// for one name and null without names.
    /// </summary>
    public static string? Suggest(IEnumerable<string?> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        string[] names = [.. models.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        if (names.Length == 0)
        {
            return null;
        }

        if (names.Length == 1)
        {
            return names[0];
        }

        // Split at separators and keep the parts that all names share at the start and at the end.
        string[][] parts = [.. names.Select(n => SeparatorPattern().Split(n).Where(p => p.Length > 0).ToArray())];
        int min = parts.Min(p => p.Length);
        int head = 0;
        while (head < min && parts.All(p => string.Equals(p[head], parts[0][head], StringComparison.OrdinalIgnoreCase)))
        {
            head++;
        }

        int tail = 0;
        while (tail < min - head && parts.All(p => string.Equals(p[^(tail + 1)], parts[0][^(tail + 1)], StringComparison.OrdinalIgnoreCase)))
        {
            tail++;
        }

        string[] kept = [.. parts[0].Take(head), .. parts[0].Skip(parts[0].Length - tail)];
        return kept.Length == 0 ? names[0] : string.Join("-", kept);
    }

    /// <summary>Type of an inverter from its values: phases and whether it has a battery input.</summary>
    public static string InverterType(Inverter inverter)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        string phases = inverter.AcPhases switch
        {
            1 => "Single-phase",
            3 => "Three-phase",
            _ => "Phases not stated,",
        };
        return $"{phases} {(inverter.HasBatteryInput ? "hybrid" : "string")} inverter";
    }

    [GeneratedRegex(@"[-\s/]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorPattern();
}
