using System.Globalization;
using System.Text.Json.Serialization;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Core.Calculation;

/// <summary>Cable runs and installation conditions of a design, entered by the user.</summary>
public sealed class CableSettings
{
    /// <summary>One-way length of the string cables per MPPT in metres (index 0 = MPPT 1); 0 = not entered.</summary>
    public List<double> DcLengthsM { get; set; } = [];

    /// <summary>Cross-section of the string cables chosen by the user; null sizes it automatically.</summary>
    public double? DcCrossSectionMm2 { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<SolarCableInstallation>))]
    public SolarCableInstallation DcInstallation { get; set; } = SolarCableInstallation.OnSurface;

    /// <summary>Ambient temperature of the DC cables; on a roof often 60 °C or more.</summary>
    public double DcAmbientC { get; set; } = 60;

    /// <summary>Number of circuits laid together (grouping).</summary>
    public int DcGroupedCircuits { get; set; } = 1;

    /// <summary>Length of each lead (plus and minus) of a module when its datasheet states none; the user's value.</summary>
    public double ModuleLeadLengthM { get; set; } = 0.5;

    /// <summary>Cross-section of the module leads when the datasheet states none; the user's value.</summary>
    public double ModuleLeadCrossSectionMm2 { get; set; } = 4;

    /// <summary>One-way length of the AC line from the inverter to the distribution board; 0 = not entered.</summary>
    public double AcLengthM { get; set; }

    public string AcMethod { get; set; } = "C";

    public double AcAmbientC { get; set; } = 30;

    public int AcGroupedCircuits { get; set; } = 1;

    /// <summary>True when every battery has a cable to a busbar and a main cable runs from the busbar to the inverter.</summary>
    public bool BatteryBusbar { get; set; }

    /// <summary>One-way length of each battery cable (to the busbar, or to the inverter without busbar); 0 = not entered.</summary>
    public double BatteryCableLengthM { get; set; }

    /// <summary>Cross-section of the battery cables chosen by the user; null sizes it automatically.</summary>
    public double? BatteryCableCrossSectionMm2 { get; set; }

    /// <summary>One-way length of the main cable from the busbar to the inverter; 0 = not entered.</summary>
    public double BatteryMainLengthM { get; set; }

    /// <summary>Cross-section of the main battery cable chosen by the user; null sizes it automatically.</summary>
    public double? BatteryMainCrossSectionMm2 { get; set; }

    public double BatteryAmbientC { get; set; } = 30;

    /// <summary>Voltage drop limits in percent; null uses the defaults of the cable tables.</summary>
    public double? DcDropLimitPct { get; set; }

    public double? AcDropLimitPct { get; set; }

    public double? BatteryDropLimitPct { get; set; }

    public double DcLength(int mpptIndex) => mpptIndex >= 1 && mpptIndex <= DcLengthsM.Count ? DcLengthsM[mpptIndex - 1] : 0;

    public CableSettings Clone()
    {
        var copy = (CableSettings)MemberwiseClone();
        copy.DcLengthsM = [.. DcLengthsM];
        return copy;
    }
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
    IReadOnlyList<ResultNote> Notes)
{
    /// <summary>True when the user chose the cross-section.</summary>
    public bool CrossSectionChosen { get; init; }

    /// <summary>Leads of one module (plus and minus) in metres.</summary>
    public double LeadLengthPerModuleM { get; init; }

    public double LeadCrossSectionMm2 { get; init; }

    /// <summary>True when the lead values come from the module datasheet, false for the user's default.</summary>
    public bool LeadsFromDatasheet { get; init; }

    public int ModulesPerString { get; init; }

    /// <summary>Voltage drop of the module leads of one string; <see cref="VoltageDropPct"/> includes it.</summary>
    public double LeadDropPct { get; init; }

    /// <summary>Voltage drop of the string cable from the string to the inverter.</summary>
    public double? HomeRunDropPct { get; init; }
}

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

