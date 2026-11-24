using System.Globalization;

using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>Display state of a result, a value row or a note.</summary>
public enum ResultState
{
    /// <summary>Nothing to judge yet (no inverter, no module, disabled MPPT) or an informational note.</summary>
    Neutral,

    /// <summary>All checked limits are met.</summary>
    Ok,

    /// <summary>A configuration is incomplete, for example an MPPT without PV module.</summary>
    Warning,

    /// <summary>At least one limit is violated or the inverter data is invalid.</summary>
    Error,
}

/// <summary>One quantity of an MPPT result with its lowest and highest value and the effective limit.</summary>
/// <param name="Quantity">Name of the quantity, for example "String Voc".</param>
/// <param name="Min">Lowest value over the temperature range, or "–".</param>
/// <param name="Max">Highest value over the temperature range.</param>
/// <param name="Limit">Effective limit with the safety margin.</param>
/// <param name="State"><see cref="ResultState.Error"/> when a violation belongs to this quantity, otherwise OK.</param>
public sealed record ResultValueRow(string Quantity, string Min, string Max, string Limit, ResultState State);

/// <summary>A note of an MPPT result.</summary>
public sealed record ResultNote(string Text, ResultState State);

/// <summary>
/// Voltages for the voltage window graphic of one MPPT, in volts: the string voltage range against the effective
/// MPPT range and the effective maximum DC voltage.
/// </summary>
/// <param name="ScaleMaxV">Right end of the scale.</param>
public sealed record VoltageWindow(
    double ScaleMaxV,
    double MpptLowerV,
    double MpptUpperV,
    double StartV,
    double MaxDcV,
    double VmppMinV,
    double VmppMaxV,
    double VocMaxV,
    bool VocExceedsMaxDc,
    bool VmppOutsideMppt);

/// <summary>Presentation of one MPPT result.</summary>
public sealed record MpptView(
    int MpptIndex,
    string Title,
    string Subtitle,
    ResultState State,
    string StatusText,
    IReadOnlyList<ResultValueRow> Rows,
    VoltageWindow? Voltage,
    string AllowedRange,
    IReadOnlyList<ResultNote> Notes)
{
    public bool HasRows => Rows.Count > 0;
}

/// <summary>A value of the AC side with its limit.</summary>
public sealed record AcRow(string Quantity, string Value, string Limit, ResultState State);

/// <summary>Presentation of the AC side and grid connection.</summary>
public sealed record AcView(string Subtitle, ResultState State, string StatusText, IReadOnlyList<AcRow> Rows, IReadOnlyList<ResultNote> Notes);

/// <summary>Presentation of the cable and fuse sizing.</summary>
public sealed record CableView(string Subtitle, ResultState State, string StatusText, IReadOnlyList<AcRow> Rows, IReadOnlyList<ResultNote> Notes);

/// <summary>An inverter limit from the datasheet and after applying the safety margin.</summary>
public sealed record LimitRow(string Name, string Datasheet, string Effective);

/// <summary>Presentation of a complete calculation for the results area of the main window.</summary>
public sealed record ResultOverview(
    ResultState State,
    string Headline,
    string Details,
    IReadOnlyList<MpptView> Mppts,
    IReadOnlyList<LimitRow> Limits,
    string LimitsCaption,
    AcView? Ac = null,
    CableView? Cables = null);

/// <summary>
/// Turns a <see cref="StringCalculationResult"/> into texts and states for the results area. It only formats values
/// of the result; every limit check stays in <see cref="StringCalculator"/>. Numbers use the current culture.
/// </summary>
public static class ResultPresenter
{
    private const string None = "–";

    public static ResultOverview Present(StringCalculationResult? result) => Present(result, cables: null, settings: null);

