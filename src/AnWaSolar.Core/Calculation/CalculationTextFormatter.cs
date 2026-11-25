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
            return $"The MPPT voltage range of the inverter is invalid: '{inv.MpptVoltageRangeV}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Calculation based on:");
        sb.AppendLine($"- Inverter: {inv.Manufacturer} {inv.Model} ({FormatInverterSummary(inv)})");
        sb.AppendLine($"- Parameters: cell temperature Tmin={result.Parameters.MinTempC} °C, Tmax={result.Parameters.MaxTempC} °C, safety margin={result.Parameters.SafetyMarginPct} %");
        sb.AppendLine($"- Effective limits (with safety margin): {FormatLimits(limits)}");

        foreach (MpptResult mppt in result.Mppts)
        {
            sb.AppendLine(string.Empty);
            sb.AppendLine($"MPPT {mppt.MpptIndex}:");

            switch (mppt.Status)
            {
                case MpptStatus.MissingConfiguration:
                    sb.AppendLine("- Configuration missing.");
                    continue;
                case MpptStatus.Disabled:
                    sb.AppendLine("- Disabled: this MPPT is skipped by the calculation.");
                    continue;
                case MpptStatus.NoModule:
                    sb.AppendLine("- No PV module selected.");
                    continue;
                case MpptStatus.Calculated:
                default:
                    break;
            }

            PVModule module = mppt.Module!;
            sb.AppendLine($"- Module: {module.Manufacturer} {module.Model} (Pmax={module.PmaxW} W)");
            sb.AppendLine($"- Settings: modules per string={mppt.ModulesPerString}, parallel strings={mppt.ParallelStrings}");
            sb.AppendLine($"- String voltage Voc: min={mppt.StringVocMinV:F1} V, max={mppt.StringVocMaxV:F1} V");
            sb.AppendLine($"- String voltage Vmpp: min={mppt.StringVmppMinV:F1} V, max={mppt.StringVmppMaxV:F1} V");
            sb.AppendLine($"- String current Isc: min={mppt.StringIscMinA:F2} A, max={mppt.StringIscMaxA:F2} A");
            sb.AppendLine($"- String current Impp: min={mppt.StringImppMinA:F2} A, max={mppt.StringImppMaxA:F2} A");
            sb.AppendLine($"- Estimated PV power (cold, per MPPT): {mppt.ArrayPowerAtTminW:F0} W");
            sb.AppendLine($"- Allowed modules per string: {FormatModuleRange(mppt)}");
            sb.AppendLine($"- Allowed parallel strings per MPPT (datasheet): {FormatMaxStrings(inv)}");

            if (mppt.Violations.Count > 0)
            {
                sb.AppendLine("Notes:");
                foreach (LimitViolation violation in mppt.Violations)
                {
                    sb.AppendLine($"  - {FormatViolation(violation)}");
                }
            }
        }

        if (result.Ac is { } ac)
        {
            sb.AppendLine(string.Empty);
            sb.AppendLine("AC side and grid connection:");
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
        sb.AppendLine($"Result: {(result.IsOk ? "All conditions are met." : "Restrictions or violations found.")}");

        IReadOnlyList<string> messages = GetMessages(result);
        if (!result.IsOk && messages.Count > 0)
        {
            sb.AppendLine("Summary of notes:");
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
            bat.AppendLine("Battery storage:");
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
        sb.AppendLine("Cables and fuses:");
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
            AcCheckKind.AcValuesMissing => $"The master data of the inverter has no {string.Join(", ", ac.MissingValues)}. Import its datasheet to check the AC side completely.",
            AcCheckKind.DcAcRatioLow => string.Create(c, $"DC/AC ratio {check.Actual:F2} is below {check.Limit:F2}: the inverter is large for the PV power."),
            AcCheckKind.DcAcRatioHigh => string.Create(c, $"DC/AC ratio {check.Actual:F2} is above {check.Limit:F2}: on sunny days the inverter limits the power (clipping)."),
            AcCheckKind.UnbalancedLoad => string.Create(c, $"Single-phase inverter with {check.Actual / 1000:F1} kVA exceeds the unbalanced load limit of {check.Limit / 1000:F1} kVA."),
            AcCheckKind.RegistrationLimit => string.Create(c, $"Apparent power {check.Actual / 1000:F1} kVA is above {check.Limit / 1000:F1} kVA: the simplified grid registration does not apply; ask the grid operator for the procedure."),
            AcCheckKind.FeedInLimit => string.Create(c, $"Feed-in is limited to {check.Actual / 1000:F1} kW ({ac.FeedInCapW / Math.Max(1, ac.PvPeakPowerW) * 100:F0} % of the PV peak power); above that the inverter reduces its output."),
            _ => throw new ArgumentOutOfRangeException(nameof(check), check.Kind, "Unknown AC check."),
        };
    }

    /// <summary>Values of the AC side in one line each.</summary>
    public static IReadOnlyList<string> FormatAcValues(AcCalculationResult ac)
    {
        ArgumentNullException.ThrowIfNull(ac);
        CultureInfo c = CultureInfo.CurrentCulture;
        static string Or(double? value, Func<double, string> format) => value is { } v ? format(v) : "not stated";
        return
        [
            string.Create(c, $"PV peak power (STC): {ac.PvPeakPowerW / 1000:F2} kWp"),
            $"Rated AC power: {Or(ac.RatedAcPowerW, v => string.Create(c, $"{v / 1000:F1} kW"))}",
            $"DC/AC ratio: {Or(ac.DcAcRatio, v => v.ToString("F2", c))}",
            $"Max. AC apparent power: {Or(ac.ApparentPowerVA, v => string.Create(c, $"{v / 1000:F1} kVA"))}",
            $"AC phases: {(ac.Phases is { } p ? p.ToString(c) : "not stated")}",
            $"AC current per phase: rated {Or(ac.RatedAcCurrentA, v => string.Create(c, $"{v:F1} A"))}, max. {Or(ac.MaxAcCurrentA, v => string.Create(c, $"{v:F1} A"))}",
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
                messages.Add($"MPPT {mppt.MpptIndex}: Please select a PV module.");
            }

            foreach (LimitViolation violation in mppt.Violations)
            {
                messages.Add($"MPPT {mppt.MpptIndex}: {FormatViolation(violation)}");
            }
        }

        if (result.Ac is { } ac)
        {
            messages.AddRange(ac.Checks.Where(c => c.State != ResultState.Neutral).Select(c => $"AC side: {FormatAcCheck(c, ac)}"));
        }

        return messages;
    }

    /// <summary>Inverter datasheet values in one line, as used in the summaries.</summary>
    public static string FormatInverterSummary(Inverter inv)
    {
        ArgumentNullException.ThrowIfNull(inv);
        return $"Vdc_max={inv.MaxDcInputVoltageV} V, Start={inv.StartVoltageV} V, MPPT={inv.MpptVoltageRangeV} V, I_in_max={inv.MaxInputCurrentA} A, I_sc_max={inv.MaxShortCircuitCurrentA} A, MPPTs={inv.MpptCount}, max. strings/MPPT={inv.MaxStringsPerMppt}";
    }

    /// <summary>Effective limits in one line.</summary>
    public static string FormatLimits(EffectiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return $"Vdc_max={limits.MaxDcVoltageV:F1} V, MPPT={limits.MpptLowerBoundV:F1}–{limits.MpptUpperBoundV:F1} V, Start={limits.StartVoltageV:F1} V, I_in_max={limits.MaxInputCurrentA:F2} A, I_sc_max={limits.MaxShortCircuitCurrentA:F2} A, Pdc_MPPT={limits.DcPowerPerMpptLimitW:F0} W";
    }

    internal static string FormatModuleRange(MpptResult mppt)
        => mppt.HasValidModuleRange ? $"{mppt.MinModulesPerString} … {mppt.MaxModulesPerString}" : "no valid range";

    internal static string FormatMaxStrings(Inverter inv)
        => inv.MaxStringsPerMppt > 0 ? inv.MaxStringsPerMppt.ToString(CultureInfo.CurrentCulture) : "n/a";

    /// <summary>Note for a violated limit, with the recommended count.</summary>
    public static string FormatViolation(LimitViolation v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return v.Kind switch
        {
            ViolationKind.MaxDcVoltage => $"Max. DC voltage exceeded: N×Voc(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. modules per string: {v.Recommendation}.",
            ViolationKind.MpptLowerBound => $"Below the MPPT lower bound when hot: N×Vmpp(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. modules per string: {v.Recommendation}.",
            ViolationKind.MpptUpperBound => $"Above the MPPT upper bound when cold: N×Vmpp(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. modules per string: {v.Recommendation}.",
            ViolationKind.StartVoltage => $"Start voltage not reached: N×Vmpp(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. modules per string: {v.Recommendation}.",
            ViolationKind.ShortCircuitCurrent => $"Short-circuit current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A. Reduce parallel strings (max. {v.Recommendation}).",
            ViolationKind.InputCurrent => $"Input current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A. Reduce parallel strings (max. {v.Recommendation}).",
            ViolationKind.MaxStringsPerMppt => $"Max. strings per MPPT exceeded: {v.Actual:F0} > {v.Limit:F0}. Please reduce.",
            ViolationKind.DcPower => $"DC power limit exceeded: {v.Actual:F0} W > {v.Limit:F0} W per MPPT. Reduce parallel strings (max. {v.Recommendation}).",
            _ => throw new ArgumentOutOfRangeException(nameof(v), v.Kind, "Unknown violation kind."),
        };
    }
}