/// <summary>One battery cable run (battery cable or main cable) with its fuse.</summary>
/// <param name="DesignCurrentA">Current the cable and fuse are sized for.</param>
/// <param name="OperatingCurrentA">Current of the voltage drop: the share of one cable at the inverter's battery current.</param>
public sealed record BatteryCableRun(
    string Name,
    int Count,
    double LengthM,
    double DesignCurrentA,
    double OperatingCurrentA,
    double? FuseA,
    double? CrossSectionMm2,
    double? CarryingCapacityA,
    double? DropPct,
    bool CrossSectionChosen);

/// <summary>Sizing of the battery cables, the busbar connection and the fuses.</summary>
public sealed record BatteryCableResult(
    bool Busbar,
    double BankNominalV,
    BatteryCableRun BatteryCables,
    BatteryCableRun? MainCable,
    double? TotalDropPct,
    double DropLimitPct,
    ResultState State,
    IReadOnlyList<ResultNote> Notes);

/// <summary>Cable and fuse sizing of a design.</summary>
public sealed class CableCalculationResult
{
    public required bool TablesConfirmed { get; init; }

    /// <summary>True when the battery cable values of the tables are confirmed; only relevant with <see cref="Battery"/>.</summary>
    public bool BatteryTablesConfirmed { get; init; } = true;

    public IReadOnlyList<DcCableResult> Dc { get; init; } = [];

    public AcCableResult? Ac { get; init; }

    public BatteryCableResult? Battery { get; init; }

    public ResultState State =>
        Dc.Select(d => d.State).Append(Ac?.State ?? ResultState.Neutral).Append(Battery?.State ?? ResultState.Neutral)
            .Append(TablesConfirmed && (Battery is null || BatteryTablesConfirmed) ? ResultState.Ok : ResultState.Warning).Max();
}

/// <summary>Texts of the cable and fuse sizing, shared by the result card, the copied text and the report.</summary>
public static class CableTexts
{
    public static string Unconfirmed => L.T("The standard values in Data/CableTables.json are not yet confirmed; check them before you rely on the cable sizes.");

    public static string BatteryUnconfirmed => L.T("The battery cable values in Data/CableTables.json (current-carrying capacities, fuse ratings, voltage drop limit) are not yet confirmed; check them before you rely on the battery cable sizes.");

    public static string Installation(SolarCableInstallation installation) => installation switch
    {
        SolarCableInstallation.FreeAir => L.T("single cable in free air"),
        SolarCableInstallation.OnSurface => L.T("single cable on a surface"),
        _ => L.T("two cables touching on a surface"),
    };

    public static string AcMethod(string method) => method switch
    {
        "A1" => L.T("A1, single cores in conduit in an insulated wall"),
        "B1" => L.T("B1, single cores in conduit on a wall"),
        "B2" => L.T("B2, multi-core cable in conduit on a wall"),
        "C" => L.T("C, cable on a wall"),
        "E" => L.T("E, cable in free air"),
        _ => method,
    };

    public static string Dc(DcCableResult d)
    {
        ArgumentNullException.ThrowIfNull(d);
        string fuse = d.FuseRequired ? L.F($", string fuses gPV {d.FuseRatingA?.ToString("F0", CultureInfo.CurrentCulture) ?? "?"} A") : L.T(", no string fuses needed");
        string leads = L.F($", module leads {d.ModulesPerString} × {d.LeadLengthPerModuleM:0.##} m {d.LeadCrossSectionMm2:0.#} mm² ({(d.LeadsFromDatasheet ? L.T("datasheet") : L.T("default"))}) {d.LeadDropPct:F2} %");
        return d.CrossSectionMm2 is { } a
            ? L.F($"MPPT {d.MpptIndex}: {a:0.#} mm² solar cable{(d.CrossSectionChosen ? L.T(" (chosen)") : string.Empty)}, {d.LengthM:0.#} m, design current {d.DesignCurrentA:F2} A ≤ {d.CarryingCapacityA:F1} A, voltage drop {d.VoltageDropPct:F2} % (≤ {d.DropLimitPct:0.##} %){leads}{fuse}")
            : L.F($"MPPT {d.MpptIndex}: not sized{fuse}");
    }

