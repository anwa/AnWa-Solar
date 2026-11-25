using System.Globalization;
using System.Text;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Core.Calculation;

/// <summary>
/// Formats a <see cref="StringCalculationResult"/> as the plain text shown in the results section of the main window.
/// Numbers are formatted with the current culture.
/// </summary>
public static class CalculationTextFormatter
{
    /// <summary>Full text of the results section.</summary>
    public static string Format(StringCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Inverter inv = result.Inverter;
        if (result.Limits is not { } limits)
        {
            return L.F($"The MPPT voltage range of the inverter is invalid: '{inv.MpptVoltageRangeV}'.");
        }

        var sb = new StringBuilder();
        sb.AppendLine(L.T("Calculation based on:"));
        sb.AppendLine(L.F($"- Inverter: {inv.Manufacturer} {inv.Model} ({FormatInverterSummary(inv)})"));
        sb.AppendLine(L.F($"- Parameters: cell temperature Tmin={result.Parameters.MinTempC} °C, Tmax={result.Parameters.MaxTempC} °C, safety margin={result.Parameters.SafetyMarginPct} %"));
        sb.AppendLine(L.F($"- Effective limits (with safety margin): {FormatLimits(limits)}"));

        foreach (MpptResult mppt in result.Mppts)
        {
            sb.AppendLine(string.Empty);
            sb.AppendLine(L.F($"MPPT {mppt.MpptIndex}:"));

            switch (mppt.Status)
            {
                case MpptStatus.MissingConfiguration:
                    sb.AppendLine(L.T("- Configuration missing."));
                    continue;
                case MpptStatus.Disabled:
                    sb.AppendLine(L.T("- Disabled: this MPPT is skipped by the calculation."));
                    continue;
                case MpptStatus.NoModule:
                    sb.AppendLine(L.T("- No PV module selected."));
                    continue;
                case MpptStatus.Calculated:
                default:
                    break;
            }

            PVModule module = mppt.Module!;
            sb.AppendLine(L.F($"- Module: {module.Manufacturer} {module.Model} (Pmax={module.PmaxW} W)"));
            sb.AppendLine(L.F($"- Settings: modules per string={mppt.ModulesPerString}, parallel strings={mppt.ParallelStrings}"));
            sb.AppendLine(L.F($"- String voltage Voc: min={mppt.StringVocMinV:F1} V, max={mppt.StringVocMaxV:F1} V"));
            sb.AppendLine(L.F($"- String voltage Vmpp: min={mppt.StringVmppMinV:F1} V, max={mppt.StringVmppMaxV:F1} V"));
            sb.AppendLine(L.F($"- String current Isc: min={mppt.StringIscMinA:F2} A, max={mppt.StringIscMaxA:F2} A"));
            sb.AppendLine(L.F($"- String current Impp: min={mppt.StringImppMinA:F2} A, max={mppt.StringImppMaxA:F2} A"));
            sb.AppendLine(L.F($"- Estimated PV power (cold, per MPPT): {mppt.ArrayPowerAtTminW:F0} W"));
            sb.AppendLine(L.F($"- Allowed modules per string: {FormatModuleRange(mppt)}"));
            sb.AppendLine(L.F($"- Allowed parallel strings per MPPT (datasheet): {FormatMaxStrings(inv)}"));

            if (mppt.Violations.Count > 0)
            {
                sb.AppendLine(L.T("Notes:"));
                foreach (LimitViolation violation in mppt.Violations)
                {
                    sb.AppendLine($"  - {FormatViolation(violation)}");
                }
            }
        }

        if (result.Ac is { } ac)
        {
            sb.AppendLine(string.Empty);
            sb.AppendLine(L.T("AC side and grid connection:"));
            foreach (string line in FormatAcValues(ac))
            {
                sb.AppendLine($"- {line}");
            }

            foreach (AcCheck check in ac.Checks)
            {
                sb.AppendLine($"  - {FormatAcCheck(check, ac)}");
            }
        }

        sb.AppendLine(string.Empty);
        sb.AppendLine(L.F($"Result: {(result.IsOk ? L.T("All conditions are met.") : L.T("Restrictions or violations found."))}"));

        IReadOnlyList<string> messages = GetMessages(result);
        if (!result.IsOk && messages.Count > 0)
        {
            sb.AppendLine(L.T("Summary of notes:"));
            foreach (string message in messages)
            {
                sb.AppendLine($"- {message}");
            }
        }

        return sb.ToString();
    }

