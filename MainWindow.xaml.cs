using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using AnWaSolar.Models;
using AnWaSolar.Services;
using Microsoft.Extensions.Logging;

namespace AnWaSolar;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly IMarkdownService _markdown;
    private readonly IDataStore _dataStore;

    public MainWindow(ILogger<MainWindow> logger, IMarkdownService markdown, IDataStore dataStore)
    {
        InitializeComponent();
        _logger = logger;
        _markdown = markdown;
        _dataStore = dataStore;

        _logger.LogInformation("Hauptfenster initialisiert.");

        ModuleCombo.ItemsSource = _dataStore.Module;
        ModuleCombo.SelectedItem = _dataStore.Module.FirstOrDefault();

        InverterCombo.ItemsSource = _dataStore.Wechselrichter;
        InverterCombo.SelectedItem = _dataStore.Wechselrichter.FirstOrDefault();
    }

    private bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
    private bool TryParseInt(string? text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) ||
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static double ApplyTempCoeff(double valueAt25, double coeffPctPerC, double tC)
    {
        // x(T) = x25 * (1 + alpha[%/°C] * (T-25) / 100)
        return valueAt25 * (1.0 + (coeffPctPerC / 100.0) * (tC - 25.0));
    }

    private static bool TryParseRangeV(string range, out double vMin, out double vMax)
    {
        vMin = vMax = 0;
        if (string.IsNullOrWhiteSpace(range)) return false;
        var parts = range.Replace(" ", "").Split('-', '–', '—');
        if (parts.Length != 2) return false;
        return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out vMin)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out vMax)
            && vMin > 0 && vMax > vMin;
    }
    private void OnConvertMarkdownClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var md = MarkdownInput.Text ?? string.Empty;
            var html = _markdown.ToHtml(md);
            HtmlOutput.Text = html;
            _logger.LogInformation("Markdown erfolgreich in HTML konvertiert. Länge={Laenge}", html.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Markdown-Konvertierung.");
            MessageBox.Show("Fehler bei der Markdown-Konvertierung. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCalculateStringClick(object sender, RoutedEventArgs e)
    {
        var modul = ModuleCombo.SelectedItem as PVModule;
        var wr = InverterCombo.SelectedItem as Wechselrichter;

        if (modul is null || wr is null)
        {
            StringCalcOutput.Text = "Bitte sowohl PV-Modul als auch Wechselrichter auswählen.";
            return;
        }

        if (!TryParseDouble(MinTempInput.Text, out var tMin) ||
            !TryParseDouble(MaxTempInput.Text, out var tMax) ||
            tMin >= tMax)
        {
            StringCalcOutput.Text = "Ungültiger Temperaturbereich. Tmin < Tmax angeben.";
            return;
        }
        if (!TryParseInt(ModuleProStringInput.Text, out var nModule) || nModule <= 0)
        {
            StringCalcOutput.Text = "Ungültige Anzahl 'Module pro String' (muss > 0 sein).";
            return;
        }
        if (!TryParseInt(ParalleleStringsInput.Text, out var nStrings) || nStrings <= 0)
        {
            StringCalcOutput.Text = "Ungültige Anzahl 'Parallele Strings' (muss > 0 sein).";
            return;
        }
        if (!TryParseDouble(SicherheitsmargeInput.Text, out var marginPct) || marginPct < 0 || marginPct > 50)
        {
            StringCalcOutput.Text = "Ungültige Sicherheitsmarge (%). Bereich 0–50 %.";
            return;
        }
        var m = marginPct / 100.0;

        if (!TryParseRangeV(wr.MpptSpannungsbereichV, out var mpptMin, out var mpptMax))
        {
            StringCalcOutput.Text = $"MPPT-Spannungsbereich des Wechselrichters ist ungültig: '{wr.MpptSpannungsbereichV}'.";
            return;
        }

        // Effektive (verschärfte) Grenzwerte mit Sicherheitsmarge
        var vdcMaxEff = wr.MaxDcEingangsspannungV * (1 - m);
        var vStartEff = wr.StartspannungV * (1 + m);
        var mpptMinEff = mpptMin * (1 + m);
        var mpptMaxEff = mpptMax * (1 - m);
        var iInMaxEff = wr.MaxBetriebsPvEingangsstromA * (1 - m);
        var iScMaxEff = wr.MaxEingangsKurzschlussstromA * (1 - m);

        // Per-MPPT-Leistungsgrenze (Annahme: Gesamtleistung verteilt sich gleichmäßig auf MPPTs)
        var mpptCount = Math.Max(1, wr.AnzahlDerMpptTrackers);
        var pDcPerMppt = wr.MaxDcEingangsleistungW / (double)mpptCount;
        var pDcPerMpptEff = pDcPerMppt * (1 - m);

        // Modulwerte bei Tmin/Tmax
        var vocTmin = ApplyTempCoeff(modul.LeerlaufspannungUocV, modul.TemperaturkoeffVocProzentProGradC, tMin);
        var vocTmax = ApplyTempCoeff(modul.LeerlaufspannungUocV, modul.TemperaturkoeffVocProzentProGradC, tMax);

        var vmpTmin = ApplyTempCoeff(modul.SpannungImMppUmppV, modul.TemperaturkoeffVocProzentProGradC, tMin);  // Näherung
        var vmpTmax = ApplyTempCoeff(modul.SpannungImMppUmppV, modul.TemperaturkoeffVocProzentProGradC, tMax);  // Näherung

        var pmaxTmin = ApplyTempCoeff(modul.NominalleistungPmaxWp, modul.TemperaturkoeffPmaxProzentProGradC, tMin);
        var pmaxTmax = ApplyTempCoeff(modul.NominalleistungPmaxWp, modul.TemperaturkoeffPmaxProzentProGradC, tMax);

        var iscTmin = ApplyTempCoeff(modul.KurzschlusstromIscA, modul.TemperaturkoeffIscProzentProGradC, tMin);
        var iscTmax = ApplyTempCoeff(modul.KurzschlusstromIscA, modul.TemperaturkoeffIscProzentProGradC, tMax);

        // IMPP aus P/V
        var imppTmin = pmaxTmin / Math.Max(0.1, vmpTmin);
        var imppTmax = pmaxTmax / Math.Max(0.1, vmpTmax);

        // String-Min/Max (Serie: Spannung skaliert mit N, Strom bleibt gleich)
        var vStringVocMin = nModule * Math.Min(vocTmin, vocTmax);
        var vStringVocMax = nModule * Math.Max(vocTmin, vocTmax);

        var vStringVmpMin = nModule * Math.Min(vmpTmin, vmpTmax);
        var vStringVmpMax = nModule * Math.Max(vmpTmin, vmpTmax);

        var iStringScMin = Math.Min(iscTmin, iscTmax);
        var iStringScMax = Math.Max(iscTmin, iscTmax);

        var iStringImppMin = Math.Min(imppTmin, imppTmax);
        var iStringImppMax = Math.Max(imppTmin, imppTmax);

        // Gesamtströme am MPPT (Parallelschaltung: Ströme addieren, Spannung bleibt wie String)
        var iArrayScMin = nStrings * iStringScMin;
        var iArrayScMax = nStrings * iStringScMax;

        var iArrayImppMin = nStrings * iStringImppMin;
        var iArrayImppMax = nStrings * iStringImppMax;

        // Prüfungen
        var messages = new List<string>();
        bool ok = true;

        // 1) Max. DC-Spannung bei Kälte
        var vStringVocTmin = nModule * vocTmin;
        if (vStringVocTmin > vdcMaxEff + 1e-6)
        {
            ok = false;
            var nMaxVoc = (int)Math.Floor(vdcMaxEff / vocTmin);
            messages.Add($"Überschreitung der max. DC-Spannung: N*VOC(Tmin)={vStringVocTmin:F1} V > {vdcMaxEff:F1} V. Max. Module pro String (Spannungsgrenze): {Math.Max(0, nMaxVoc)}.");
        }

        // 2) MPPT-Bereich (untere Grenze bei heiß, obere bei kalt)
        var vStringVmpTmax = nModule * vmpTmax;
        var vStringVmpTmin = nModule * vmpTmin;
        if (vStringVmpTmax < mpptMinEff - 1e-6)
        {
            ok = false;
            var nMinMppt = (int)Math.Ceiling(mpptMinEff / vmpTmax);
            messages.Add($"Unterschreitung MPPT-Untergrenze bei heiß: N*VMPP(Tmax)={vStringVmpTmax:F1} V < {mpptMinEff:F1} V. Min. Module pro String: {Math.Max(1, nMinMppt)}.");
        }
        if (vStringVmpTmin > mpptMaxEff + 1e-6)
        {
            ok = false;
            var nMaxMppt = (int)Math.Floor(mpptMaxEff / vmpTmin);
            messages.Add($"Überschreitung MPPT-Obergrenze bei kalt: N*VMPP(Tmin)={vStringVmpTmin:F1} V > {mpptMaxEff:F1} V. Max. Module pro String: {Math.Max(0, nMaxMppt)}.");
        }

        // 3) Startspannung (kritisch bei heiß)
        if (vStringVmpTmax < vStartEff - 1e-6)
        {
            ok = false;
            var nMinStart = (int)Math.Ceiling(vStartEff / vmpTmax);
            messages.Add($"Startspannung nicht erreicht: N*VMPP(Tmax)={vStringVmpTmax:F1} V < {vStartEff:F1} V. Min. Module pro String für Start: {Math.Max(1, nMinStart)}.");
        }

        // 4) Stromgrenzen je MPPT
        var iTotalIsc = nStrings * iscTmax;
        var iTotalImpp = nStrings * imppTmax;

        if (iTotalIsc > iScMaxEff + 1e-6)
        {
            ok = false;
            var sMaxIscViol = (int)Math.Floor(iScMaxEff / Math.Max(0.001, iscTmax));
            messages.Add($"Kurzschlussstrom-Grenze überschritten: {iTotalIsc:F2} A > {iScMaxEff:F2} A. Max. parallele Strings (ISC): {Math.Max(0, sMaxIscViol)}.");
        }
        if (iTotalImpp > iInMaxEff + 1e-6)
        {
            ok = false;
            var sMaxImppViol = (int)Math.Floor(iInMaxEff / Math.Max(0.001, imppTmax));
            messages.Add($"Eingangsstrom-Grenze überschritten: {iTotalImpp:F2} A > {iInMaxEff:F2} A. Max. parallele Strings (IMPP): {Math.Max(0, sMaxImppViol)}.");
        }

        // 5) Max. Strings pro MPPT (herstellerseitig)
        if (wr.MaxStringsProMppt > 0 && nStrings > wr.MaxStringsProMppt)
        {
            ok = false;
            messages.Add($"Max. Strings/MPPT überschritten: {nStrings} > {wr.MaxStringsProMppt}. Bitte reduzieren.");
        }

        // 6) DC-Leistungsgrenze pro MPPT
        var pStringMax = nModule * pmaxTmin;
        var pTotal = nStrings * pStringMax;
        if (pTotal > pDcPerMpptEff + 1e-6)
        {
            ok = false;
            var sMaxPowerViol = (int)Math.Floor(pDcPerMpptEff / Math.Max(1.0, pStringMax));
            messages.Add($"DC-Leistungsgrenze überschritten: {pTotal:F0} W > {pDcPerMpptEff:F0} W pro MPPT. Max. parallele Strings (Leistung): {Math.Max(0, sMaxPowerViol)}.");
        }

        // Empfehlung: zulässiger N-Bereich aus Grenzwerten
        var nMaxFromVoc = (int)Math.Floor(vdcMaxEff / vocTmin);
        var nMinFromMppt = (int)Math.Ceiling(mpptMinEff / vmpTmax);
        var nMaxFromMppt = (int)Math.Floor(mpptMaxEff / vmpTmin);
        var nMinFromStart = (int)Math.Ceiling(vStartEff / vmpTmax);

        var nLower = Math.Max(1, Math.Max(nMinFromMppt, nMinFromStart));
        var nUpper = Math.Min(nMaxFromVoc, nMaxFromMppt);

        // Empfehlung für Strings (einmal zentral berechnen, andere Namen verwenden)
        var sMaxIscAllowed = (int)Math.Floor(iScMaxEff / Math.Max(0.001, iscTmax));
        var sMaxImppAllowed = (int)Math.Floor(iInMaxEff / Math.Max(0.001, imppTmax));
        var sMaxPowerAllowed = (int)Math.Floor(pDcPerMpptEff / Math.Max(1.0, pStringMax));
        var sMaxBySpec = wr.MaxStringsProMppt > 0 ? wr.MaxStringsProMppt : int.MaxValue;
        var sUpper = new[] { sMaxIscAllowed, sMaxImppAllowed, sMaxPowerAllowed, sMaxBySpec }
                     .Where(x => x > 0)
                     .DefaultIfEmpty(0)
                     .Min();

        // Ergebnistext
        var summary =
            $"Auswahl:" + Environment.NewLine +
            $"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp, UMPP={modul.SpannungImMppUmppV:F2} V, IMPP={modul.StromImMppImppA:F2} A, UOC={modul.LeerlaufspannungUocV:F2} V, ISC={modul.KurzschlusstromIscA:F2} A)" + Environment.NewLine +
            $"- WR: {wr.Hersteller} {wr.Model} (Vdc_max={wr.MaxDcEingangsspannungV} V, Start={wr.StartspannungV} V, MPPT={wr.MpptSpannungsbereichV} V, I_in_max={wr.MaxBetriebsPvEingangsstromA} A, I_sc_max={wr.MaxEingangsKurzschlussstromA} A, MPPTs={wr.AnzahlDerMpptTrackers}, Max Strings/MPPT={wr.MaxStringsProMppt})" + Environment.NewLine +
            $"Parameter: Tmin={tMin} °C, Tmax={tMax} °C, Sicherheitsmarge={marginPct} %, Module/String={nModule}, Parallele Strings={nStrings}" + Environment.NewLine +
            $"Abgeleitete Werte (pro String):" + Environment.NewLine +
            $"- Spannung OC (Leerlauf): min={vStringVocMin:F1} V, max={vStringVocMax:F1} V" + Environment.NewLine +
            $"- Spannung MPP: min={vStringVmpMin:F1} V, max={vStringVmpMax:F1} V" + Environment.NewLine +
            $"- Strom ISC: min={iStringScMin:F2} A, max={iStringScMax:F2} A" + Environment.NewLine +
            $"- Strom IMPP: min={iStringImppMin:F2} A, max={iStringImppMax:F2} A" + Environment.NewLine +
            $"Gesamtströme am MPPT (parallele Strings):" + Environment.NewLine +
            $"- ISC gesamt: min={iArrayScMin:F2} A, max={iArrayScMax:F2} A" + Environment.NewLine +
            $"- IMPP gesamt: min={iArrayImppMin:F2} A, max={iArrayImppMax:F2} A" + Environment.NewLine +
            $"Hinweis: Bei parallelen Strings ist die Arrayspannung identisch zur Stringspannung." + Environment.NewLine +
            $"- Effektive Grenzen (mit Sicherheitsmarge): Vdc_max={vdcMaxEff:F1} V, MPPT={mpptMinEff:F1}–{mpptMaxEff:F1} V, Start={vStartEff:F1} V, I_in_max={iInMaxEff:F2} A, I_sc_max={iScMaxEff:F2} A, Pdc_MPPT={pDcPerMpptEff:F0} W" + Environment.NewLine +
            $"Prüfergebnis: {(ok ? "Alle Bedingungen erfüllt." : "Einschränkungen/Verstöße vorhanden.")}" + Environment.NewLine;

        if (!ok)
        {
            summary += "Hinweise:" + Environment.NewLine + string.Join(Environment.NewLine, messages);
        }

        // Empfehlung für N und Strings
        if (nLower <= nUpper)
            summary += Environment.NewLine + $"Empfohlener Bereich Module/String: {nLower} … {nUpper}";
        else
            summary += Environment.NewLine + "Kein zulässiger Bereich für Module/String unter den aktuellen Parametern.";

        if (sUpper > 0)
            summary += Environment.NewLine + $"Zulässige maximale parallele Strings/MPPT: bis {sUpper}";
        else
            summary += Environment.NewLine + "Keine parallelen Strings zulässig (unter aktuellen Parametern).";

        StringCalcOutput.Text = summary;

        _logger.LogInformation("String-Berechnung abgeschlossen. OK={Ok}, N={N}, S={S}.", ok, nModule, nStrings);
        if (!ok)
            _logger.LogWarning("Einschränkungen: {Messages}", string.Join(" | ", messages));
        _logger.LogDebug("Details: VOCmin={VocMin:F2}, VMPPmin={VmpMin:F2}, VMPPmax={VmpMax:F2}, ISCmax={IscMax:F2}, IMPPmax={ImppMax:F2}",
            vocTmin, vmpTmin, vmpTmax, iscTmax, imppTmax);
    }
}