    public static string Ac(AcCableResult a)
    {
        ArgumentNullException.ThrowIfNull(a);
        string breaker = a.BreakerA is { } b ? L.F($"circuit breaker {b:F0} A for {a.DesignCurrentA:F1} A") : L.T("no circuit breaker");
        return a.CrossSectionMm2 is { } area
            ? L.F($"AC line: {area:0.#} mm², {a.LengthM:0.#} m, {breaker}, Iz {a.CarryingCapacityA:F1} A, voltage drop {a.VoltageDropPct:F2} % (≤ {a.DropLimitPct:0.##} %)")
            : L.F($"AC line: not sized, {breaker}");
    }

    public static string Run(BatteryCableRun r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string fuse = r.FuseA is { } f ? L.F($"fuse {f:F0} A for {r.DesignCurrentA:F0} A") : L.F($"no fuse rating for {r.DesignCurrentA:F0} A");
        string count = r.Count > 1 ? $"{r.Count} × " : string.Empty;
        return r.CrossSectionMm2 is { } a
            ? L.F($"{r.Name}: {count}{a:0.#} mm²{(r.CrossSectionChosen ? L.T(" (chosen)") : string.Empty)}, {r.LengthM:0.#} m, {fuse}, Iz {r.CarryingCapacityA:F0} A, voltage drop {r.DropPct:F2} % at {r.OperatingCurrentA:F0} A")
            : L.F($"{r.Name}: {count}not sized, {fuse}");
    }

    public static string Battery(BatteryCableResult b)
    {
        ArgumentNullException.ThrowIfNull(b);
        string total = b.TotalDropPct is { } t ? L.F($"; total voltage drop {t:F2} % of {b.BankNominalV:0.#} V (≤ {b.DropLimitPct:0.##} %)") : string.Empty;
        return (b.Busbar ? L.T("Battery with busbar") : L.T("Battery without busbar")) + total;
    }
}

/// <summary>
/// Sizes the string cables, string fuses, the AC line with its circuit breaker and the battery cables with their fuses
/// from the shipped standard tables (<see cref="CableTables"/>) and the user's cable settings:
/// <list type="bullet">
/// <item>DC design current 1.25 × Isc(STC); string fuses (IEC 62548) when (Np − 1) × 1.25 × Isc exceeds the max. series
/// fuse rating of the module, rated between 1.5 and 2.4 × Isc and not above that rating; the cable carries the design
/// current, or the fuse rating when fused. The voltage drop of a string includes the module leads (from the datasheet,
/// otherwise the user's default).</item>
/// <item>AC: the smallest circuit breaker In ≥ max. AC current, the cable Iz ≥ In.</item>
/// <item>Battery: Ib ≤ In ≤ Iz for each cable; a battery cable carries the current of one battery (at most the
/// inverter's), the main cable from a busbar the battery current of the inverter and bank.</item>
/// <item>An automatic cross-section is the smallest that meets the current-carrying capacity (with temperature and
/// grouping factors) and the voltage drop limit; a chosen cross-section is checked: too low a capacity is an error, a
/// voltage drop above the limit a warning.</item>
/// </list>
/// Missing inputs or values outside the tables give a note instead of a result.
/// </summary>
public static class CableCalculator
{
    private const double Tolerance = 1e-9;

    public static CableCalculationResult Calculate(StringCalculationResult result, CableSettings settings, CableTables tables,
        BatteryCalculationResult? battery = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tables);

