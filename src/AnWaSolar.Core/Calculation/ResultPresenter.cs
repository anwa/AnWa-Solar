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

/// <summary>An inverter limit from the datasheet and after applying the safety margin.</summary>
public sealed record LimitRow(string Name, string Datasheet, string Effective);

/// <summary>Presentation of a complete calculation for the results area of the main window.</summary>
public sealed record ResultOverview(
    ResultState State,
    string Headline,
    string Details,
    IReadOnlyList<MpptView> Mppts,
    IReadOnlyList<LimitRow> Limits,
    string LimitsCaption);

/// <summary>
/// Turns a <see cref="StringCalculationResult"/> into texts and states for the results area. It only formats values
/// of the result; every limit check stays in <see cref="StringCalculator"/>. Numbers use the current culture.
/// </summary>
public static class ResultPresenter
{
    private const string None = "–";

    public static ResultOverview Present(StringCalculationResult? result)
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
        int violations = result.Mppts.Sum(m => m.Violations.Count);
        int[] withoutModule = result.Mppts.Where(m => m.Status == MpptStatus.NoModule).Select(m => m.MpptIndex).ToArray();
        int calculated = result.Mppts.Count(m => m.Status == MpptStatus.Calculated);

        (ResultState state, string headline) = (violations, withoutModule.Length, calculated) switch
        {
            ( > 0, _, _) => (ResultState.Error, violations == 1 ? "1 limit violated" : $"{violations} limits violated"),
            (_, > 0, _) => (ResultState.Warning, withoutModule.Length == 1
                ? $"MPPT {withoutModule[0]} has no PV module"
                : $"MPPTs {string.Join(", ", withoutModule)} have no PV module"),
            (_, _, 0) => (ResultState.Neutral, "All MPPTs are disabled"),
            _ => (ResultState.Ok, "All limits are met"),
        };

        CalculationParameters p = result.Parameters;
        string details = $"{inv.Manufacturer} {inv.Model} · cell temperature {Number(p.MinTempC, "0.#")} … {Number(p.MaxTempC, "0.#")} °C · safety margin {Number(p.SafetyMarginPct, "0.#")} %";

        return new ResultOverview(state, headline, details, mppts, PresentLimits(inv, limits),
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
