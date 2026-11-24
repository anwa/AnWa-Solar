using System.Text;

using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>
/// Builds the Markdown report of a string calculation. Numbers are formatted with the current culture.
/// </summary>
public static class ReportBuilder
{
    /// <summary>Report for a calculation, or a hint when no inverter is selected.</summary>
    /// <param name="result">Calculation result; null when no inverter is selected.</param>
    /// <param name="parameters">Parameters shown in the report.</param>
    /// <param name="createdAt">Creation time printed in the header.</param>
    public static string Build(StringCalculationResult? result, CalculationParameters parameters, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var md = new StringBuilder();
        md.AppendLine("# Report: PV string design");
        md.AppendLine($"Created on {createdAt:yyyy-MM-dd HH:mm}");
        md.AppendLine();

        if (result is null)
        {
            md.AppendLine("Note: Please select an inverter first to create a complete report.");
            return md.ToString();
        }

        Inverter inv = result.Inverter;

        md.AppendLine("## Parameters and conditions");
        md.AppendLine($"- Cell temperature range: Tmin={parameters.MinTempC} °C, Tmax={parameters.MaxTempC} °C");
        md.AppendLine($"- Safety margin: {parameters.SafetyMarginPct} %");
        md.AppendLine();

        md.AppendLine("## Inverter");
        md.AppendLine($"- Manufacturer/model: {inv.Manufacturer} {inv.Model}");
        md.AppendLine($"- MPPT voltage range: {inv.MpptVoltageRangeV} V");
        md.AppendLine($"- Max. DC input voltage: {inv.MaxDcInputVoltageV} V");
        md.AppendLine($"- Start voltage: {inv.StartVoltageV} V");
        md.AppendLine($"- Max. input current per MPPT: {inv.MaxInputCurrentA} A");
        md.AppendLine($"- Max. short-circuit current per MPPT: {inv.MaxShortCircuitCurrentA} A");
        md.AppendLine($"- MPPT count: {inv.MpptCount}, max. strings per MPPT: {inv.MaxStringsPerMppt}");
        md.AppendLine();

        md.AppendLine("### Calculation notes");
        md.AppendLine("- Voltages and currents are estimated for the temperature range from the temperature coefficients of the datasheet.");
        md.AppendLine("- Tmin and Tmax are cell temperatures. Vmpp uses the Vmpp temperature coefficient of the module; when the datasheet does not state it, the Pmax coefficient is used as an approximation.");
        md.AppendLine("- The safety margin tightens the limits: for example Vdc_max is reduced, the start voltage and the MPPT lower bound are raised.");
        md.AppendLine("- Modules in series add their voltages while the current stays the same; parallel strings add their currents.");
        md.AppendLine("- $V_{oc}(T) = V_{oc}(25\\,^{\\circ}\\mathrm{C}) \\cdot (1 + \\alpha \\cdot (T - 25\\,^{\\circ}\\mathrm{C}))$");
        md.AppendLine();

        if (result.Limits is not { } limits)
        {
            md.AppendLine("Warning: The MPPT voltage range of the inverter is invalid. The report may be incomplete.");
            return md.ToString();
        }

        md.AppendLine("### Effective limits (with safety margin)");
        md.AppendLine($"- {CalculationTextFormatter.FormatLimits(limits)}");
        md.AppendLine();

        foreach (MpptResult mppt in result.Mppts)
        {
            md.AppendLine($"## MPPT {mppt.MpptIndex}");

            if (mppt.Status == MpptStatus.Disabled)
            {
                md.AppendLine("- Disabled: this MPPT is skipped by the calculation.");
                md.AppendLine();
                continue;
            }

            if (mppt.Status != MpptStatus.Calculated)
            {
                md.AppendLine("- No PV module selected.");
                md.AppendLine();
                continue;
            }

            PVModule module = mppt.Module!;
            md.AppendLine($"- Module: {module.Manufacturer} {module.Model} (Pmax={module.PmaxW} W, Vmpp={module.VmppV:F2} V, Impp={module.ImppA:F2} A, Voc={module.VocV:F2} V, Isc={module.IscA:F2} A)");
            md.AppendLine($"- Settings: modules per string={mppt.ModulesPerString}, parallel strings={mppt.ParallelStrings}");
            ModuleTemperatureValues t = mppt.Temperatures!;
            md.AppendLine($"- Vmpp temperature coefficient: {t.VmppCoefficientPctPerC} %/°C{(t.VmppCoefficientApproximated ? " (approximation: Pmax coefficient)" : string.Empty)}");
            md.AppendLine();

            md.AppendLine("### Results");
            md.AppendLine($"- String voltage Voc: min={mppt.StringVocMinV:F1} V, max={mppt.StringVocMaxV:F1} V");
            md.AppendLine($"- String voltage Vmpp: min={mppt.StringVmppMinV:F1} V, max={mppt.StringVmppMaxV:F1} V");
            md.AppendLine($"- String current Isc: min={mppt.StringIscMinA:F2} A, max={mppt.StringIscMaxA:F2} A");
            md.AppendLine($"- String current Impp: min={mppt.StringImppMinA:F2} A, max={mppt.StringImppMaxA:F2} A");
            md.AppendLine($"- Total MPPT current: Isc min={mppt.ArrayIscMinA:F2} A / max={mppt.ArrayIscMaxA:F2} A; Impp min={mppt.ArrayImppMinA:F2} A / max={mppt.ArrayImppMaxA:F2} A");
            md.AppendLine();

            md.AppendLine("### Recommendations and limit checks");
            md.AppendLine($"- Allowed modules per string: {CalculationTextFormatter.FormatModuleRange(mppt)}");
            md.AppendLine($"- Allowed parallel strings per MPPT (datasheet): {CalculationTextFormatter.FormatMaxStrings(inv)}");
            md.AppendLine();

            md.AppendLine("### Details");
            foreach (LimitViolation violation in mppt.Violations)
            {
                md.AppendLine($"- {FormatViolation(violation)}");
            }

            md.AppendLine();
        }

        return md.ToString();
    }

    private static string FormatViolation(LimitViolation v) => v.Kind switch
    {
        ViolationKind.MaxDcVoltage => $"Max. DC voltage exceeded at Tmin: {v.Actual:F1} V > {v.Limit:F1} V.",
        ViolationKind.MpptLowerBound => $"Below the MPPT lower bound at Tmax: {v.Actual:F1} V < {v.Limit:F1} V.",
        ViolationKind.MpptUpperBound => $"Above the MPPT upper bound at Tmin: {v.Actual:F1} V > {v.Limit:F1} V.",
        ViolationKind.StartVoltage => $"Start voltage not reached: {v.Actual:F1} V < {v.Limit:F1} V.",
        ViolationKind.ShortCircuitCurrent => $"Short-circuit current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A.",
        ViolationKind.InputCurrent => $"Input current limit exceeded: {v.Actual:F2} A > {v.Limit:F2} A.",
        ViolationKind.MaxStringsPerMppt => $"Max. strings per MPPT exceeded: {v.Actual:F0} > {v.Limit:F0}.",
        ViolationKind.DcPower => $"DC power limit exceeded: {v.Actual:F0} W > {v.Limit:F0} W per MPPT.",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v.Kind, "Unknown violation kind."),
    };
}
