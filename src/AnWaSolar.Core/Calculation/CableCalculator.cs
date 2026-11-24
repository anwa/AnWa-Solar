using System.Text.Json.Serialization;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Core.Calculation;

/// <summary>Cable runs and installation conditions of a design, entered by the user.</summary>
public sealed class CableSettings
{
    /// <summary>One-way length of the string cables per MPPT in metres (index 0 = MPPT 1); 0 = not entered.</summary>
    public List<double> DcLengthsM { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter<SolarCableInstallation>))]
    public SolarCableInstallation DcInstallation { get; set; } = SolarCableInstallation.OnSurface;

    /// <summary>Ambient temperature of the DC cables; on a roof often 60 °C or more.</summary>
    public double DcAmbientC { get; set; } = 60;

    /// <summary>Number of circuits laid together (grouping).</summary>
    public int DcGroupedCircuits { get; set; } = 1;

    /// <summary>One-way length of the AC line from the inverter to the distribution board; 0 = not entered.</summary>
    public double AcLengthM { get; set; }

    public string AcMethod { get; set; } = "C";

    public double AcAmbientC { get; set; } = 30;

    public int AcGroupedCircuits { get; set; } = 1;

    /// <summary>Voltage drop limits in percent; null uses the defaults of the cable tables.</summary>
    public double? DcDropLimitPct { get; set; }

    public double? AcDropLimitPct { get; set; }

    public double DcLength(int mpptIndex) => mpptIndex >= 1 && mpptIndex <= DcLengthsM.Count ? DcLengthsM[mpptIndex - 1] : 0;

    public CableSettings Clone() => new()
    {
        DcLengthsM = [.. DcLengthsM],
        DcInstallation = DcInstallation,
        DcAmbientC = DcAmbientC,
        DcGroupedCircuits = DcGroupedCircuits,
        AcLengthM = AcLengthM,
        AcMethod = AcMethod,
        AcAmbientC = AcAmbientC,
        AcGroupedCircuits = AcGroupedCircuits,
        DcDropLimitPct = DcDropLimitPct,
        AcDropLimitPct = AcDropLimitPct,
    };
}

/// <summary>Sizing of the string cables of one MPPT (each string has its own cable pair).</summary>
public sealed record DcCableResult(
    int MpptIndex,
    double LengthM,
    double IscStcA,
    double DesignCurrentA,
    double ImppA,
    double VmppStringV,
    bool FuseRequired,
    double? FuseMinA,
    double? FuseMaxA,
    double? FuseRatingA,
    double? CrossSectionMm2,
    double? CarryingCapacityA,
    double? VoltageDropPct,
    double DropLimitPct,
    ResultState State,
    IReadOnlyList<ResultNote> Notes);

/// <summary>Sizing of the AC line and its circuit breaker.</summary>
public sealed record AcCableResult(
    double LengthM,
    int Phases,
    double DesignCurrentA,
    double? BreakerA,
    double? CrossSectionMm2,
    double? CarryingCapacityA,
    double? VoltageDropPct,
    double DropLimitPct,
    ResultState State,
    IReadOnlyList<ResultNote> Notes);

/// <summary>Cable and fuse sizing of a design.</summary>
public sealed class CableCalculationResult
{
    public required bool TablesConfirmed { get; init; }

    public IReadOnlyList<DcCableResult> Dc { get; init; } = [];

    public AcCableResult? Ac { get; init; }

    public ResultState State =>
        Dc.Select(d => d.State).Append(Ac?.State ?? ResultState.Neutral).Append(TablesConfirmed ? ResultState.Ok : ResultState.Warning).Max();
}

/// <summary>Texts of the cable and fuse sizing, shared by the result card, the copied text and the report.</summary>
public static class CableTexts
{
    public const string Unconfirmed = "The standard values in Data/CableTables.json are not yet confirmed; check them before you rely on the cable sizes.";