    /// <summary>Text of the results including the cable and fuse sizing and, when planned, the battery check.</summary>
    public static string Format(StringCalculationResult result, CableCalculationResult? cables, BatteryCalculationResult? battery = null)
    {
        string text = Format(result);
        if (battery is not null)
        {
            var bat = new StringBuilder(text);
            bat.AppendLine(string.Empty);
            bat.AppendLine(L.T("Battery storage:"));
            foreach (string line in BatteryTexts.Values(battery, result.Inverter))
            {
                bat.AppendLine($"- {line}");
            }

            foreach (BatteryCheck check in battery.Checks)
            {
                bat.AppendLine($"- {BatteryTexts.Format(check, battery)}");
            }

            text = bat.ToString();
        }

        if (cables is null)
        {
            return text;
        }

        var sb = new StringBuilder(text);
        sb.AppendLine(string.Empty);
        sb.AppendLine(L.T("Cables and fuses:"));
        if (!cables.TablesConfirmed)
        {
            sb.AppendLine($"- {CableTexts.Unconfirmed}");
        }

        foreach (DcCableResult d in cables.Dc)
        {
            sb.AppendLine($"- {CableTexts.Dc(d)}");
        }

        if (cables.Ac is { } ac)
        {
            sb.AppendLine($"- {CableTexts.Ac(ac)}");
        }

        if (cables.Battery is { } batteryCables)
        {
            if (!cables.BatteryTablesConfirmed)
            {
                sb.AppendLine($"- {CableTexts.BatteryUnconfirmed}");
            }

            sb.AppendLine($"- {CableTexts.Battery(batteryCables)}");
            sb.AppendLine($"- {CableTexts.Run(batteryCables.BatteryCables)}");
            if (batteryCables.MainCable is { } main)
            {
                sb.AppendLine($"- {CableTexts.Run(main)}");
            }
        }

        return sb.ToString();
    }