    /// <summary>Presentation including the cable and fuse sizing.</summary>
    public static ResultOverview Present(StringCalculationResult? result, CableCalculationResult? cables, CableSettings? settings)
    {
        if (result is null)
        {
            return new ResultOverview(ResultState.Neutral, "No inverter selected",
                "Select an inverter to start the calculation.", [], [], string.Empty);
        }

        Inverter inv = result.Inverter;
        if (result.Limits is not { } limits)
        {
            return new ResultOverview(ResultState.Error, "Invalid inverter data",
                $"The MPPT voltage range of {inv.Manufacturer} {inv.Model} cannot be read: '{inv.MpptVoltageRangeV}'.", [], [], string.Empty);
        }

        List<MpptView> mppts = result.Mppts.Select(m => PresentMppt(m, inv, limits)).ToList();
        AcView? ac = result.Ac is { } acResult ? PresentAc(acResult, inv, result.Parameters) : null;
        CableView? cableView = cables is not null && settings is not null ? PresentCables(cables, settings) : null;
        int violations = result.Mppts.Sum(m => m.Violations.Count) + (result.Ac?.Checks.Count(c => c.State == ResultState.Error) ?? 0)
            + (cables is null ? 0 : cables.Dc.Count(d => d.State == ResultState.Error) + (cables.Ac?.State == ResultState.Error ? 1 : 0));
        int[] withoutModule = result.Mppts.Where(m => m.Status == MpptStatus.NoModule).Select(m => m.MpptIndex).ToArray();
        int calculated = result.Mppts.Count(m => m.Status == MpptStatus.Calculated);

        (ResultState state, string headline) = (violations, withoutModule.Length, calculated) switch
        {
            ( > 0, _, _) => (ResultState.Error, violations == 1 ? "1 limit violated" : $"{violations} limits violated"),
            (_, > 0, _) => (ResultState.Warning, withoutModule.Length == 1
                ? $"MPPT {withoutModule[0]} has no PV module"
                : $"MPPTs {string.Join(", ", withoutModule)} have no PV module"),
            (_, _, 0) => (ResultState.Neutral, "All MPPTs are disabled"),
            _ when ac?.State == ResultState.Warning => (ResultState.Warning, "All limits are met; see the notes of the AC side"),
            _ => (ResultState.Ok, "All limits are met"),
        };

        CalculationParameters p = result.Parameters;
        string details = $"{inv.Manufacturer} {inv.Model} · cell temperature {Number(p.MinTempC, "0.#")} … {Number(p.MaxTempC, "0.#")} °C · safety margin {Number(p.SafetyMarginPct, "0.#")} %";

        return new ResultOverview(state, headline, details, mppts, PresentLimits(inv, limits), Ac: ac, Cables: cableView, LimitsCaption:
            $"Datasheet values and the limits used by the calculation (safety margin {Number(p.SafetyMarginPct, "0.#")} %).");
    }

    private static MpptView PresentMppt(MpptResult mppt, Inverter inv, EffectiveLimits limits)
    {
        string title = $"MPPT {mppt.MpptIndex}";
        switch (mppt.Status)
        {
            case MpptStatus.MissingConfiguration:
                return Empty(mppt, title, ResultState.Warning, "Missing", "No string configuration exists for this MPPT.");
            case MpptStatus.Disabled:
                return Empty(mppt, title, ResultState.Neutral, "Disabled", "This MPPT is disabled and skipped by the calculation.");
            case MpptStatus.NoModule:
                return Empty(mppt, title, ResultState.Warning, "No module", "Select a PV module for this MPPT.");
            case MpptStatus.Calculated:
            default:
                break;
        }

        PVModule module = mppt.Module!;
        bool Has(params ViolationKind[] kinds) => mppt.Violations.Any(v => kinds.Contains(v.Kind));
        ResultState StateOf(params ViolationKind[] kinds) => Has(kinds) ? ResultState.Error : ResultState.Ok;

        var rows = new List<ResultValueRow>
        {
            new("String Voc", Volt(mppt.StringVocMinV), Volt(mppt.StringVocMaxV),
                $"≤ {Volt(limits.MaxDcVoltageV)}", StateOf(ViolationKind.MaxDcVoltage)),
            new("String Vmpp", Volt(mppt.StringVmppMinV), Volt(mppt.StringVmppMaxV),
                $"{Number(limits.MpptLowerBoundV, "F1")} … {Volt(limits.MpptUpperBoundV)}, start {Volt(limits.StartVoltageV)}",
                StateOf(ViolationKind.MpptLowerBound, ViolationKind.MpptUpperBound, ViolationKind.StartVoltage)),
            new("Isc of all strings", Ampere(mppt.ArrayIscMinA), Ampere(mppt.ArrayIscMaxA),
                $"≤ {Ampere(limits.MaxShortCircuitCurrentA)}", StateOf(ViolationKind.ShortCircuitCurrent)),
            new("Impp of all strings", Ampere(mppt.ArrayImppMinA), Ampere(mppt.ArrayImppMaxA),
                $"≤ {Ampere(limits.MaxInputCurrentA)}", StateOf(ViolationKind.InputCurrent)),
            new("DC power (cold)", None, Watt(mppt.ArrayPowerAtTminW),
                $"≤ {Watt(limits.DcPowerPerMpptLimitW)}", StateOf(ViolationKind.DcPower)),
            new("Parallel strings", None, mppt.ParallelStrings.ToString(CultureInfo.CurrentCulture),
                inv.MaxStringsPerMppt > 0 ? $"≤ {inv.MaxStringsPerMppt.ToString(CultureInfo.CurrentCulture)}" : "n/a",
                StateOf(ViolationKind.MaxStringsPerMppt)),
        };

        var notes = mppt.Violations.Select(v => new ResultNote(CalculationTextFormatter.FormatViolation(v), ResultState.Error)).ToList();
        if (mppt.Temperatures is { VmppCoefficientApproximated: true })
        {
            notes.Add(new ResultNote("The datasheet states no Vmpp temperature coefficient; the Pmax coefficient is used as an approximation.", ResultState.Neutral));
        }

        int count = mppt.Violations.Count;
        string subtitle = $"{module.Manufacturer} {module.Model} · {mppt.ModulesPerString} × {mppt.ParallelStrings} " +
                          $"({(mppt.ModulesPerString == 1 ? "1 module" : $"{mppt.ModulesPerString} modules")} per string, " +
                          $"{(mppt.ParallelStrings == 1 ? "1 string" : $"{mppt.ParallelStrings} strings")})";
        string allowed = mppt.HasValidModuleRange
            ? $"Allowed modules per string: {mppt.MinModulesPerString} … {mppt.MaxModulesPerString}"
            : "No module count per string meets all voltage limits.";

        var voltage = new VoltageWindow(
            ScaleMaxV: Math.Max(Math.Max(inv.MaxDcInputVoltageV, limits.MpptMaxV), mppt.StringVocMaxV) * 1.05,
            MpptLowerV: limits.MpptLowerBoundV,
            MpptUpperV: limits.MpptUpperBoundV,
            StartV: limits.StartVoltageV,
            MaxDcV: limits.MaxDcVoltageV,
            VmppMinV: mppt.StringVmppMinV,
            VmppMaxV: mppt.StringVmppMaxV,
            VocMaxV: mppt.StringVocMaxV,
            VocExceedsMaxDc: Has(ViolationKind.MaxDcVoltage),
            VmppOutsideMppt: Has(ViolationKind.MpptLowerBound, ViolationKind.MpptUpperBound, ViolationKind.StartVoltage));

        return new MpptView(mppt.MpptIndex, title, subtitle,
            count == 0 ? ResultState.Ok : ResultState.Error,
            count == 0 ? "OK" : count == 1 ? "1 issue" : $"{count} issues",
            rows, voltage, allowed, notes);
    }

    private static AcView PresentAc(AcCalculationResult ac, Inverter inv, CalculationParameters p)
    {
        ResultState StateOf(params AcCheckKind[] kinds) =>
            ac.Checks.Where(c => kinds.Contains(c.Kind)).Select(c => c.State).DefaultIfEmpty(ResultState.Ok).Max();
        string Kilo(double? value, string unit) => value is { } v ? $"{Number(v / 1000, "F1")} {unit}" : "not stated";

        var rows = new List<AcRow>
        {
            new("PV peak power (STC)", $"{Number(ac.PvPeakPowerW / 1000, "F2")} kWp", None, ResultState.Neutral),
            new("Rated AC power", Kilo(ac.RatedAcPowerW, "kW"), None, ac.RatedAcPowerW is null ? ResultState.Warning : ResultState.Neutral),
            new("DC/AC ratio", ac.DcAcRatio is { } r ? Number(r, "F2") : None,
                $"{Number(p.DcAcRatioMin, "0.0#")} … {Number(p.DcAcRatioMax, "0.0#")}",
                ac.DcAcRatio is null ? ResultState.Neutral : StateOf(AcCheckKind.DcAcRatioLow, AcCheckKind.DcAcRatioHigh)),
            new("Apparent power", Kilo(ac.ApparentPowerVA, "kVA"),
                $"≤ {Number(p.SimplifiedRegistrationLimitVA / 1000, "0.#")} kVA (simplified registration)",
                ac.ApparentPowerVA is null ? ResultState.Neutral : StateOf(AcCheckKind.RegistrationLimit)),
        };
        if (ac.Phases == 1)
        {
            rows.Add(new("Unbalanced load (single-phase)", Kilo(ac.ApparentPowerVA, "kVA"),
                $"≤ {Number(p.UnbalancedLoadLimitVA / 1000, "0.0#")} kVA", StateOf(AcCheckKind.UnbalancedLoad)));
        }

        rows.Add(new("AC current per phase",
            ac.MaxAcCurrentA is { } max ? $"{Number(max, "F1")} A max." + (ac.RatedAcCurrentA is { } rated ? $" ({Number(rated, "F1")} A rated)" : string.Empty) : "not stated",
            None, ac.MaxAcCurrentA is null ? ResultState.Warning : ResultState.Neutral));
        if (ac.FeedInCapW is { } cap)
        {
            rows.Add(new("Feed-in limit", $"{Number(cap / 1000, "F1")} kW", $"{Number(p.FeedInLimitPct, "0.#")} % of the PV peak power", ResultState.Neutral));
        }

        var notes = ac.Checks.Select(c => new ResultNote(CalculationTextFormatter.FormatAcCheck(c, ac), c.State)).ToList();
        ResultState state = ac.Checks.Select(c => c.State).Where(s => s != ResultState.Neutral).DefaultIfEmpty(ResultState.Ok).Max();
        int issues = ac.Checks.Count(c => c.State is ResultState.Error or ResultState.Warning);
        string subtitle = $"{inv.Manufacturer} {inv.Model} · " + (ac.Phases is { } phases ? (phases == 1 ? "single-phase" : "three-phase") : "phases not stated");
        return new AcView(subtitle, state, issues == 0 ? "OK" : issues == 1 ? "1 note" : $"{issues} notes", rows, notes);
    }

    private static CableView PresentCables(CableCalculationResult cables, CableSettings settings)
    {
        var rows = new List<AcRow>();
        var notes = new List<ResultNote>();
        if (!cables.TablesConfirmed)
        {
            notes.Add(new ResultNote(CableTexts.Unconfirmed, ResultState.Warning));
        }

        foreach (DcCableResult d in cables.Dc)
        {
            rows.Add(new AcRow($"MPPT {d.MpptIndex} string cables",
                d.CrossSectionMm2 is { } a ? $"{Number(a, "0.#")} mm² · {Number(d.VoltageDropPct!.Value, "F2")} %" : "not sized",
                $"≥ {Number(d.FuseRatingA ?? d.DesignCurrentA, "F1")} A · ≤ {Number(d.DropLimitPct, "0.##")} %",
                d.CrossSectionMm2 is null ? (d.State == ResultState.Error ? ResultState.Error : ResultState.Neutral) : ResultState.Ok));
            rows.Add(new AcRow($"MPPT {d.MpptIndex} string fuses",
                d.FuseRequired ? (d.FuseRatingA is { } f ? $"gPV {Number(f, "F0")} A" : "no rating fits") : "not needed",
                d.FuseRequired ? $"{Number(d.FuseMinA!.Value, "F1")} … {Number(d.FuseMaxA!.Value, "F1")} A" : None,
                d.FuseRequired ? (d.FuseRatingA is null ? ResultState.Error : ResultState.Warning) : ResultState.Neutral));
            notes.AddRange(d.Notes.Select(n => n with { Text = $"MPPT {d.MpptIndex}: {n.Text}" }));
        }

        if (cables.Ac is { } ac)
        {
            rows.Add(new AcRow("AC circuit breaker", ac.BreakerA is { } b ? $"{Number(b, "F0")} A" : "–",
                ac.DesignCurrentA > 0 ? $"≥ {Number(ac.DesignCurrentA, "F1")} A (max. AC current)" : None,
                ac.BreakerA is null ? ac.State : ResultState.Ok));
            rows.Add(new AcRow("AC line",
                ac.CrossSectionMm2 is { } area ? $"{Number(area, "0.#")} mm² · {Number(ac.VoltageDropPct!.Value, "F2")} %" : "not sized",
                ac.BreakerA is { } breaker ? $"Iz ≥ {Number(breaker, "F0")} A · ≤ {Number(ac.DropLimitPct, "0.##")} %" : None,
                ac.CrossSectionMm2 is null ? (ac.State == ResultState.Error ? ResultState.Error : ResultState.Neutral) : ResultState.Ok));
            notes.AddRange(ac.Notes.Select(n => n with { Text = $"AC line: {n.Text}" }));
        }

        ResultState state = cables.State;
        int issues = notes.Count(n => n.State is ResultState.Error or ResultState.Warning);
        string subtitle = $"DC: {CableTexts.Installation(settings.DcInstallation)}, {Number(settings.DcAmbientC, "0.#")} °C · AC: method {settings.AcMethod}, {Number(settings.AcAmbientC, "0.#")} °C";
        return new CableView(subtitle, state, issues == 0 ? (state == ResultState.Neutral ? "Lengths missing" : "OK") : issues == 1 ? "1 note" : $"{issues} notes", rows, notes);
    }

    private static MpptView Empty(MpptResult mppt, string title, ResultState state, string status, string note) =>
        new(mppt.MpptIndex, title, string.Empty, state, status, [], null, string.Empty, [new ResultNote(note, ResultState.Neutral)]);

    private static List<LimitRow> PresentLimits(Inverter inv, EffectiveLimits limits) =>
    [
        new("Max. DC voltage", Volt(inv.MaxDcInputVoltageV), Volt(limits.MaxDcVoltageV)),
        new("MPPT voltage range", $"{Number(limits.MpptMinV, "F1")} … {Volt(limits.MpptMaxV)}",
            $"{Number(limits.MpptLowerBoundV, "F1")} … {Volt(limits.MpptUpperBoundV)}"),
        new("Start voltage", Volt(inv.StartVoltageV), Volt(limits.StartVoltageV)),
        new("Max. input current per MPPT", Ampere(inv.MaxInputCurrentA), Ampere(limits.MaxInputCurrentA)),
        new("Max. short-circuit current per MPPT", Ampere(inv.MaxShortCircuitCurrentA), Ampere(limits.MaxShortCircuitCurrentA)),
        new("DC power per MPPT", Watt(limits.DcPowerPerMpptW), Watt(limits.DcPowerPerMpptLimitW)),
        new("MPPTs / max. strings per MPPT",
            $"{inv.MpptCount.ToString(CultureInfo.CurrentCulture)} / {(inv.MaxStringsPerMppt > 0 ? inv.MaxStringsPerMppt.ToString(CultureInfo.CurrentCulture) : "n/a")}",
            None),
    ];

    private static string Number(double value, string format) => value.ToString(format, CultureInfo.CurrentCulture);

    private static string Volt(double value) => $"{Number(value, "F1")} V";

    private static string Ampere(double value) => $"{Number(value, "F2")} A";

    private static string Watt(double value) => $"{Number(value, "F0")} W";
}