    public static string Installation(SolarCableInstallation installation) => installation switch
    {
        SolarCableInstallation.FreeAir => "single cable in free air",
        SolarCableInstallation.OnSurface => "single cable on a surface",
        _ => "two cables touching on a surface",
    };

    public static string AcMethod(string method) => method switch
    {
        "A1" => "A1, single cores in conduit in an insulated wall",
        "B1" => "B1, single cores in conduit on a wall",
        "B2" => "B2, multi-core cable in conduit on a wall",
        "C" => "C, cable on a wall",
        "E" => "E, cable in free air",
        _ => method,
    };

    public static string Dc(DcCableResult d)
    {
        ArgumentNullException.ThrowIfNull(d);
        string fuse = d.FuseRequired ? $", string fuses gPV {d.FuseRatingA?.ToString("F0", System.Globalization.CultureInfo.CurrentCulture) ?? "?"} A" : ", no string fuses needed";
        return d.CrossSectionMm2 is { } a
            ? FormattableString.CurrentCulture($"MPPT {d.MpptIndex}: {a:0.#} mm² solar cable, {d.LengthM:0.#} m, design current {d.DesignCurrentA:F2} A ≤ {d.CarryingCapacityA:F1} A, voltage drop {d.VoltageDropPct:F2} % (≤ {d.DropLimitPct:0.##} %){fuse}")
            : $"MPPT {d.MpptIndex}: not sized{fuse}";
    }

    public static string Ac(AcCableResult a)
    {
        ArgumentNullException.ThrowIfNull(a);
        string breaker = a.BreakerA is { } b ? FormattableString.CurrentCulture($"circuit breaker {b:F0} A for {a.DesignCurrentA:F1} A") : "no circuit breaker";
        return a.CrossSectionMm2 is { } area
            ? FormattableString.CurrentCulture($"AC line: {area:0.#} mm², {a.LengthM:0.#} m, {breaker}, Iz {a.CarryingCapacityA:F1} A, voltage drop {a.VoltageDropPct:F2} % (≤ {a.DropLimitPct:0.##} %)")
            : $"AC line: not sized, {breaker}";
    }
}

/// <summary>
/// Sizes the string cables, string fuses, the AC line and its circuit breaker from the shipped standard tables
/// (<see cref="CableTables"/>) and the user's cable settings:
/// <list type="bullet">
/// <item>DC design current 1.25 × Isc(STC); string fuses (IEC 62548) when (Np − 1) × 1.25 × Isc exceeds the max. series
/// fuse rating of the module, rated between 1.5 and 2.4 × Isc and not above that rating; the cable carries the design
/// current, or the fuse rating when fused.</item>
/// <item>AC: the smallest circuit breaker In ≥ max. AC current, the cable Iz ≥ In.</item>
/// <item>The cross-section is the smallest that meets the current-carrying capacity (with temperature and grouping
/// factors) and the voltage drop limit.</item>
/// </list>
/// Missing inputs or values outside the tables give a note instead of a result.
/// </summary>
public static class CableCalculator
{
    private const double Tolerance = 1e-9;

