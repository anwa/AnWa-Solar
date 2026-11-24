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

        Wechselrichter inv = result.Inverter;
        if (result.Limits is not { } limits)
        {
            return $"MPPT-Spannungsbereich des Wechselrichters ist ungültig: '{inv.MpptSpannungsbereichV}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Berechnung basierend auf:");
        sb.AppendLine($"- WR: {inv.Hersteller} {inv.Model} ({FormatInverterSummary(inv)})");
        sb.AppendLine($"- Parameter: Tmin={result.Parameters.MinTempC} °C, Tmax={result.Parameters.MaxTempC} °C, Sicherheitsmarge={result.Parameters.SicherheitsmargePct} %");
        sb.AppendLine($"- Effektive Grenzen (mit Sicherheitsmarge): {FormatLimits(limits)}");

        foreach (MpptResult mppt in result.Mppts)
        {
            sb.AppendLine(string.Empty);
            sb.AppendLine($"MPPT {mppt.MpptIndex}:");

            switch (mppt.Status)
            {
                case MpptStatus.MissingConfiguration:
                    sb.AppendLine("- Konfiguration fehlt.");
                    continue;
                case MpptStatus.Disabled:
                    sb.AppendLine("- Deaktiviert: MPPT wird in der Berechnung ausgelassen.");
                    continue;
                case MpptStatus.NoModule:
                    sb.AppendLine("- Kein PV-Modul ausgewählt.");
                    continue;
                case MpptStatus.Calculated:
                default:
                    break;
            }

            PVModule modul = mppt.Module!;
            sb.AppendLine($"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp)");
            sb.AppendLine($"- Einstellungen: Module/String={mppt.ModulesPerString}, Parallele Strings={mppt.ParallelStrings}");
            sb.AppendLine($"- String-Spannung OC: min={mppt.StringVocMinV:F1} V, max={mppt.StringVocMaxV:F1} V");
            sb.AppendLine($"- String-Spannung MPP: min={mppt.StringVmppMinV:F1} V, max={mppt.StringVmppMaxV:F1} V");
            sb.AppendLine($"- String-Ströme ISC: min={mppt.StringIscMinA:F2} A, max={mppt.StringIscMaxA:F2} A");
            sb.AppendLine($"- String-Ströme IMPP: min={mppt.StringImppMinA:F2} A, max={mppt.StringImppMaxA:F2} A");
            sb.AppendLine($"- PV-Leistung geschätzt (kalt, pro MPPT): {mppt.ArrayPowerAtTminW:F0} W");
            sb.AppendLine($"- Zulässige Module/String: {FormatModuleRange(mppt)}");
            sb.AppendLine($"- Zulässige parallele Strings/MPPT (Herstellerangabe): {FormatMaxStrings(inv)}");

            if (mppt.Violations.Count > 0)
            {
                sb.AppendLine("Hinweise:");
                foreach (LimitViolation violation in mppt.Violations)
                {
                    sb.AppendLine($"  - {FormatViolation(violation)}");
                }
            }
        }

        sb.AppendLine(string.Empty);
        sb.AppendLine($"Prüfergebnis: {(result.IsOk ? "Alle Bedingungen erfüllt." : "Einschränkungen/Verstöße vorhanden.")}");

        IReadOnlyList<string> messages = GetMessages(result);
        if (!result.IsOk && messages.Count > 0)
        {
            sb.AppendLine("Gesamthinweise:");
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
                messages.Add($"MPPT {mppt.MpptIndex}: Bitte PV-Modul auswählen.");
            }

            foreach (LimitViolation violation in mppt.Violations)
            {
                messages.Add($"MPPT {mppt.MpptIndex}: {FormatViolation(violation)}");
            }
        }

        return messages;
    }

    /// <summary>Inverter datasheet values in one line, as used in the summaries.</summary>
    public static string FormatInverterSummary(Wechselrichter inv)
    {
        ArgumentNullException.ThrowIfNull(inv);
        return $"Vdc_max={inv.MaxDcEingangsspannungV} V, Start={inv.StartspannungV} V, MPPT={inv.MpptSpannungsbereichV} V, I_in_max={inv.MaxBetriebsPvEingangsstromA} A, I_sc_max={inv.MaxEingangsKurzschlussstromA} A, MPPTs={inv.AnzahlDerMpptTrackers}, Max Strings/MPPT={inv.MaxStringsProMppt}";
    }

    /// <summary>Effective limits in one line.</summary>
    public static string FormatLimits(EffectiveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return $"Vdc_max={limits.MaxDcVoltageV:F1} V, MPPT={limits.MpptLowerBoundV:F1}–{limits.MpptUpperBoundV:F1} V, Start={limits.StartVoltageV:F1} V, I_in_max={limits.MaxInputCurrentA:F2} A, I_sc_max={limits.MaxShortCircuitCurrentA:F2} A, Pdc_MPPT={limits.DcPowerPerMpptLimitW:F0} W";
    }

    internal static string FormatModuleRange(MpptResult mppt)
        => mppt.HasValidModuleRange ? $"{mppt.MinModulesPerString} … {mppt.MaxModulesPerString}" : "kein gültiger Bereich";

    internal static string FormatMaxStrings(Wechselrichter inv)
        => inv.MaxStringsProMppt > 0 ? inv.MaxStringsProMppt.ToString(System.Globalization.CultureInfo.CurrentCulture) : "n/a";

    /// <summary>Note for a violated limit, with the recommended count.</summary>
    public static string FormatViolation(LimitViolation v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return v.Kind switch
        {
            ViolationKind.MaxDcVoltage => $"Überschreitung der max. DC-Spannung: N*VOC(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. Module/String: {v.Recommendation}.",
            ViolationKind.MpptLowerBound => $"Unterschreitung MPPT-Untergrenze bei heiß: N*VMPP(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. Module/String: {v.Recommendation}.",
            ViolationKind.MpptUpperBound => $"Überschreitung MPPT-Obergrenze bei kalt: N*VMPP(Tmin)={v.Actual:F1} V > {v.Limit:F1} V. Max. Module/String: {v.Recommendation}.",
            ViolationKind.StartVoltage => $"Startspannung nicht erreicht: N*VMPP(Tmax)={v.Actual:F1} V < {v.Limit:F1} V. Min. Module/String: {v.Recommendation}.",
            ViolationKind.ShortCircuitCurrent => $"Kurzschlussstrom-Grenze überschritten: {v.Actual:F2} A > {v.Limit:F2} A. Empfehlung: parallele Strings reduzieren (max. {v.Recommendation}).",
            ViolationKind.InputCurrent => $"Eingangsstrom-Grenze überschritten: {v.Actual:F2} A > {v.Limit:F2} A. Empfehlung: parallele Strings reduzieren (max. {v.Recommendation}).",
            ViolationKind.MaxStringsPerMppt => $"Max. Strings/MPPT überschritten: {v.Actual:F0} > {v.Limit:F0}. Bitte reduzieren.",
            ViolationKind.DcPower => $"DC-Leistungsgrenze überschritten: {v.Actual:F0} W > {v.Limit:F0} W pro MPPT. Empfehlung: parallele Strings reduzieren (max. {v.Recommendation}).",
            _ => throw new ArgumentOutOfRangeException(nameof(v), v.Kind, "Unknown violation kind."),
        };
    }
}