        var dc = result.Mppts
            .Where(m => m.Status == MpptStatus.Calculated && m.Module is not null)
            .Select(m => SizeDc(m, settings, tables))
            .ToList();
        AcCableResult? ac = result.Ac is { } acSide ? SizeAc(acSide, settings, tables) : null;
        BatteryCableResult? batteryCables = battery is { Battery: not null, ChargeCurrentA: not null, DischargeCurrentA: not null, BankNominalV: not null }
            ? SizeBattery(battery, result.Inverter, settings, tables)
            : null;
        return new CableCalculationResult
        {
            TablesConfirmed = tables.Confirmed,
            BatteryTablesConfirmed = tables.BatteryCable.Confirmed,
            Dc = dc,
            Ac = ac,
            Battery = batteryCables,
        };
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

        // Module leads: plus and minus lead of every module are part of the string circuit.
        bool leadsFromDatasheet = module.CableLengthPlusMm is > 0 && module.CableLengthMinusMm is > 0 && module.CableCrossSectionMm2 is > 0;
        double leadPerModule = leadsFromDatasheet
            ? (module.CableLengthPlusMm!.Value + module.CableLengthMinusMm!.Value) / 1000
            : 2 * settings.ModuleLeadLengthM;
        double leadArea = module.CableCrossSectionMm2 is > 0 && leadsFromDatasheet ? module.CableCrossSectionMm2.Value : settings.ModuleLeadCrossSectionMm2;
        double leadDrop = mppt.ModulesPerString * leadPerModule * module.ImppA / (tables.CopperConductivity * leadArea) / vmppString * 100;

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
                    notes.Add(new ResultNote(L.T("The max. series fuse rating of the module is not stated; with 3 or more parallel strings string fuses may be needed. Import the module datasheet."), ResultState.Warning));
                }
            }
            else if (reverse > moduleFuse + Tolerance)
            {
                fuseRequired = true;
                fuseMin = tables.Protection.StringFuseMinFactor * isc;
                fuseMax = Math.Min(tables.Protection.StringFuseMaxFactor * isc, moduleFuse);
                fuse = tables.Protection.StringFuseRatings.Where(r => r >= fuseMin - Tolerance && r <= fuseMax + Tolerance).Order().Cast<double?>().FirstOrDefault();
                notes.Add(fuse is null
                    ? new ResultNote(L.F($"String fuses are required ({parallel - 1} × {design:F2} A > {moduleFuse:F0} A), but no standard rating lies between {fuseMin:F1} A and {fuseMax:F1} A."), ResultState.Error)
                    : new ResultNote(L.F($"String fuses are required: {parallel - 1} × {design:F2} A = {reverse:F2} A > {moduleFuse:F0} A max. series fuse rating. Use gPV {fuse:F0} A in each string (plus and minus)."), ResultState.Warning));
            }
        }

        if (!leadsFromDatasheet)
        {
            notes.Add(new ResultNote(L.F($"The module datasheet states no lead length and cross-section; your default of {settings.ModuleLeadLengthM:0.##} m per lead and {settings.ModuleLeadCrossSectionMm2:0.#} mm² is used."), ResultState.Neutral));
        }

        double required = fuse ?? design;
        bool chosen = settings.DcCrossSectionMm2 is not null;
        DcCableResult Result(double len, double? area, double? iz, double? homeRun, ResultState state) =>
            new(mppt.MpptIndex, len, isc, design, module.ImppA, vmppString, fuseRequired, fuseMin, fuseMax, fuse,
                area, iz, homeRun is { } h ? h + leadDrop : null, limit, state, notes)
            {
                CrossSectionChosen = chosen,
                LeadLengthPerModuleM = leadPerModule,
                LeadCrossSectionMm2 = leadArea,
                LeadsFromDatasheet = leadsFromDatasheet,
                ModulesPerString = mppt.ModulesPerString,
                LeadDropPct = leadDrop,
                HomeRunDropPct = homeRun,
            };

        if (length <= 0)
        {
            notes.Insert(0, new ResultNote(L.T("Enter the cable length of this MPPT to size the cable."), ResultState.Neutral));
            return Result(0, null, null, null, notes.Any(n => n.State == ResultState.Error) ? ResultState.Error : ResultState.Neutral);
        }

        double? temperature = CableTables.Factor(tables.SolarCable.AmbientFactors, settings.DcAmbientC);
        double? grouping = CableTables.Factor(tables.Grouping.Factors, settings.DcGroupedCircuits);
        double[] ampacity = tables.SolarCable.Ampacity[settings.DcInstallation.ToString()];
        if (temperature is null || grouping is null)
        {
            notes.Insert(0, new ResultNote(L.T("The ambient temperature or the number of grouped circuits lies outside the tables."), ResultState.Error));
            return Result(length, null, null, null, ResultState.Error);
        }

        // Voltage drop of the string cable at the MPP (STC): ΔU = 2 · L · I / (κ · A), relative to the string voltage.
        double Drop(double area) => 2 * length * module.ImppA / (tables.CopperConductivity * area) / vmppString * 100;
        double Iz(int i) => ampacity[i] * temperature.Value * grouping.Value;

        if (settings.DcCrossSectionMm2 is { } fixedArea)
        {
            int fixedIndex = Array.IndexOf(tables.CrossSections, fixedArea);
            if (fixedIndex < 0)
            {
                notes.Insert(0, new ResultNote(L.F($"The chosen cross-section {fixedArea} mm² is not in the solar cable table."), ResultState.Error));
                return Result(length, null, null, null, ResultState.Error);
            }

            double fixedIz = Iz(fixedIndex);
            if (fixedIz < required - Tolerance)
            {
                notes.Insert(0, new ResultNote(L.F($"The chosen {fixedArea:0.#} mm² carries {fixedIz:F1} A, less than the required {required:F1} A; choose a larger cross-section."), ResultState.Error));
            }
            else if (Drop(fixedArea) + leadDrop > limit + Tolerance)
            {
                notes.Insert(0, new ResultNote(L.F($"With the chosen {fixedArea:0.#} mm² the voltage drop is {Drop(fixedArea) + leadDrop:F2} %, above the limit of {limit:0.##} %."), ResultState.Warning));
            }

            return Result(length, fixedArea, fixedIz, Drop(fixedArea), notes.Select(n => n.State).Append(ResultState.Ok).Max());
        }

        int index = Enumerable.Range(0, tables.CrossSections.Length)
            .FirstOrDefault(i => Iz(i) >= required - Tolerance && Drop(tables.CrossSections[i]) + leadDrop <= limit + Tolerance, -1);
        if (index < 0)
        {
            string reason = leadDrop >= limit ? L.F($" The module leads alone drop {leadDrop:F2} %.") : string.Empty;
            notes.Insert(0, new ResultNote(L.F($"No cross-section up to {tables.CrossSections[^1]} mm² meets {required:F1} A and {limit:0.##} % voltage drop.{reason}"), ResultState.Error));
            return Result(length, null, null, null, ResultState.Error);
        }

        double areaAuto = tables.CrossSections[index];
        return Result(length, areaAuto, Iz(index), Drop(areaAuto), notes.Select(n => n.State).Append(ResultState.Ok).Max());
    }

    private static AcCableResult SizeAc(AcCalculationResult ac, CableSettings settings, CableTables tables)
    {
        var notes = new List<ResultNote>();
        double limit = settings.AcDropLimitPct ?? tables.VoltageDrop.AcLimitPct;
        int phases = ac.Phases ?? 0;
        double current = ac.MaxAcCurrentA ?? 0;
        if (current <= 0 || phases == 0)
        {
            notes.Add(new ResultNote(L.T("The max. AC current or the phase count of the inverter is not stated; import its datasheet to size the AC line."), ResultState.Warning));
            return new AcCableResult(settings.AcLengthM, phases, current, null, null, null, null, limit, ResultState.Warning, notes);
        }

        double? breaker = tables.Protection.BreakerRatings.Where(r => r >= current - Tolerance).Order().Cast<double?>().FirstOrDefault();
        if (breaker is null)
        {
            notes.Add(new ResultNote(L.F($"No circuit breaker rating up to {tables.Protection.BreakerRatings.Max():F0} A covers {current:F1} A."), ResultState.Error));
            return new AcCableResult(settings.AcLengthM, phases, current, null, null, null, null, limit, ResultState.Error, notes);
        }

        if (settings.AcLengthM <= 0)
        {
            notes.Add(new ResultNote(L.T("Enter the length of the AC line to size the cable."), ResultState.Neutral));
            return new AcCableResult(0, phases, current, breaker, null, null, null, limit, ResultState.Neutral, notes);
        }

        Dictionary<string, double[]> table = phases == 1 ? tables.AcCable.TwoLoaded : tables.AcCable.ThreeLoaded;
        double? temperature = CableTables.Factor(tables.AcCable.AmbientFactors, settings.AcAmbientC);
        double? grouping = CableTables.Factor(tables.Grouping.Factors, settings.AcGroupedCircuits);
        if (!table.TryGetValue(settings.AcMethod, out double[]? ampacity) || temperature is null || grouping is null)
        {
            notes.Add(new ResultNote(L.T("The installation method, ambient temperature or number of grouped circuits lies outside the tables."), ResultState.Error));
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
            notes.Add(new ResultNote(L.F($"No cross-section up to {tables.CrossSections[^1]} mm² meets {breaker:F0} A and {limit:0.##} % voltage drop."), ResultState.Error));
            return new AcCableResult(settings.AcLengthM, phases, current, breaker, null, null, null, limit, ResultState.Error, notes);
        }

        double area = tables.CrossSections[index];
        return new AcCableResult(settings.AcLengthM, phases, current, breaker, area, ampacity[index] * temperature.Value * grouping.Value,
            Drop(area), limit, ResultState.Ok, notes);
    }

    private static BatteryCableResult SizeBattery(BatteryCalculationResult b, Inverter inverter, CableSettings settings, CableTables tables)
    {
        CableTables.BatteryCableTable table = tables.BatteryCable;
        var notes = new List<ResultNote>();
        Battery battery = b.Battery!;
        double nominal = b.BankNominalV!.Value;
        double limit = settings.BatteryDropLimitPct ?? table.VoltageDropLimitPct;
        double inverterMax = Math.Max(inverter.MaxBatteryChargeCurrentA, inverter.MaxBatteryDischargeCurrentA);
        double perBattery = Math.Min(Math.Max(battery.MaxChargeCurrentA, battery.MaxDischargeCurrentA), inverterMax);
        double main = Math.Max(b.ChargeCurrentA!.Value, b.DischargeCurrentA!.Value);
        int count = b.InParallel;
        bool busbar = settings.BatteryBusbar;
        double? temperature = CableTables.Factor(tables.AcCable.AmbientFactors, settings.BatteryAmbientC);

        if (!busbar && inverter.BatteryInputs > 0 && count > inverter.BatteryInputs)
        {
            notes.Add(new ResultNote(L.F($"{count} battery cables, but the inverter has {inverter.BatteryInputs} battery inputs; use a busbar."), ResultState.Warning));
        }

        // With a busbar the battery cables and the main cable share the voltage drop limit equally.
        double share = busbar ? limit / 2 : limit;
        BatteryCableRun batteryRun = SizeRun(busbar ? L.T("Battery cables to the busbar") : L.T("Battery cables to the inverter"), count, settings.BatteryCableLengthM,
            perBattery, main / count, settings.BatteryCableCrossSectionMm2, share, nominal, table, temperature, tables.CopperConductivity, notes);
        BatteryCableRun? mainRun = busbar
            ? SizeRun(L.T("Main cable busbar to inverter"), 1, settings.BatteryMainLengthM, main, main, settings.BatteryMainCrossSectionMm2, share, nominal,
                table, temperature, tables.CopperConductivity, notes)
            : null;

        double? total = batteryRun.DropPct is { } d1 && (mainRun is null || mainRun.DropPct is not null) ? d1 + (mainRun?.DropPct ?? 0) : null;
        if (total is { } t && t > limit + Tolerance)
        {
            notes.Add(new ResultNote(L.F($"The voltage drop of the battery cables is {t:F2} %, above the limit of {limit:0.##} %."), ResultState.Warning));
        }

        if (batteryRun.FuseA is not null)
        {
            notes.Add(new ResultNote(L.T("Many batteries have a built-in DC breaker; check in the battery manual whether it replaces the battery fuse."), ResultState.Neutral));
        }

        ResultState state = notes.Select(n => n.State).Append(total is null ? ResultState.Neutral : ResultState.Ok).Max();
        return new BatteryCableResult(busbar, nominal, batteryRun, mainRun, total, limit, state, notes);
    }

    // One run: fuse In ≥ Ib, cable Iz ≥ In, voltage drop 2 · L · I / (κ · A) of the nominal bank voltage.
    private static BatteryCableRun SizeRun(string name, int count, double length, double design, double operating, double? chosenArea, double dropLimit,
        double nominal, CableTables.BatteryCableTable table, double? temperature, double kappa, List<ResultNote> notes)
    {
        double? fuse = table.FuseRatings.Where(r => r >= design - Tolerance).Order().Cast<double?>().FirstOrDefault();
        bool chosen = chosenArea is not null;
        if (fuse is null)
        {
            notes.Add(new ResultNote(L.F($"{name}: no fuse rating up to {table.FuseRatings.Max():F0} A covers {design:F0} A."), ResultState.Error));
            return new BatteryCableRun(name, count, length, design, operating, null, null, null, null, chosen);
        }

        if (length <= 0)
        {
            notes.Add(new ResultNote(L.F($"{name}: enter the length to size the cable."), ResultState.Neutral));
            return new BatteryCableRun(name, count, length, design, operating, fuse, null, null, null, chosen);
        }

        if (temperature is null)
        {
            notes.Add(new ResultNote(L.F($"{name}: the ambient temperature lies outside the tables."), ResultState.Error));
            return new BatteryCableRun(name, count, length, design, operating, fuse, null, null, null, chosen);
        }

        double Drop(double area) => 2 * length * operating / (kappa * area) / nominal * 100;
        double Iz(int i) => table.Ampacity[i] * temperature.Value;
        if (chosenArea is { } fixedArea)
        {
            int fixedIndex = Array.IndexOf(table.CrossSections, fixedArea);
            if (fixedIndex < 0)
            {
                notes.Add(new ResultNote(L.F($"{name}: the chosen cross-section {fixedArea} mm² is not in the battery cable table."), ResultState.Error));
                return new BatteryCableRun(name, count, length, design, operating, fuse, null, null, null, chosen);
            }

            if (Iz(fixedIndex) < fuse.Value - Tolerance)
            {
                notes.Add(new ResultNote(L.F($"{name}: the chosen {fixedArea:0.#} mm² carries {Iz(fixedIndex):F0} A, less than the fuse rating {fuse:F0} A; choose a larger cross-section."), ResultState.Error));
            }

            return new BatteryCableRun(name, count, length, design, operating, fuse, fixedArea, Iz(fixedIndex), Drop(fixedArea), chosen);
        }

        int index = Enumerable.Range(0, table.CrossSections.Length)
            .FirstOrDefault(i => Iz(i) >= fuse.Value - Tolerance && Drop(table.CrossSections[i]) <= dropLimit + Tolerance, -1);
        if (index < 0)
        {
            notes.Add(new ResultNote(L.F($"{name}: no cross-section up to {table.CrossSections[^1]} mm² meets {fuse:F0} A and {dropLimit:0.##} % voltage drop."), ResultState.Error));
            return new BatteryCableRun(name, count, length, design, operating, fuse, null, null, null, chosen);
        }

        double area = table.CrossSections[index];
        return new BatteryCableRun(name, count, length, design, operating, fuse, area, Iz(index), Drop(area), chosen);
    }
}
