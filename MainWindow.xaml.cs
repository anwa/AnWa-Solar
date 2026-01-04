using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using AnWaSolar.Models;
using AnWaSolar.Services;
using Microsoft.Extensions.Logging;
using MaterialDesignThemes.Wpf;

namespace AnWaSolar;

public partial class MainWindow : Window
{
    #region Felder

    private readonly ILogger<MainWindow> _logger;
    private readonly IMarkdownService _markdown;
    private readonly IDataStore _dataStore;
    private readonly ISettingsService _settings;

    private Wechselrichter? _selectedInverter;
    private readonly List<StringConfiguration> _stringConfigs = new();

    private CalculationParameters _parameters;

    #endregion

    #region Konstruktor

    public MainWindow(ILogger<MainWindow> logger, IMarkdownService markdown, IDataStore dataStore, ISettingsService settings)
    {
        InitializeComponent();
        _logger = logger;
        _markdown = markdown;
        _dataStore = dataStore;
        _settings = settings;

        _logger.LogInformation("Hauptfenster initialisiert.");

        // Parameter laden und anzeigen
        _parameters = _settings.LoadParameters();
        UpdateParametersSummary();

        // Letzte Auswahl wiederherstellen
        TryRestoreLastSelection();

        // Falls kein WR geladen werden konnte:
        if (_selectedInverter is null)
        {
            InverterSummaryText.Text = "Kein Wechselrichter ausgewählt.";
            StringsCard.Visibility = System.Windows.Visibility.Collapsed;
            ResultsOutput.Text = "Keine Berechnung vorhanden.";
        }
    }

    #endregion

    #region Hilfsfunktionen Parsing/Physik

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

    #endregion

    #region UI-Erstellung Strings

    // Erstellt die String-Abschnitte dynamisch basierend auf _selectedInverter
    private void BuildStringsUi()
    {
        StringsPanel.Children.Clear();
        _stringConfigs.Clear();

        if (_selectedInverter is null) return;

        var mpptCount = Math.Max(1, _selectedInverter.AnzahlDerMpptTrackers);

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = new StringConfiguration { MpptIndex = i + 1, ModuleProString = 10, ParalleleStrings = 1 };
            _stringConfigs.Add(cfg);

            var card = new Card { Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            card.Content = sp;

            var header = new DockPanel();
            var title = new TextBlock { Text = $"MPPT {cfg.MpptIndex}", FontWeight = FontWeights.Bold, FontSize = 14 };
            header.Children.Add(title);

            var btnSelect = new Button { Content = "PV-Modul auswählen", Margin = new Thickness(8, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btnSelect.Click += (s, e) => OnSelectModuleForMppt(cfg, sp);
            header.Children.Add(btnSelect);

            sp.Children.Add(header);

            var summary = new TextBlock { Text = "Kein PV-Modul ausgewählt.", Margin = new Thickness(0, 6, 0, 8) };
            sp.Children.Add(summary);

            // Steuerung: Module pro String
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0) };
            var rightPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0) };

