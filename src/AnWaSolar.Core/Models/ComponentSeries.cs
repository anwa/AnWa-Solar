using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AnWaSolar.Core.Models;

/// <summary>
/// A series of one manufacturer as one datasheet describes it: the series name, the datasheets and its power classes
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

    /// <summary>Datasheet PDF without a stated language, relative to the folder of the series file; null when there is none.</summary>
    [JsonPropertyName("Datasheet file")]
    public string? DatasheetFile { get; set; }

    /// <summary>German datasheet PDF, relative to the folder of the series file; null when there is none.</summary>
    [JsonPropertyName("Datasheet file (de)")]
    public string? DatasheetFileGerman { get; set; }

    /// <summary>English datasheet PDF, relative to the folder of the series file; null when there is none.</summary>
    [JsonPropertyName("Datasheet file (en)")]
    public string? DatasheetFileEnglish { get; set; }

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

    /// <summary>
    /// The power that orders the power classes of a series: Pmax of a module, the rated AC power of an inverter (the
    /// max. DC input power when it is not stated) and the nominal energy of a battery.
    /// </summary>
    public static double PowerOf(IMasterDataEntry entry) => entry switch
    {
        PVModule module => module.PmaxW,
        Inverter inverter => inverter.RatedAcPowerW > 0 ? inverter.RatedAcPowerW : inverter.MaxDcInputPowerW,
        Battery battery => battery.NominalEnergyKWh,
        _ => 0,
    };

    /// <summary>Power classes in ascending power; classes of equal power keep their order.</summary>
    public static IEnumerable<T> ByPower<T>(IEnumerable<T> variants)
        where T : IMasterDataEntry => variants.OrderBy(v => PowerOf(v));

    /// <summary>Type of an inverter from its values: phases and whether it has a battery input.</summary>
    public static string InverterType(Inverter inverter)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        return (inverter.AcPhases, inverter.HasBatteryInput) switch
        {
            (1, true) => L.T("Single-phase hybrid inverter"),
            (1, false) => L.T("Single-phase string inverter"),
            (3, true) => L.T("Three-phase hybrid inverter"),
            (3, false) => L.T("Three-phase string inverter"),
            (_, true) => L.T("Phases not stated, hybrid inverter"),
            _ => L.T("Phases not stated, string inverter"),
        };
    }

    [GeneratedRegex(@"[-\s/]+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorPattern();
}
