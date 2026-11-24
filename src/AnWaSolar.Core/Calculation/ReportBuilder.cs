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
        md.AppendLine("# Bericht: PV-String-Auslegung");
        md.AppendLine($"Erstellt am {createdAt:yyyy-MM-dd HH:mm}");
        md.AppendLine();

        if (result is null)
        {
            md.AppendLine("Hinweis: Bitte zuerst einen Wechselrichter auswählen, um einen vollständigen Bericht zu erzeugen.");
            return md.ToString();
        }

        Wechselrichter inv = result.Inverter;

        md.AppendLine("## Parameter und Rahmenbedingungen");
        md.AppendLine($"- Temperaturbereich: Tmin={parameters.MinTempC} °C, Tmax={parameters.MaxTempC} °C");
        md.AppendLine($"- Sicherheitsmarge: {parameters.SicherheitsmargePct} %");
        md.AppendLine();

        md.AppendLine("## Wechselrichter");
        md.AppendLine($"- Hersteller/Modell: {inv.Hersteller} {inv.Model}");
        md.AppendLine($"- MPPT-Spannungsbereich: {inv.MpptSpannungsbereichV} V");
        md.AppendLine($"- Max. DC-Eingangsspannung: {inv.MaxDcEingangsspannungV} V");
        md.AppendLine($"- Startspannung: {inv.StartspannungV} V");
        md.AppendLine($"- Max. Betriebs-PV-Eingangsstrom: {inv.MaxBetriebsPvEingangsstromA} A");
        md.AppendLine($"- Max. Eingangs-Kurzschlussstrom: {inv.MaxEingangsKurzschlussstromA} A");
        md.AppendLine($"- Anzahl MPPT: {inv.AnzahlDerMpptTrackers}, Max. Strings pro MPPT: {inv.MaxStringsProMppt}");
        md.AppendLine();

        md.AppendLine("### Berechnungshinweise");
        md.AppendLine("- Spannungen und Ströme werden temperaturabhängig auf Basis der angegebenen Temperaturkoeffizienten abgeschätzt.");
        md.AppendLine("- Sicherheitsmargen werden auf Grenzwerte angewendet: z. B. Vdc_max reduziert, Startspannung und MPPT-Untergrenze erhöht.");
        md.AppendLine("- Strings in Serie erhöhen die Spannung proportional zur Modulanzahl, der Strom bleibt gleich. Parallelschaltung addiert Ströme.");
        md.AppendLine("$V_{oc} (T_{STC}) * (1 + ((T_{min} - 25°C) * Temperaturkoeffizient))$");
        md.AppendLine();

        if (result.Limits is not { } limits)
        {
            md.AppendLine("Warnung: MPPT-Spannungsbereich des Wechselrichters ist ungültig. Bericht ggf. unvollständig.");
            return md.ToString();
        }

        md.AppendLine("### Effektive Grenzen (mit Sicherheitsmarge)");
        md.AppendLine($"- {CalculationTextFormatter.FormatLimits(limits)}");
        md.AppendLine();

        foreach (MpptResult mppt in result.Mppts)
        {
            md.AppendLine($"## MPPT {mppt.MpptIndex}");

            if (mppt.Status == MpptStatus.Disabled)
            {
                md.AppendLine("- Deaktiviert: MPPT wird in der Berechnung ausgelassen.");
                md.AppendLine();
                continue;
            }

            if (mppt.Status != MpptStatus.Calculated)
            {
                md.AppendLine("- Kein PV-Modul ausgewählt.");
                md.AppendLine();
                continue;
            }

            PVModule modul = mppt.Module!;
            md.AppendLine($"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp, UMPP={modul.SpannungImMppUmppV:F2} V, IMPP={modul.StromImMppImppA:F2} A, UOC={modul.LeerlaufspannungUocV:F2} V, ISC={modul.KurzschlusstromIscA:F2} A)");
            md.AppendLine($"- Einstellungen: Module/String={mppt.ModulesPerString}, Parallele Strings={mppt.ParallelStrings}");
            md.AppendLine();

            md.AppendLine("### Ergebnisse (kondensiert)");
            md.AppendLine($"- String-Spannung OC: min={mppt.StringVocMinV:F1} V, max={mppt.StringVocMaxV:F1} V");
            md.AppendLine($"- String-Spannung MPP: min={mppt.StringVmppMinV:F1} V, max={mppt.StringVmppMaxV:F1} V");
            md.AppendLine($"- String-Ströme ISC: min={mppt.StringIscMinA:F2} A, max={mppt.StringIscMaxA:F2} A");
            md.AppendLine($"- String-Ströme IMPP: min={mppt.StringImppMinA:F2} A, max={mppt.StringImppMaxA:F2} A");
            md.AppendLine($"- Gesamtströme am MPPT: ISC min={mppt.ArrayIscMinA:F2} A / max={mppt.ArrayIscMaxA:F2} A; IMPP min={mppt.ArrayImppMinA:F2} A / max={mppt.ArrayImppMaxA:F2} A");
            md.AppendLine();

            md.AppendLine("### Empfehlungen und Grenzprüfung");
            md.AppendLine($"- Zulässige Module/String: {CalculationTextFormatter.FormatModuleRange(mppt)}");
            md.AppendLine($"- Zulässige parallele Strings/MPPT (Herstellerangabe): {CalculationTextFormatter.FormatMaxStrings(inv)}");
            md.AppendLine();

            md.AppendLine("### Detail-Hinweise");
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
        ViolationKind.MaxDcVoltage => $"Überschreitung der max. DC-Spannung bei Tmin: {v.Actual:F1} V > {v.Limit:F1} V.",
        ViolationKind.MpptLowerBound => $"Unterschreitung MPPT-Untergrenze bei Tmax: {v.Actual:F1} V < {v.Limit:F1} V.",
        ViolationKind.MpptUpperBound => $"Überschreitung MPPT-Obergrenze bei Tmin: {v.Actual:F1} V > {v.Limit:F1} V.",
        ViolationKind.StartVoltage => $"Startspannung nicht erreicht: {v.Actual:F1} V < {v.Limit:F1} V.",
        ViolationKind.ShortCircuitCurrent => $"Kurzschlussstrom-Grenze überschritten: {v.Actual:F2} A > {v.Limit:F2} A.",
        ViolationKind.InputCurrent => $"Eingangsstrom-Grenze überschritten: {v.Actual:F2} A > {v.Limit:F2} A.",
        ViolationKind.MaxStringsPerMppt => $"Max. Strings/MPPT überschritten: {v.Actual:F0} > {v.Limit:F0}.",
        ViolationKind.DcPower => $"DC-Leistungsgrenze überschritten: {v.Actual:F0} W > {v.Limit:F0} W pro MPPT.",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v.Kind, "Unknown violation kind."),
    };
}
