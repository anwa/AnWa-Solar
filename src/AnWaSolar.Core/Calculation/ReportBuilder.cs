using System.Globalization;
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
    public static string Build(StringCalculationResult? result, CalculationParameters parameters, DateTime createdAt) =>
        Build(result, parameters, createdAt, cables: null, tables: null);

    /// <summary>Report including the cable and fuse sizing.</summary>
    public static string Build(StringCalculationResult? result, CalculationParameters parameters, DateTime createdAt, CableCalculationResult? cables, CableTables? tables,
        BatteryCalculationResult? battery = null, Projects.ProjectInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var md = new StringBuilder();
        md.AppendLine(project is null ? "# Report: PV string design" : $"# Report: {project.Name}");
        md.AppendLine($"Created on {createdAt:yyyy-MM-dd HH:mm}");
        md.AppendLine();
        if (project is not null)
        {
            md.AppendLine("## Project");
            md.AppendLine($"- Project: {project.Name}");
            if (!string.IsNullOrWhiteSpace(project.Customer))
            {
                md.AppendLine($"- Customer: {project.Customer}");
            }

            if (!string.IsNullOrWhiteSpace(project.Address))
            {
                md.AppendLine($"- Address: {project.Address.ReplaceLineEndings(", ")}");
            }

            if (!string.IsNullOrWhiteSpace(project.Notes))
            {
                md.AppendLine();
                md.AppendLine(project.Notes.Trim());
            }

            md.AppendLine();
        }

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

        if (result.Ac is { } ac)
        {
            md.AppendLine("## AC side and grid connection");
            foreach (string line in CalculationTextFormatter.FormatAcValues(ac))
            {
                md.AppendLine($"- {line}");
            }

            md.AppendLine();
            md.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"Limits: DC/AC ratio {parameters.DcAcRatioMin:0.##} … {parameters.DcAcRatioMax:0.##}, unbalanced load of single-phase inverters {parameters.UnbalancedLoadLimitVA / 1000:0.##} kVA, simplified grid registration up to {parameters.SimplifiedRegistrationLimitVA / 1000:0.##} kVA, feed-in limit {parameters.FeedInLimitPct:0.##} % of the PV peak power."));
            md.AppendLine();
            md.AppendLine("$$\\mathrm{DC/AC} = \\frac{\\sum N_{\\mathrm{modules}} \\cdot P_{\\mathrm{max,STC}}}{P_{\\mathrm{AC,rated}}}$$");
            md.AppendLine();
            if (ac.Checks.Count > 0)
            {
                md.AppendLine("### Notes");
                foreach (AcCheck check in ac.Checks)
                {
                    md.AppendLine($"- {CalculationTextFormatter.FormatAcCheck(check, ac)}");
                }

                md.AppendLine();
            }
        }

        if (cables is not null && tables is not null)
        {
            md.AppendLine("## Cables and fuses");
            if (!cables.TablesConfirmed)
            {
                md.AppendLine($"> {CableTexts.Unconfirmed}");
                md.AppendLine();
            }

            if (cables.Battery is not null && !cables.BatteryTablesConfirmed)
            {
                md.AppendLine($"> {CableTexts.BatteryUnconfirmed}");
                md.AppendLine();
            }

            foreach (DcCableResult d in cables.Dc)
            {
                md.AppendLine($"- {CableTexts.Dc(d)}");
                foreach (ResultNote note in d.Notes)
                {
                    md.AppendLine($"  - {note.Text}");
                }
            }

            if (cables.Ac is { } acLine)
            {
                md.AppendLine($"- {CableTexts.Ac(acLine)}");
                foreach (ResultNote note in acLine.Notes)
                {
                    md.AppendLine($"  - {note.Text}");
                }
            }

            if (cables.Battery is { } batteryCables)
            {
                md.AppendLine($"- {CableTexts.Battery(batteryCables)}");
                md.AppendLine($"  - {CableTexts.Run(batteryCables.BatteryCables)}");
                if (batteryCables.MainCable is { } mainCable)
                {
                    md.AppendLine($"  - {CableTexts.Run(mainCable)}");
                }

                foreach (ResultNote note in batteryCables.Notes)
                {
                    md.AppendLine($"  - {note.Text}");
                }
            }

            md.AppendLine();
            md.AppendLine("Rules: design current of a string $I_B = 1.25 \\cdot I_{SC,STC}$; string fuses when $(N_p - 1) \\cdot 1.25 \\cdot I_{SC,STC} > I_{MOD,max,OCPR}$ with $1.5 \\cdot I_{SC} \\le I_n \\le 2.4 \\cdot I_{SC}$ and $I_n \\le I_{MOD,max,OCPR}$ (IEC 62548); AC $I_B \\le I_n \\le I_z$.");
            md.AppendLine();
            md.AppendLine("The string voltage drop includes the module leads: $\\Delta u_{leads} = \\frac{N \\cdot (l_+ + l_-) \\cdot I_{MPP}}{\\kappa \\cdot A_{lead} \\cdot U_{MPP}}$. Battery cables: $I_B \\le I_n \\le I_z$; a battery cable carries the current of one battery (at most the inverter's), the main cable from a busbar the battery current; with a busbar the voltage drop limit is shared equally by both; $\\Delta u_{bat} = \\frac{2 \\cdot L \\cdot I}{\\kappa \\cdot A \\cdot U_{bank,nominal}}$.");
            md.AppendLine();
            md.AppendLine("$$\\Delta u_{DC} = \\frac{2 \\cdot L \\cdot I_{MPP}}{\\kappa \\cdot A \\cdot U_{MPP}}, \\quad \\Delta u_{AC,3} = \\frac{\\sqrt{3} \\cdot L \\cdot I}{\\kappa \\cdot A \\cdot 400\\,\\mathrm{V}}, \\quad \\Delta u_{AC,1} = \\frac{2 \\cdot L \\cdot I}{\\kappa \\cdot A \\cdot 230\\,\\mathrm{V}}$$");
            md.AppendLine();
            md.AppendLine($"Sources: {tables.SolarCable.Source}; {tables.AcCable.Source}; {tables.Grouping.Source}; {tables.Protection.Source}; κ = {tables.CopperConductivity} m/(Ω·mm²)." + (cables.Battery is null ? string.Empty : $" Battery cables: {tables.BatteryCable.Source}."));
            md.AppendLine();
        }

        if (battery is not null)
        {
            md.AppendLine("## Battery storage");
            foreach (string line in BatteryTexts.Values(battery, inv))
            {
                md.AppendLine($"- {line}");
            }

            md.AppendLine();
            md.AppendLine("$$U_{\\mathrm{bank}} = N_s \\cdot U_{\\mathrm{module}}, \\quad I_{\\mathrm{charge}} = \\min(N_p \\cdot I_{\\mathrm{module,max}},\\ I_{\\mathrm{inverter,max}}), \\quad P = I \\cdot U_{\\mathrm{bank,nominal}}, \\quad E = N_s \\cdot N_p \\cdot E_{\\mathrm{module}}$$");
            md.AppendLine();
            md.AppendLine("The bank voltage range must lie within the battery voltage range of the inverter; the values come from the datasheets of the battery and the inverter. The safety margin of the PV calculation does not apply to the battery.");
            md.AppendLine();
            if (battery.Checks.Count > 0)
            {
                md.AppendLine("### Notes");
                foreach (BatteryCheck check in battery.Checks)
                {
                    md.AppendLine($"- {BatteryTexts.Format(check, battery)}");
                }

                md.AppendLine();
            }
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