    /// <summary>Note for a check of the AC side.</summary>
    public static string FormatAcCheck(AcCheck check, AcCalculationResult ac)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(ac);
        CultureInfo c = CultureInfo.CurrentCulture;
        return check.Kind switch
        {
            AcCheckKind.AcValuesMissing => L.F($"The master data of the inverter has no {string.Join(", ", ac.MissingValues)}. Import its datasheet to check the AC side completely."),
            AcCheckKind.DcAcRatioLow => L.F($"DC/AC ratio {check.Actual:F2} is below {check.Limit:F2}: the inverter is large for the PV power."),
            AcCheckKind.DcAcRatioHigh => L.F($"DC/AC ratio {check.Actual:F2} is above {check.Limit:F2}: on sunny days the inverter limits the power (clipping)."),
            AcCheckKind.UnbalancedLoad => L.F($"Single-phase inverter with {check.Actual / 1000:F1} kVA exceeds the unbalanced load limit of {check.Limit / 1000:F1} kVA."),
            AcCheckKind.RegistrationLimit => L.F($"Apparent power {check.Actual / 1000:F1} kVA is above {check.Limit / 1000:F1} kVA: the simplified grid registration does not apply; ask the grid operator for the procedure."),
            AcCheckKind.FeedInLimit => L.F($"Feed-in is limited to {check.Actual / 1000:F1} kW ({ac.FeedInCapW / Math.Max(1, ac.PvPeakPowerW) * 100:F0} % of the PV peak power); above that the inverter reduces its output."),
            _ => throw new ArgumentOutOfRangeException(nameof(check), check.Kind, "Unknown AC check."),
        };
    }

    /// <summary>Values of the AC side in one line each.</summary>
    public static IReadOnlyList<string> FormatAcValues(AcCalculationResult ac)
    {
        ArgumentNullException.ThrowIfNull(ac);
        CultureInfo c = CultureInfo.CurrentCulture;
        static string Or(double? value, Func<double, string> format) => value is { } v ? format(v) : L.T("not stated");
        return
        [
            L.F($"PV peak power (STC): {ac.PvPeakPowerW / 1000:F2} kWp"),
            L.F($"Rated AC power: {Or(ac.RatedAcPowerW, v => string.Create(c, $"{v / 1000:F1} kW"))}"),
            L.F($"DC/AC ratio: {Or(ac.DcAcRatio, v => v.ToString("F2", c))}"),
            L.F($"Max. AC apparent power: {Or(ac.ApparentPowerVA, v => string.Create(c, $"{v / 1000:F1} kVA"))}"),
            L.F($"AC phases: {(ac.Phases is { } p ? p.ToString(c) : L.T("not stated"))}"),
            L.F($"AC current per phase: rated {Or(ac.RatedAcCurrentA, v => string.Create(c, $"{v:F1} A"))}, max. {Or(ac.MaxAcCurrentA, v => string.Create(c, $"{v:F1} A"))}"),
        ];
    }

    /// <summary>All notes of the calculation, each prefixed with its MPPT ("MPPT 1: …").</summary>
    public static IReadOnlyList<string> GetMessages(StringCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var messages = new List<string>();
        foreach (MpptResult mppt in result.Mppts)
        {
            if (mppt.Status == MpptStatus.NoModule)
            {
                messages.Add(L.F($"MPPT {mppt.MpptIndex}: Please select a PV module."));
            }

            foreach (LimitViolation violation in mppt.Violations)
            {
                messages.Add(L.F($"MPPT {mppt.MpptIndex}: {FormatViolation(violation)}"));
            }
        }

        if (result.Ac is { } ac)
        {
            messages.AddRange(ac.Checks.Where(c => c.State != ResultState.Neutral).Select(c => L.F($"AC side: {FormatAcCheck(c, ac)}")));
        }

        return messages;
    }

    /// <summary>Inverter datasheet values in one line, as used in the summaries.</summary>
    public static string FormatInverterSummary(Inverter inv)
    {
        ArgumentNullException.ThrowIfNull(inv);
        return L.F($"Vdc_max={inv.MaxDcInputVoltageV} V, Start={inv.StartVoltageV} V, MPPT={inv.MpptVoltageRangeV} V, I_in_max={inv.MaxInputCurrentA} A, I_sc_max={inv.MaxShortCircuitCurrentA} A, MPPTs={inv.MpptCount}, max. strings/MPPT={inv.MaxStringsPerMppt}");
    }

    /// <summary>Effective limits in one line.</summary>
    public static string FormatLimits(EffectiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return L.F($"Vdc_max={limits.MaxDcVoltageV:F1} V, MPPT={limits.MpptLowerBoundV:F1}–{limits.MpptUpperBoundV:F1} V, Start={limits.StartVoltageV:F1} V, I_in_max={limits.MaxInputCurrentA:F2} A, I_sc_max={limits.MaxShortCircuitCurrentA:F2} A, Pdc_MPPT={limits.DcPowerPerMpptLimitW:F0} W");
    }

    internal static string FormatModuleRange(MpptResult mppt)
        => mppt.HasValidModuleRange ? $"{mppt.MinModulesPerString} … {mppt.MaxModulesPerString}" : L.T("no valid range");

    internal static string FormatMaxStrings(Inverter inv)
        => inv.MaxStringsPerMppt > 0 ? inv.MaxStringsPerMppt.ToString(CultureInfo.CurrentCulture) : L.T("n/a");

    /// <summary>Note for a violated limit, with the recommended count.</summary>
    public static string FormatViolation(LimitViolation v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return v.Kind switch
        {
            ViolationKind.MaxDcVoltage => L.F($"Max. DC voltage exceeded: N×Voc(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. modules per string: {v.Recommendation}."),
            ViolationKind.MpptLowerBound => L.F($"Below the MPPT lower bound when hot: N×Vmpp(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. modules per string: {v.Recommendation}."),
            ViolationKind.MpptUpperBound => L.F($"Above the MPPT upper bound when cold: N×Vmpp(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. modules per string: {v.Recommendation}."),
            ViolationKind.StartVoltage => L.F($"Start voltage not reached: N×Vmpp(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. modules per string: {v.Recommendation}."),
            ViolationKind.ShortCircuitCurrent => L.F($"Short-circuit current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A. Reduce parallel strings (max. {v.Recommendation})."),
            ViolationKind.InputCurrent => L.F($"Input current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A. Reduce parallel strings (max. {v.Recommendation})."),
            ViolationKind.MaxStringsPerMppt => L.F($"Max. strings per MPPT exceeded: {v.Actual:F0} > {v.Limit:F0}. Please reduce."),
            ViolationKind.DcPower => L.F($"DC power limit exceeded: {v.Actual:F0} W > {v.Limit:F0} W per MPPT. Reduce parallel strings (max. {v.Recommendation})."),
            _ => throw new ArgumentOutOfRangeException(nameof(v), v.Kind, "Unknown violation kind."),
        };
    }
}
