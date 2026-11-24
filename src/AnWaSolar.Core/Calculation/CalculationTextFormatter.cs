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