    public static CableCalculationResult Calculate(StringCalculationResult result, CableSettings settings, CableTables tables)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);

        var dc = result.Mppts
            .Where(m => m.Status == MpptStatus.Calculated && m.Module is not null)
            .Select(m => SizeDc(m, settings, tables))
            .ToList();
        AcCableResult? ac = result.Ac is { } acSide ? SizeAc(acSide, settings, tables) : null;
        return new CableCalculationResult { TablesConfirmed = tables.Confirmed, Dc = dc, Ac = ac };
    }

    private static DcCableResult SizeDc(MpptResult mppt, CableSettings settings, CableTables tables)
    {
        PVModule module = mppt.Module!;
        var notes = new List<ResultNote>();
        double length = settings.DcLength(mppt.MpptIndex);
        double isc = module.IscA;
        double design = tables.Protection.DesignCurrentFactor * isc;
        double limit = settings.DcDropLimitPct ?? tables.VoltageDrop.DcLimitPct;
        double vmppString = mppt.ModulesPerString * module.VmppV;
        int parallel = mppt.ParallelStrings;

        // String fuses (IEC 62548): needed when the reverse current of the other strings can exceed the module rating.
        bool fuseRequired = false;
        double? fuseMin = null, fuseMax = null, fuse = null;
        double reverse = (parallel - 1) * design;
        if (parallel >= 2)
        {
            if (module.MaxSeriesFuseA is not { } moduleFuse)
            {
                if (parallel >= 3)
                {
                    notes.Add(new ResultNote("The max. series fuse rating of the module is not stated; with 3 or more parallel strings string fuses may be needed. Import the module datasheet.", ResultState.Warning));
                }
            }
            else if (reverse > moduleFuse + Tolerance)
            {
                fuseRequired = true;
                fuseMin = tables.Protection.StringFuseMinFactor * isc;
                fuseMax = Math.Min(tables.Protection.StringFuseMaxFactor * isc, moduleFuse);
                fuse = tables.Protection.StringFuseRatings.Where(r => r >= fuseMin - Tolerance && r <= fuseMax + Tolerance).Order().Cast<double?>().FirstOrDefault();
                notes.Add(fuse is null
                    ? new ResultNote($"String fuses are required ({parallel - 1} × {design:F2} A > {moduleFuse:F0} A), but no standard rating lies between {fuseMin:F1} A and {fuseMax:F1} A.", ResultState.Error)
                    : new ResultNote($"String fuses are required: {parallel - 1} × {design:F2} A = {reverse:F2} A > {moduleFuse:F0} A max. series fuse rating. Use gPV {fuse:F0} A in each string (plus and minus).", ResultState.Warning));
            }
        }

        double required = fuse ?? design;
        if (length <= 0)
        {
            notes.Insert(0, new ResultNote("Enter the cable length of this MPPT to size the cable.", ResultState.Neutral));
            return new DcCableResult(mppt.MpptIndex, 0, isc, design, module.ImppA, vmppString, fuseRequired, fuseMin, fuseMax, fuse,
                null, null, null, limit, notes.Any(n => n.State == ResultState.Error) ? ResultState.Error : ResultState.Neutral, notes);
        }

        double? temperature = CableTables.Factor(tables.SolarCable.AmbientFactors, settings.DcAmbientC);
        double? grouping = CableTables.Factor(tables.Grouping.Factors, settings.DcGroupedCircuits);
        double[] ampacity = tables.SolarCable.Ampacity[settings.DcInstallation.ToString()];
        if (temperature is null || grouping is null)
        {
            notes.Insert(0, new ResultNote("The ambient temperature or the number of grouped circuits lies outside the tables.", ResultState.Error));
            return new DcCableResult(mppt.MpptIndex, length, isc, design, module.ImppA, vmppString, fuseRequired, fuseMin, fuseMax, fuse,
                null, null, null, limit, ResultState.Error, notes);
        }

        // Voltage drop of the string at the MPP (STC): ΔU = 2 · L · I / (κ · A), relative to the string voltage.
        double Drop(double area) => 2 * length * module.ImppA / (tables.CopperConductivity * area) / vmppString * 100;
        int index = Enumerable.Range(0, tables.CrossSections.Length)
            .FirstOrDefault(i => ampacity[i] * temperature.Value * grouping.Value >= required - Tolerance && Drop(tables.CrossSections[i]) <= limit + Tolerance, -1);
        if (index < 0)
        {
            notes.Insert(0, new ResultNote($"No cross-section up to {tables.CrossSections[^1]} mm² meets {required:F1} A and {limit:0.##} % voltage drop.", ResultState.Error));
            return new DcCableResult(mppt.MpptIndex, length, isc, design, module.ImppA, vmppString, fuseRequired, fuseMin, fuseMax, fuse,
                null, null, null, limit, ResultState.Error, notes);
        }

        double area = tables.CrossSections[index];
        double iz = ampacity[index] * temperature.Value * grouping.Value;
        ResultState state = notes.Select(n => n.State).Append(ResultState.Ok).Max();
        return new DcCableResult(mppt.MpptIndex, length, isc, design, module.ImppA, vmppString, fuseRequired, fuseMin, fuseMax, fuse,
            area, iz, Drop(area), limit, state, notes);
    }

    private static AcCableResult SizeAc(AcCalculationResult ac, CableSettings settings, CableTables tables)
    {
        var notes = new List<ResultNote>();
        double limit = settings.AcDropLimitPct ?? tables.VoltageDrop.AcLimitPct;
        int phases = ac.Phases ?? 0;
        double current = ac.MaxAcCurrentA ?? 0;
        if (current <= 0 || phases == 0)
        {
            notes.Add(new ResultNote("The max. AC current or the phase count of the inverter is not stated; import its datasheet to size the AC line.", ResultState.Warning));
            return new AcCableResult(settings.AcLengthM, phases, current, null, null, null, null, limit, ResultState.Warning, notes);
        }

        double? breaker = tables.Protection.BreakerRatings.Where(r => r >= current - Tolerance).Order().Cast<double?>().FirstOrDefault();
        if (breaker is null)
        {
            notes.Add(new ResultNote($"No circuit breaker rating up to {tables.Protection.BreakerRatings.Max():F0} A covers {current:F1} A.", ResultState.Error));
            return new AcCableResult(settings.AcLengthM, phases, current, null, null, null, null, limit, ResultState.Error, notes);
        }

        if (settings.AcLengthM <= 0)
        {
            notes.Add(new ResultNote("Enter the length of the AC line to size the cable.", ResultState.Neutral));
            return new AcCableResult(0, phases, current, breaker, null, null, null, limit, ResultState.Neutral, notes);
        }

        Dictionary<string, double[]> table = phases == 1 ? tables.AcCable.TwoLoaded : tables.AcCable.ThreeLoaded;
        double? temperature = CableTables.Factor(tables.AcCable.AmbientFactors, settings.AcAmbientC);
        double? grouping = CableTables.Factor(tables.Grouping.Factors, settings.AcGroupedCircuits);
        if (!table.TryGetValue(settings.AcMethod, out double[]? ampacity) || temperature is null || grouping is null)
        {
            notes.Add(new ResultNote("The installation method, ambient temperature or number of grouped circuits lies outside the tables.", ResultState.Error));
            return new AcCableResult(settings.AcLengthM, phases, current, breaker, null, null, null, limit, ResultState.Error, notes);
        }

        // Voltage drop with cos φ = 1: three-phase √3 · L · I / (κ · A) of 400 V, single-phase 2 · L · I / (κ · A) of 230 V.
        double Drop(double area) => phases == 1
            ? 2 * settings.AcLengthM * current / (tables.CopperConductivity * area) / 230 * 100
            : Math.Sqrt(3) * settings.AcLengthM * current / (tables.CopperConductivity * area) / 400 * 100;
        int index = Enumerable.Range(0, tables.CrossSections.Length)
            .FirstOrDefault(i => ampacity[i] * temperature.Value * grouping.Value >= breaker.Value - Tolerance && Drop(tables.CrossSections[i]) <= limit + Tolerance, -1);
        if (index < 0)
        {
            notes.Add(new ResultNote($"No cross-section up to {tables.CrossSections[^1]} mm² meets {breaker:F0} A and {limit:0.##} % voltage drop.", ResultState.Error));
            return new AcCableResult(settings.AcLengthM, phases, current, breaker, null, null, null, limit, ResultState.Error, notes);
        }

        double area = tables.CrossSections[index];
        return new AcCableResult(settings.AcLengthM, phases, current, breaker, area, ampacity[index] * temperature.Value * grouping.Value,
            Drop(area), limit, ResultState.Ok, notes);
    }
}