            var lblN = new TextBlock { Text = "Anzahl Module pro String" };
            var nPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var nBox = new TextBox { Width = 80, Text = cfg.ModuleProString.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 8, 0) };
            var nInc = new Button { Content = "+", Width = 32, Margin = new Thickness(0, 0, 4, 0) };
            var nDec = new Button { Content = "−", Width = 32 };
            nInc.Click += (s, e) =>
            {
                if (TryParseInt(nBox.Text, out var val)) { val = val + 1; nBox.Text = val.ToString(CultureInfo.InvariantCulture); cfg.ModuleProString = val; PersistLastSelection(); Recalculate(); }
            };
            nDec.Click += (s, e) =>
            {
                if (TryParseInt(nBox.Text, out var val)) { val = Math.Max(1, val - 1); nBox.Text = val.ToString(CultureInfo.InvariantCulture); cfg.ModuleProString = val; PersistLastSelection(); Recalculate(); }
            };
            nBox.TextChanged += (s, e) =>
            {
                if (TryParseInt(nBox.Text, out var val) && val > 0) { cfg.ModuleProString = val; PersistLastSelection(); Recalculate(); }
            };
            nPanel.Children.Add(nBox);
            nPanel.Children.Add(nInc);
            nPanel.Children.Add(nDec);
            leftPanel.Children.Add(lblN);
            leftPanel.Children.Add(nPanel);

            var lblS = new TextBlock { Text = "Anzahl paralleler Strings" };
            var sPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var sBox = new TextBox { Width = 80, Text = cfg.ParalleleStrings.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(0, 0, 8, 0) };
            var sInc = new Button { Content = "+", Width = 32, Margin = new Thickness(0, 0, 4, 0) };
            var sDec = new Button { Content = "−", Width = 32 };
            sInc.Click += (ss, ee) =>
            {
                if (TryParseInt(sBox.Text, out var val))
                {
                    var maxBySpec = _selectedInverter?.MaxStringsProMppt ?? int.MaxValue;
                    val = val + 1;
                    if (val > maxBySpec && maxBySpec > 0)
                    {
                        MessageBox.Show($"Max. Strings/MPPT überschritten ({maxBySpec}).", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                        val = maxBySpec;
                    }
                    sBox.Text = val.ToString(CultureInfo.InvariantCulture);
                    cfg.ParalleleStrings = val;
                    PersistLastSelection();
                    Recalculate();
                }
            };
            sDec.Click += (ss, ee) =>
            {
                if (TryParseInt(sBox.Text, out var val)) { val = Math.Max(1, val - 1); sBox.Text = val.ToString(CultureInfo.InvariantCulture); cfg.ParalleleStrings = val; PersistLastSelection(); Recalculate(); }
            };
            sBox.TextChanged += (ss, ee) =>
            {
                if (TryParseInt(sBox.Text, out var val) && val > 0)
                {
                    var maxBySpec = _selectedInverter?.MaxStringsProMppt ?? int.MaxValue;
                    if (val > maxBySpec && maxBySpec > 0)
                    {
                        MessageBox.Show($"Max. Strings/MPPT überschritten ({maxBySpec}).", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                        val = maxBySpec;
                        sBox.Text = val.ToString(CultureInfo.InvariantCulture);
                    }
                    cfg.ParalleleStrings = val;
                    PersistLastSelection();
                    Recalculate();
                }
            };
            sPanel.Children.Add(sBox);
            sPanel.Children.Add(sInc);
            sPanel.Children.Add(sDec);
            rightPanel.Children.Add(lblS);
            rightPanel.Children.Add(sPanel);

            grid.Children.Add(leftPanel);
            grid.Children.Add(rightPanel);
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            sp.Children.Add(grid);

            // Speichere Referenz für spätere UI-Updates
            sp.Tag = new MpptUiRefs { SummaryText = summary };

            StringsPanel.Children.Add(card);
        }

        StringsCard.Visibility = Visibility.Visible;
    }

    private sealed class MpptUiRefs
    {
        public TextBlock SummaryText { get; set; } = new TextBlock();
    }

    private void RefreshStringsUiSummaries()
    {
        if (_selectedInverter is null) return;
        var mpptCount = Math.Max(1, _selectedInverter.AnzahlDerMpptTrackers);

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = _stringConfigs.ElementAtOrDefault(i);
            var card = StringsPanel.Children[i] as Card;
            var sp = card?.Content as StackPanel;
            var refs = sp?.Tag as MpptUiRefs;
            if (cfg is not null && refs is not null)
            {
                refs.SummaryText.Text = cfg.SelectedModule is null
                    ? "Kein PV-Modul ausgewählt."
                    : $"{cfg.SelectedModule.Hersteller} {cfg.SelectedModule.Model} ({cfg.SelectedModule.NominalleistungPmaxWp} Wp, UMPP={cfg.SelectedModule.SpannungImMppUmppV:F2} V, IMPP={cfg.SelectedModule.StromImMppImppA:F2} A)";
            }
        }
    }

    #endregion

    #region Events

    private void OnSelectInverterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new SelectInverterWindow(_dataStore.Wechselrichter);
            win.Owner = this;
            var res = win.ShowDialog();
            if (res == true && win.Selected is not null)
            {
                _selectedInverter = win.Selected;
                InverterSummaryText.Text =
                    $"{_selectedInverter.Hersteller} {_selectedInverter.Model} | Vdc_max={_selectedInverter.MaxDcEingangsspannungV} V, Start={_selectedInverter.StartspannungV} V, MPPT={_selectedInverter.MpptSpannungsbereichV} V, I_in_max={_selectedInverter.MaxBetriebsPvEingangsstromA} A, I_sc_max={_selectedInverter.MaxEingangsKurzschlussstromA} A, MPPTs={_selectedInverter.AnzahlDerMpptTrackers}, Max Strings/MPPT={_selectedInverter.MaxStringsProMppt}";

                _logger.LogInformation("Wechselrichter übernommen: {Hersteller} {Model}.", _selectedInverter.Hersteller, _selectedInverter.Model);

                BuildStringsUi();
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der Wechselrichter-Auswahl.");
            MessageBox.Show("Fehler bei der Wechselrichter-Auswahl. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSelectModuleForMppt(StringConfiguration cfg, StackPanel container)
    {
        try
        {
            var win = new SelectModuleWindow(_dataStore.Module);
            win.Owner = this;
            var res = win.ShowDialog();
            if (res == true && win.Selected is not null)
            {
                cfg.SelectedModule = win.Selected;

                if (container.Tag is MpptUiRefs refs)
                {
                    refs.SummaryText.Text = $"{cfg.SelectedModule.Hersteller} {cfg.SelectedModule.Model} ({cfg.SelectedModule.NominalleistungPmaxWp} Wp, UMPP={cfg.SelectedModule.SpannungImMppUmppV:F2} V, IMPP={cfg.SelectedModule.StromImMppImppA:F2} A)";
                }

                _logger.LogInformation("PV-Modul für MPPT {Mppt} übernommen: {Hersteller} {Model}.", cfg.MpptIndex, cfg.SelectedModule.Hersteller, cfg.SelectedModule.Model);
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen der Modul-Auswahl.");
            MessageBox.Show("Fehler bei der Modul-Auswahl. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnEditParametersClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new ParametersWindow(_parameters);
            win.Owner = this;
            var res = win.ShowDialog();
            if (res == true)
            {
                var saved = _settings.SaveParameters(win.Parameters);
                if (!saved)
                {
                    MessageBox.Show("Parameter konnten nicht gespeichert werden. Sie bleiben nur temporär gültig.", "Fehler",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                _parameters = win.Parameters;
                _logger.LogInformation("Berechnungsparameter übernommen: Tmin={Tmin}, Tmax={Tmax}, Sicherheitsmarge={Margin} %.",
                    _parameters.MinTempC, _parameters.MaxTempC, _parameters.SicherheitsmargePct);
                UpdateParametersSummary();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Öffnen des Parameter-Dialogs.");
            MessageBox.Show("Fehler beim Parameter-Dialog. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    #endregion

    #region Berechnung

    private void UpdateParametersSummary()
    {
        ParametersSummaryText.Text = $"Tmin={_parameters.MinTempC} °C, Tmax={_parameters.MaxTempC} °C, Sicherheitsmarge={_parameters.SicherheitsmargePct} %";
    }

    private void TryRestoreLastSelection()
    {
        try
        {
            var last = _settings.LoadLastSelection();
            if (last.Inverter is null) return;

            var wr = _dataStore.Wechselrichter.FirstOrDefault(x =>
                string.Equals(x.Hersteller, last.Inverter.Hersteller, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Model, last.Inverter.Model, StringComparison.OrdinalIgnoreCase));
            if (wr is null) return;

            _selectedInverter = wr;
            InverterSummaryText.Text =
                $"{_selectedInverter.Hersteller} {_selectedInverter.Model} | Vdc_max={_selectedInverter.MaxDcEingangsspannungV} V, Start={_selectedInverter.StartspannungV} V, MPPT={_selectedInverter.MpptSpannungsbereichV} V, I_in_max={_selectedInverter.MaxBetriebsPvEingangsstromA} A, I_sc_max={_selectedInverter.MaxEingangsKurzschlussstromA} A, MPPTs={_selectedInverter.AnzahlDerMpptTrackers}, Max Strings/MPPT={_selectedInverter.MaxStringsProMppt}";

            BuildStringsUi();

            foreach (var sel in last.Strings)
            {
                var cfg = _stringConfigs.FirstOrDefault(c => c.MpptIndex == sel.MpptIndex);
                if (cfg is null) continue;

                cfg.ModuleProString = Math.Max(1, sel.ModuleProString);
                var maxBySpec = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : int.MaxValue;
                cfg.ParalleleStrings = Math.Max(1, Math.Min(sel.ParalleleStrings, maxBySpec));

                if (sel.Module is not null)
                {
                    var mod = _dataStore.Module.FirstOrDefault(x =>
                        string.Equals(x.Hersteller, sel.Module.Hersteller, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Model, sel.Module.Model, StringComparison.OrdinalIgnoreCase));
                    cfg.SelectedModule = mod;
                }
            }

            RefreshStringsUiSummaries();
            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Wiederherstellung der letzten Auswahl.");
        }
    }

    private void PersistLastSelection()
    {
        try
        {
            if (_selectedInverter is null) return;

            var sel = new LastSelection
            {
                Inverter = new SelectedRef
                {
                    Hersteller = _selectedInverter.Hersteller,
                    Model = _selectedInverter.Model
                },
                Strings = _stringConfigs.Select(c => new MpptStringSelection
                {
                    MpptIndex = c.MpptIndex,
                    ModuleProString = c.ModuleProString,
                    ParalleleStrings = c.ParalleleStrings,
                    Module = c.SelectedModule is null ? null : new SelectedRef
                    {
                        Hersteller = c.SelectedModule.Hersteller,
                        Model = c.SelectedModule.Model
                    }
                }).ToList()
            };

            var ok = _settings.SaveLastSelection(sel);
            if (!ok)
            {
                _logger.LogWarning("Letzte Auswahl konnte nicht gespeichert werden.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern der letzten Auswahl.");
        }
    }

    private void Recalculate()
    {
        if (_selectedInverter is null)
        {
            ResultsOutput.Text = "Bitte zuerst einen Wechselrichter auswählen.";
            return;
        }

        var tMin = _parameters.MinTempC;
        var tMax = _parameters.MaxTempC;
        var marginPct = _parameters.SicherheitsmargePct;
        var m = marginPct / 100.0;

        if (!TryParseRangeV(_selectedInverter.MpptSpannungsbereichV, out var mpptMin, out var mpptMax))
        {
            ResultsOutput.Text = $"MPPT-Spannungsbereich des Wechselrichters ist ungültig: '{_selectedInverter.MpptSpannungsbereichV}'.";
            return;
        }

        var vdcMaxEff = _selectedInverter.MaxDcEingangsspannungV * (1 - m);
        var vStartEff = _selectedInverter.StartspannungV * (1 + m);
        var mpptMinEff = mpptMin * (1 + m);
        var mpptMaxEff = mpptMax * (1 - m);
        var iInMaxEff = _selectedInverter.MaxBetriebsPvEingangsstromA * (1 - m);
        var iScMaxEff = _selectedInverter.MaxEingangsKurzschlussstromA * (1 - m);

        var mpptCount = Math.Max(1, _selectedInverter.AnzahlDerMpptTrackers);
        var pDcPerMppt = _selectedInverter.MaxDcEingangsleistungW / (double)mpptCount;
        var pDcPerMpptEff = pDcPerMppt * (1 - m);

        var sb = new StringBuilder();
        sb.AppendLine("Berechnung basierend auf:");
        sb.AppendLine($"- WR: {_selectedInverter.Hersteller} {_selectedInverter.Model} (Vdc_max={_selectedInverter.MaxDcEingangsspannungV} V, Start={_selectedInverter.StartspannungV} V, MPPT={_selectedInverter.MpptSpannungsbereichV} V, I_in_max={_selectedInverter.MaxBetriebsPvEingangsstromA} A, I_sc_max={_selectedInverter.MaxEingangsKurzschlussstromA} A, MPPTs={_selectedInverter.AnzahlDerMpptTrackers}, Max Strings/MPPT={_selectedInverter.MaxStringsProMppt})");
        sb.AppendLine($"- Parameter: Tmin={tMin} °C, Tmax={tMax} °C, Sicherheitsmarge={marginPct} %");
        sb.AppendLine($"- Effektive Grenzen (mit Sicherheitsmarge): Vdc_max={vdcMaxEff:F1} V, MPPT={mpptMinEff:F1}–{mpptMaxEff:F1} V, Start={vStartEff:F1} V, I_in_max={iInMaxEff:F2} A, I_sc_max={iScMaxEff:F2} A, Pdc_MPPT={pDcPerMpptEff:F0} W");

        bool globalOk = true;
        var globalMessages = new List<string>();

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = _stringConfigs.ElementAtOrDefault(i);
            sb.AppendLine("");
            sb.AppendLine($"MPPT {i + 1}:");

            if (cfg is null || cfg.SelectedModule is null)
            {
                sb.AppendLine("- Kein PV-Modul ausgewählt.");
                globalOk = false;
                globalMessages.Add($"MPPT {i + 1}: Bitte PV-Modul auswählen.");
                continue;
            }

            var modul = cfg.SelectedModule;
            var nModule = Math.Max(1, cfg.ModuleProString);
            var nStrings = Math.Max(1, cfg.ParalleleStrings);

            var vocTmin = ApplyTempCoeff(modul.LeerlaufspannungUocV, modul.TemperaturkoeffVocProzentProGradC, tMin);
            var vocTmax = ApplyTempCoeff(modul.LeerlaufspannungUocV, modul.TemperaturkoeffVocProzentProGradC, tMax);

            var vmpTmin = ApplyTempCoeff(modul.SpannungImMppUmppV, modul.TemperaturkoeffVocProzentProGradC, tMin);
            var vmpTmax = ApplyTempCoeff(modul.SpannungImMppUmppV, modul.TemperaturkoeffVocProzentProGradC, tMax);

            var pmaxTmin = ApplyTempCoeff(modul.NominalleistungPmaxWp, modul.TemperaturkoeffPmaxProzentProGradC, tMin);
            var pmaxTmax = ApplyTempCoeff(modul.NominalleistungPmaxWp, modul.TemperaturkoeffPmaxProzentProGradC, tMax);

            var iscTmin = ApplyTempCoeff(modul.KurzschlusstromIscA, modul.TemperaturkoeffIscProzentProGradC, tMin);
            var iscTmax = ApplyTempCoeff(modul.KurzschlusstromIscA, modul.TemperaturkoeffIscProzentProGradC, tMax);

            var imppTmin = pmaxTmin / Math.Max(0.1, vmpTmin);
            var imppTmax = pmaxTmax / Math.Max(0.1, vmpTmax);

            var vStringVocMin = nModule * Math.Min(vocTmin, vocTmax);
            var vStringVocMax = nModule * Math.Max(vocTmin, vocTmax);

            var vStringVmpMin = nModule * Math.Min(vmpTmin, vmpTmax);
            var vStringVmpMax = nModule * Math.Max(vmpTmin, vmpTmax);

            var iStringScMin = Math.Min(iscTmin, iscTmax);
            var iStringScMax = Math.Max(iscTmin, iscTmax);

            var iStringImppMin = Math.Min(imppTmin, imppTmax);
            var iStringImppMax = Math.Max(imppTmin, imppTmax);

            var iArrayScMin = nStrings * iStringScMin;
            var iArrayScMax = nStrings * iStringScMax;

            var iArrayImppMin = nStrings * iStringImppMin;
            var iArrayImppMax = nStrings * iStringImppMax;

            var messages = new List<string>();
            bool ok = true;

            var vStringVocTmin = nModule * vocTmin;
            if (vStringVocTmin > vdcMaxEff + 1e-6)
            {
                ok = false;
                var nMaxVoc = (int)Math.Floor(vdcMaxEff / vocTmin);
                messages.Add($"Überschreitung der max. DC-Spannung: N*VOC(Tmin)={vStringVocTmin:F1} V > {vdcMaxEff:F1} V. Max. Module/String: {Math.Max(0, nMaxVoc)}.");
            }

            var vStringVmpTmax = nModule * vmpTmax;
            var vStringVmpTmin = nModule * vmpTmin;
            if (vStringVmpTmax < mpptMinEff - 1e-6)
            {
                ok = false;
                var nMinMppt = (int)Math.Ceiling(mpptMinEff / vmpTmax);
                messages.Add($"Unterschreitung MPPT-Untergrenze bei heiß: N*VMPP(Tmax)={vStringVmpTmax:F1} V < {mpptMinEff:F1} V. Min. Module/String: {Math.Max(1, nMinMppt)}.");
            }
            if (vStringVmpTmin > mpptMaxEff + 1e-6)
            {
                ok = false;
                var nMaxMppt = (int)Math.Floor(mpptMaxEff / vmpTmin);
                messages.Add($"Überschreitung MPPT-Obergrenze bei kalt: N*VMPP(Tmin)={vStringVmpTmin:F1} V > {mpptMaxEff:F1} V. Max. Module/String: {Math.Max(0, nMaxMppt)}.");
            }

            if (vStringVmpTmax < vStartEff - 1e-6)
            {
                ok = false;
                var nMinStart = (int)Math.Ceiling(vStartEff / vmpTmax);
                messages.Add($"Startspannung nicht erreicht: N*VMPP(Tmax)={vStringVmpTmax:F1} V < {vStartEff:F1} V. Min. Module/String: {Math.Max(1, nMinStart)}.");
            }

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

            if (_selectedInverter.MaxStringsProMppt > 0 && nStrings > _selectedInverter.MaxStringsProMppt)
            {
                ok = false;
                messages.Add($"Max. Strings/MPPT überschritten: {nStrings} > {_selectedInverter.MaxStringsProMppt}. Bitte reduzieren.");
            }

            var pStringMax = nModule * pmaxTmin;
            var pTotal = nStrings * pStringMax;
            if (pTotal > pDcPerMpptEff + 1e-6)
            {
                ok = false;
                var sMaxPowerViol = (int)Math.Floor(pDcPerMpptEff / Math.Max(1.0, pStringMax));
                messages.Add($"DC-Leistungsgrenze überschritten: {pTotal:F0} W > {pDcPerMpptEff:F0} W pro MPPT. Max. parallele Strings (Leistung): {Math.Max(0, sMaxPowerViol)}.");
            }

            var nMaxFromVoc = (int)Math.Floor(vdcMaxEff / vocTmin);
            var nMinFromMppt = (int)Math.Ceiling(mpptMinEff / vmpTmax);
            var nMaxFromMppt = (int)Math.Floor(mpptMaxEff / vmpTmin);
            var nMinFromStart = (int)Math.Ceiling(vStartEff / vmpTmax);

            var nLower = Math.Max(1, Math.Max(nMinFromMppt, nMinFromStart));
            var nUpper = Math.Min(nMaxFromVoc, nMaxFromMppt);

            var sMaxIscAllowed = (int)Math.Floor(iScMaxEff / Math.Max(0.001, iscTmax));
            var sMaxImppAllowed = (int)Math.Floor(iInMaxEff / Math.Max(0.001, imppTmax));
            var sMaxPowerAllowed = (int)Math.Floor(pDcPerMpptEff / Math.Max(1.0, pStringMax));
            var sMaxBySpec = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : int.MaxValue;
            var sUpper = new[] { sMaxIscAllowed, sMaxImppAllowed, sMaxPowerAllowed, sMaxBySpec }
                         .Where(x => x > 0)
                         .DefaultIfEmpty(0)
                         .Min();

            sb.AppendLine($"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp)");
            sb.AppendLine($"- Einstellungen: Module/String={nModule}, Parallele Strings={nStrings}");
            sb.AppendLine($"- String-Spannung OC: min={vStringVocMin:F1} V, max={vStringVocMax:F1} V");
            sb.AppendLine($"- String-Spannung MPP: min={vStringVmpMin:F1} V, max={vStringVmpMax:F1} V");
            sb.AppendLine($"- String-Ströme ISC: min={iStringScMin:F2} A, max={iStringScMax:F2} A");
            sb.AppendLine($"- String-Ströme IMPP: min={iStringImppMin:F2} A, max={iStringImppMax:F2} A");
            sb.AppendLine($"- PV-Leistung geschätzt (kalt, pro MPPT): {pTotal:F0} W");
            sb.AppendLine($"- Zulässige Module/String: {(nLower <= nUpper ? $"{nLower} … {nUpper}" : "kein gültiger Bereich")}");
            sb.AppendLine($"- Zulässige parallele Strings/MPPT: {(sUpper > 0 ? $"bis {sUpper}" : "0")}");

            if (!ok)
            {
                globalOk = false;
                foreach (var msg in messages) globalMessages.Add($"MPPT {i + 1}: {msg}");
                sb.AppendLine("Hinweise:");
                foreach (var msg in messages) sb.AppendLine($"  - {msg}");
            }
        }

        sb.AppendLine("");
        sb.AppendLine($"Prüfergebnis: {(globalOk ? "Alle Bedingungen erfüllt." : "Einschränkungen/Verstöße vorhanden.")}");
        if (!globalOk && globalMessages.Count > 0)
        {
            sb.AppendLine("Gesamthinweise:");
            foreach (var gm in globalMessages) sb.AppendLine($"- {gm}");
        }

        ResultsOutput.Text = sb.ToString();

        _logger.LogInformation("String-Berechnung aktualisiert. OK={Ok}.", globalOk);
        if (!globalOk)
            _logger.LogWarning("Einschränkungen: {Messages}", string.Join(" | ", globalMessages));
    }

    #endregion
}
