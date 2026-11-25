using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnWaSolar.Core.Calculation;

/// <summary>Installation of the DC solar cables (EN 50618, Annex A).</summary>
public enum SolarCableInstallation
{
    FreeAir,
    OnSurface,
    TwoTouchingOnSurface,
}

/// <summary>
/// Standard tables for cable and fuse sizing from the shipped <c>Data/CableTables.json</c>: current-carrying capacities,
/// correction factors, conductivity, rated currents and the IEC 62548 factors. The values and their sources are data,
/// not code; <see cref="Confirmed"/> tells whether the user has checked them.
/// </summary>
public sealed class CableTables
{
    public const string FileName = "CableTables.json";

    [JsonPropertyName("Confirmed")]
    public bool Confirmed { get; set; }

    [JsonPropertyName("Copper conductivity (m/(Ohm*mm2))")]
    public double CopperConductivity { get; set; }

    [JsonPropertyName("Cross-sections (mm2)")]
    public double[] CrossSections { get; set; } = [];

    [JsonPropertyName("SolarCable")]
    public SolarCableTable SolarCable { get; set; } = new();

    [JsonPropertyName("AcCable")]
    public AcCableTable AcCable { get; set; } = new();

    [JsonPropertyName("Grouping")]
    public FactorTable Grouping { get; set; } = new();

    [JsonPropertyName("Protection")]
    public ProtectionTable Protection { get; set; } = new();

    [JsonPropertyName("Voltage drop")]
    public VoltageDropTable VoltageDrop { get; set; } = new();

    [JsonPropertyName("BatteryCable")]
    public BatteryCableTable BatteryCable { get; set; } = new();

    public sealed class SolarCableTable
    {
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("Ampacity (A)")]
        public Dictionary<string, double[]> Ampacity { get; set; } = [];

        [JsonPropertyName("Ambient factors")]
        public Dictionary<string, double> AmbientFactors { get; set; } = [];
    }

    public sealed class AcCableTable
    {
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("Ampacity two loaded conductors (A)")]
        public Dictionary<string, double[]> TwoLoaded { get; set; } = [];

        [JsonPropertyName("Ampacity three loaded conductors (A)")]
        public Dictionary<string, double[]> ThreeLoaded { get; set; } = [];

        [JsonPropertyName("Ambient factors")]
        public Dictionary<string, double> AmbientFactors { get; set; } = [];
    }

    public sealed class FactorTable
    {
        public string Source { get; set; } = string.Empty;

        public Dictionary<string, double> Factors { get; set; } = [];
    }

    public sealed class ProtectionTable
    {
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("Design current factor (x Isc STC)")]
        public double DesignCurrentFactor { get; set; }

        [JsonPropertyName("String fuse min. (x Isc STC)")]
        public double StringFuseMinFactor { get; set; }

        [JsonPropertyName("String fuse max. (x Isc STC)")]
        public double StringFuseMaxFactor { get; set; }

        [JsonPropertyName("String fuse ratings (A)")]
        public double[] StringFuseRatings { get; set; } = [];

        [JsonPropertyName("Circuit breaker ratings (A)")]
        public double[] BreakerRatings { get; set; } = [];
    }

    /// <summary>
    /// Single-core copper cables between battery, busbar and inverter, with their own cross-sections, fuse ratings and
    /// voltage drop limit, and their own confirmation. The ambient factors of <see cref="AcCable"/> apply.
    /// </summary>
    public sealed class BatteryCableTable
    {
        [JsonPropertyName("Confirmed")]
        public bool Confirmed { get; set; }

        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("Cross-sections (mm2)")]
        public double[] CrossSections { get; set; } = [];

        [JsonPropertyName("Ampacity (A)")]
        public double[] Ampacity { get; set; } = [];

        [JsonPropertyName("Fuse ratings (A)")]
        public double[] FuseRatings { get; set; } = [];

        [JsonPropertyName("Voltage drop limit (%)")]
        public double VoltageDropLimitPct { get; set; }
    }

    public sealed class VoltageDropTable
    {
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("DC limit (%)")]
        public double DcLimitPct { get; set; }

        [JsonPropertyName("AC limit (%)")]
        public double AcLimitPct { get; set; }
    }

    /// <summary>AC installation methods of the table, for example A1, B1, B2, C, E.</summary>
    public IReadOnlyList<string> AcMethods => [.. AcCable.ThreeLoaded.Keys];

    /// <summary>Reads the tables; returns null and the reason when the file is missing or inconsistent.</summary>
    public static CableTables? Load(string path, out string? error)
    {
        try
        {
            CableTables? tables = JsonSerializer.Deserialize<CableTables>(File.ReadAllText(path));
            error = tables is null ? L.T("The file is empty.") : tables.Validate();
            return error is null ? tables : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    // Every table row must have one value per cross-section, and the factors must be usable.
    internal string? Validate()
    {
        int n = CrossSections.Length;
        if (n == 0 || CrossSections.Any(a => a <= 0) || CopperConductivity <= 0)
        {
            return L.T("Cross-sections and conductivity must be greater than 0.");
        }

        IEnumerable<KeyValuePair<string, double[]>> rows = SolarCable.Ampacity.Concat(AcCable.TwoLoaded).Concat(AcCable.ThreeLoaded);
        if (rows.FirstOrDefault(r => r.Value.Length != n || r.Value.Any(v => v <= 0)) is { Key: { } bad })
        {
            return L.F($"Ampacity row \"{bad}\" must have {n} values greater than 0.");
        }

        foreach (SolarCableInstallation installation in Enum.GetValues<SolarCableInstallation>())
        {
            if (!SolarCable.Ampacity.ContainsKey(installation.ToString()))
            {
                return L.F($"Solar cable row \"{installation}\" is missing.");
            }
        }

        if (!AcCable.ThreeLoaded.Keys.Order().SequenceEqual(AcCable.TwoLoaded.Keys.Order()))
        {
            return L.T("The AC tables for two and three loaded conductors must have the same installation methods.");
        }

        if (Protection.DesignCurrentFactor < 1 || Protection.StringFuseMinFactor <= 0
            || Protection.StringFuseMaxFactor <= Protection.StringFuseMinFactor
            || Protection.StringFuseRatings.Length == 0 || Protection.BreakerRatings.Length == 0)
        {
            return L.T("The protection factors and rated currents are incomplete.");
        }

        if (BatteryCable.CrossSections.Length == 0 || BatteryCable.CrossSections.Length != BatteryCable.Ampacity.Length
            || BatteryCable.CrossSections.Any(a => a <= 0) || BatteryCable.Ampacity.Any(a => a <= 0)
            || BatteryCable.FuseRatings.Length == 0 || BatteryCable.VoltageDropLimitPct <= 0)
        {
            return L.T("The battery cable table needs one current-carrying capacity per cross-section, fuse ratings and a voltage drop limit.");
        }

        return SolarCable.AmbientFactors.Count == 0 || AcCable.AmbientFactors.Count == 0 || Grouping.Factors.Count == 0
            ? L.T("Correction factors are missing.")
            : null;
    }

    /// <summary>
    /// Factor of a table keyed by temperature or count: the exact entry, otherwise the next higher key (the smaller
    /// factor, on the safe side); null above the table.
    /// </summary>
    public static double? Factor(IReadOnlyDictionary<string, double> table, double key)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table
            .Select(e => (Key: double.Parse(e.Key, System.Globalization.CultureInfo.InvariantCulture), e.Value))
            .Where(e => e.Key >= key - 1e-9)
            .OrderBy(e => e.Key)
            .Select(e => (double?)e.Value)
            .FirstOrDefault();
    }
}
