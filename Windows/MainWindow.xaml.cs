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
using Microsoft.Win32;

namespace AnWaSolar.Windows;

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

    // Bericht
    private string _reportMarkdown = string.Empty;
    private bool _reportPreviewMode = false;

    // Unterdrückung für Slider-Events bei programmatischen Updates
    private bool _suppressSliderEvents = false;

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
            SelectInverterButton.Content = "Wechselrichter auswählen";
            StringsCard.Visibility = System.Windows.Visibility.Collapsed;
            ResultsOutput.Text = "Keine Berechnung vorhanden.";
        }

        // Initialen Bericht erzeugen
        BuildMarkdownReport();
        UpdateReportView();
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
            var cfg = new StringConfiguration { MpptIndex = i + 1, ModuleProString = 10, ParalleleStrings = 1, IsEnabled = true };
            _stringConfigs.Add(cfg);

            var card = new Card { Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            card.Content = sp;

            // Kopfzeile: Titel links, Toggle + Modul-Auswahl rechts
            var header = new DockPanel();

            var title = new TextBlock { Text = $"MPPT {cfg.MpptIndex}", FontWeight = FontWeights.Bold, FontSize = 14 };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);

            var rightHeaderPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(rightHeaderPanel, Dock.Right);

            var toggle = new System.Windows.Controls.Primitives.ToggleButton
            {
                IsChecked = true,
                ToolTip = "String aktivieren/deaktivieren"
            };
            try
            {
                var style = FindResource("MaterialDesignSwitchLightToggleButton") as Style;
                if (style is not null) toggle.Style = style;
            }
            catch { /* Style optional */ }
            toggle.Checked += (s, e) =>
            {
                cfg.IsEnabled = true;
                PersistLastSelection();
                Recalculate();
            };
            toggle.Unchecked += (s, e) =>
            {
                cfg.IsEnabled = false;
                PersistLastSelection();
                Recalculate();
            };
            rightHeaderPanel.Children.Add(toggle);

            var btnSelect = new Button { Content = "PV-Modul auswählen", Margin = new Thickness(8, 0, 0, 0) };
            btnSelect.Click += (s, e) => OnSelectModuleForMppt(cfg, sp);
            rightHeaderPanel.Children.Add(btnSelect);

            var btnRemove = new Button { Content = "Modul entfernen", Margin = new Thickness(8, 0, 0, 0), IsEnabled = false };
            btnRemove.Click += (s, e) => OnRemoveModuleForMppt(cfg, sp);
            rightHeaderPanel.Children.Add(btnRemove);

            header.Children.Add(rightHeaderPanel);
            sp.Children.Add(header);

            // Zusammenfassungstext
            var summary = new TextBlock { Text = "Kein PV-Modul ausgewählt.", Margin = new Thickness(0, 6, 0, 8) };
            sp.Children.Add(summary);

            // Raster für Slider (3/4 links, 1/4 rechts)
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Linke Spalte: Slider für Module pro String
            var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0) };
            var lblN = new TextBlock { Text = "Anzahl Module pro String" };
            var nSlider = new Slider
            {
                Minimum = 1,
                Maximum = 30, // initial, wird nach Berechnung gesetzt
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                IsSelectionRangeEnabled = false
            };
            try
            {
                var s = FindResource("MaterialDesign3.MaterialDesignDiscreteSlider") as Style;
                if (s is not null) nSlider.Style = s;
            }
            catch { /* Style optional */ }
            nSlider.Value = cfg.ModuleProString;
            nSlider.ValueChanged += (s, e) =>
            {
                if (_suppressSliderEvents) return;
                var val = (int)Math.Round(nSlider.Value);
                if (val < 1) val = 1;
                cfg.ModuleProString = val;
                PersistLastSelection();
                Recalculate();
            };
            leftPanel.Children.Add(lblN);
            leftPanel.Children.Add(nSlider);

            // Rechte Spalte: Slider für parallele Strings
            var rightPanel2 = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0) };
            var lblS = new TextBlock { Text = "Anzahl paralleler Strings" };
            var specMaxStrings = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : 10;
            var sSlider = new Slider
            {
                Minimum = 1,
                Maximum = Math.Max(1, specMaxStrings),
                TickFrequency = 1,
                IsSnapToTickEnabled = true
            };
            try
            {
                var s2 = FindResource("MaterialDesign3.MaterialDesignDiscreteSlider") as Style;
                if (s2 is not null) sSlider.Style = s2;
            }
            catch { /* Style optional */ }
            sSlider.Value = cfg.ParalleleStrings;
            sSlider.ValueChanged += (ss, ee) =>
            {
                if (_suppressSliderEvents) return;
                var val = (int)Math.Round(sSlider.Value);
                if (val < 1) val = 1;
                var maxBySpec = _selectedInverter?.MaxStringsProMppt ?? int.MaxValue;
                if (maxBySpec > 0 && val > maxBySpec) val = maxBySpec;
                cfg.ParalleleStrings = val;
                PersistLastSelection();
                Recalculate();
            };
            rightPanel2.Children.Add(lblS);
            rightPanel2.Children.Add(sSlider);

            grid.Children.Add(leftPanel);
            grid.Children.Add(rightPanel2);
            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel2, 1);
            sp.Children.Add(grid);

            // UI-Referenzen speichern
            sp.Tag = new MpptUiRefs
            {
                SummaryText = summary,
                Toggle = toggle,
                SelectButton = btnSelect,
                RemoveButton = btnRemove,
                NSlider = nSlider,
                SSlider = sSlider
            };

            StringsPanel.Children.Add(card);
        }

        StringsCard.Visibility = Visibility.Visible;
    }

    // UI-Referenzen pro MPPT, um Slider/Toggle dynamisch aktualisieren zu können
    private sealed class MpptUiRefs
    {
        public TextBlock SummaryText { get; set; } = new TextBlock();
        public System.Windows.Controls.Primitives.ToggleButton Toggle { get; set; } = new System.Windows.Controls.Primitives.ToggleButton();
        public Button SelectButton { get; set; } = new Button();
        public Button RemoveButton { get; set; } = new Button();
        public Slider NSlider { get; set; } = new Slider();
        public Slider SSlider { get; set; } = new Slider();
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

                // Button-Texte/Enablement anpassen
                refs.SelectButton.Content = cfg.SelectedModule is null ? "PV-Modul auswählen" : "PV-Modul ändern";
                refs.RemoveButton.IsEnabled = cfg.SelectedModule is not null;
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
                SelectInverterButton.Content = "Wechselrichter ändern";

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
                    refs.SelectButton.Content = "PV-Modul ändern";
                    refs.RemoveButton.IsEnabled = true;
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

    private void OnRemoveModuleForMppt(StringConfiguration cfg, StackPanel container)
    {
        try
        {
            cfg.SelectedModule = null;

            if (container.Tag is MpptUiRefs refs)
            {
                refs.SummaryText.Text = "Kein PV-Modul ausgewählt.";
                refs.SelectButton.Content = "PV-Modul auswählen";
                refs.RemoveButton.IsEnabled = false;

                // Slider zurücksetzen (Module: 1..30, Markierung aus)
                _suppressSliderEvents = true;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = 30;
                refs.NSlider.IsSelectionRangeEnabled = false;
                refs.NSlider.Value = Math.Max(1, cfg.ModuleProString);

                var maxBySpec = _selectedInverter?.MaxStringsProMppt ?? 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(1, maxBySpec);
                refs.SSlider.Value = Math.Max(1, Math.Min(cfg.ParalleleStrings, (int)refs.SSlider.Maximum));
                _suppressSliderEvents = false;
            }

            PersistLastSelection();
            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Entfernen des Moduls.");
            MessageBox.Show("Fehler beim Entfernen des Moduls. Details siehe Log.", "Fehler",
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
            SelectInverterButton.Content = "Wechselrichter ändern";

            BuildStringsUi();

            foreach (var sel in last.Strings)
            {
                var cfg = _stringConfigs.FirstOrDefault(c => c.MpptIndex == sel.MpptIndex);
                if (cfg is null) continue;

                // Aktivierungsstatus übernehmen
                cfg.IsEnabled = sel.IsEnabled;

                // Mengen übernehmen und begrenzen
                cfg.ModuleProString = Math.Max(1, sel.ModuleProString);
                var maxBySpec = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : int.MaxValue;
                cfg.ParalleleStrings = Math.Max(1, Math.Min(sel.ParalleleStrings, maxBySpec));

                // Modulreferenz übernehmen
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
                    IsEnabled = c.IsEnabled,
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
            BuildMarkdownReport();
            UpdateReportView();
            return;
        }

        var tMin = _parameters.MinTempC;
        var tMax = _parameters.MaxTempC;
        var marginPct = _parameters.SicherheitsmargePct;
        var m = marginPct / 100.0;

        if (!TryParseRangeV(_selectedInverter.MpptSpannungsbereichV, out var mpptMin, out var mpptMax))
        {
            ResultsOutput.Text = $"MPPT-Spannungsbereich des Wechselrichters ist ungültig: '{_selectedInverter.MpptSpannungsbereichV}'.";
            BuildMarkdownReport();
            UpdateReportView();
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

        _suppressSliderEvents = true;

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = _stringConfigs.ElementAtOrDefault(i);
            sb.AppendLine("");
            sb.AppendLine($"MPPT {i + 1}:");

            // UI-Refs besorgen
            MpptUiRefs? refs = null;
            if (StringsPanel.Children.Count > i)
            {
                var card = StringsPanel.Children[i] as Card;
                var sp = card?.Content as StackPanel;
                refs = sp?.Tag as MpptUiRefs;
            }

            if (cfg is null)
            {
                sb.AppendLine("- Konfiguration fehlt.");
                continue;
            }

            // Toggle-Status auf UI spiegeln
            if (refs?.Toggle is not null) refs.Toggle.IsChecked = cfg.IsEnabled;

            if (!cfg.IsEnabled)
            {
                sb.AppendLine("- Deaktiviert: MPPT wird in der Berechnung ausgelassen.");
                // Slider deaktivieren
                if (refs?.NSlider is not null) refs.NSlider.IsEnabled = false;
                if (refs?.SSlider is not null) refs.SSlider.IsEnabled = false;
                continue;
            }

            // Slider wieder aktivieren (falls deaktiviert war)
            if (refs?.NSlider is not null) refs.NSlider.IsEnabled = true;
            if (refs?.SSlider is not null) refs.SSlider.IsEnabled = true;

            if (cfg.SelectedModule is null)
            {
                sb.AppendLine("- Kein PV-Modul ausgewählt.");
                globalOk = false;
                globalMessages.Add($"MPPT {i + 1}: Bitte PV-Modul auswählen.");

                // Module-Slider: 1..30, keine Markierung
                if (refs?.NSlider is not null)
                {
                    refs.NSlider.Minimum = 1;
                    refs.NSlider.Maximum = 30;
                    refs.NSlider.IsSelectionRangeEnabled = false;
                    if (cfg.ModuleProString < 1) cfg.ModuleProString = 1;
                    if (cfg.ModuleProString > refs.NSlider.Maximum) cfg.ModuleProString = (int)refs.NSlider.Maximum;
                    refs.NSlider.Value = cfg.ModuleProString;
                }
                // Strings-Slider: 1..Spezifikation
                if (refs?.SSlider is not null)
                {
                    var specMax = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : 10;
                    refs.SSlider.Minimum = 1;
                    refs.SSlider.Maximum = Math.Max(1, specMax);
                    if (cfg.ParalleleStrings < 1) cfg.ParalleleStrings = 1;
                    if (cfg.ParalleleStrings > refs.SSlider.Maximum) cfg.ParalleleStrings = (int)refs.SSlider.Maximum;
                    refs.SSlider.Value = cfg.ParalleleStrings;
                }
                continue;
            }

            var modul = cfg.SelectedModule;
            var nModule = Math.Max(1, cfg.ModuleProString);
            var nStrings = Math.Max(1, cfg.ParalleleStrings);

            // Temperaturabhängige Werte
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

            // String-Werte
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

            // Prüfungen
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
                messages.Add($"Kurzschlussstrom-Grenze überschritten: {iTotalIsc:F2} A > {iScMaxEff:F2} A. Empfehlung: parallele Strings reduzieren (max. {Math.Max(0, sMaxIscViol)}).");
            }
            if (iTotalImpp > iInMaxEff + 1e-6)
            {
                ok = false;
                var sMaxImppViol = (int)Math.Floor(iInMaxEff / Math.Max(0.001, imppTmax));
                messages.Add($"Eingangsstrom-Grenze überschritten: {iTotalImpp:F2} A > {iInMaxEff:F2} A. Empfehlung: parallele Strings reduzieren (max. {Math.Max(0, sMaxImppViol)}).");
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
                messages.Add($"DC-Leistungsgrenze überschritten: {pTotal:F0} W > {pDcPerMpptEff:F0} W pro MPPT. Empfehlung: parallele Strings reduzieren (max. {Math.Max(0, sMaxPowerViol)}).");
            }

            // Zulässige Bereiche berechnen
            var nMaxFromVoc = (int)Math.Floor(vdcMaxEff / vocTmin);
            var nMinFromMppt = (int)Math.Ceiling(mpptMinEff / vmpTmax);
            var nMaxFromMppt = (int)Math.Floor(mpptMaxEff / vmpTmin);
            var nMinFromStart = (int)Math.Ceiling(vStartEff / vmpTmax);

            var nLower = Math.Max(1, Math.Max(nMinFromMppt, nMinFromStart));
            var nUpper = Math.Min(nMaxFromVoc, nMaxFromMppt);

            // Slider-Grenzen für Module: fest 1 .. (nUpper + 5)
            if (refs?.NSlider is not null)
            {
                var sliderMax = nUpper >= 1 ? nUpper + 5 : 30;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = Math.Max(1, sliderMax);
                // Markierung des zulässigen Bereichs
                if (nLower <= nUpper && nUpper >= 1)
                {
                    refs.NSlider.IsSelectionRangeEnabled = true;
                    refs.NSlider.SelectionStart = nLower;
                    refs.NSlider.SelectionEnd = nUpper;
                }
                else
                {
                    refs.NSlider.IsSelectionRangeEnabled = false;
                }
                // Wert nur innerhalb des Sliderbereichs halten (kein Clamp auf zulässigen Bereich)
                if (cfg.ModuleProString < 1) cfg.ModuleProString = 1;
                if (cfg.ModuleProString > refs.NSlider.Maximum) cfg.ModuleProString = (int)refs.NSlider.Maximum;
                refs.NSlider.Value = cfg.ModuleProString;
            }

            // Slider-Grenzen für Strings: fest 1 .. Spezifikation (WR)
            if (refs?.SSlider is not null)
            {
                var specMax = _selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(1, specMax);
                if (cfg.ParalleleStrings < 1) cfg.ParalleleStrings = 1;
                if (cfg.ParalleleStrings > refs.SSlider.Maximum) cfg.ParalleleStrings = (int)refs.SSlider.Maximum;
                refs.SSlider.Value = cfg.ParalleleStrings;
            }

            // Ausgabe
            sb.AppendLine($"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp)");
            sb.AppendLine($"- Einstellungen: Module/String={nModule}, Parallele Strings={nStrings}");
            sb.AppendLine($"- String-Spannung OC: min={vStringVocMin:F1} V, max={vStringVocMax:F1} V");
            sb.AppendLine($"- String-Spannung MPP: min={vStringVmpMin:F1} V, max={vStringVmpMax:F1} V");
            sb.AppendLine($"- String-Ströme ISC: min={iStringScMin:F2} A, max={iStringScMax:F2} A");
            sb.AppendLine($"- String-Ströme IMPP: min={iStringImppMin:F2} A, max={iStringImppMax:F2} A");
            sb.AppendLine($"- PV-Leistung geschätzt (kalt, pro MPPT): {pTotal:F0} W");
            sb.AppendLine($"- Zulässige Module/String: {(nLower <= nUpper ? $"{nLower} … {nUpper}" : "kein gültiger Bereich")}");
            sb.AppendLine($"- Zulässige parallele Strings/MPPT (Herstellerangabe): {(_selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt.ToString() : "n/a")}");

            if (!ok)
            {
                globalOk = false;
                foreach (var msg in messages) globalMessages.Add($"MPPT {i + 1}: {msg}");
                sb.AppendLine("Hinweise:");
                foreach (var msg in messages) sb.AppendLine($"  - {msg}");
            }
        }

        _suppressSliderEvents = false;

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

        // Bericht neu erzeugen und Ansicht aktualisieren
        BuildMarkdownReport();
        UpdateReportView();
    }

    #endregion

    #region Bericht

    // Bericht in Markdown erzeugen (inkl. Berechnungshinweisen)
    private void BuildMarkdownReport()
    {
        var md = new StringBuilder();

        md.AppendLine("# Bericht: PV-String-Auslegung");
        md.AppendLine($"Erstellt am {DateTime.Now:yyyy-MM-dd HH:mm}");
        md.AppendLine();

        if (_selectedInverter is null)
        {
            md.AppendLine("Hinweis: Bitte zuerst einen Wechselrichter auswählen, um einen vollständigen Bericht zu erzeugen.");
            _reportMarkdown = md.ToString();
            ReportMarkdownTextBox.Text = _reportMarkdown;
            return;
        }

        // Parameter
        md.AppendLine("## Parameter und Rahmenbedingungen");
        md.AppendLine($"- Temperaturbereich: Tmin={_parameters.MinTempC} °C, Tmax={_parameters.MaxTempC} °C");
        md.AppendLine($"- Sicherheitsmarge: {_parameters.SicherheitsmargePct} %");
        md.AppendLine();

        // WR-Daten
        md.AppendLine("## Wechselrichter");
        md.AppendLine($"- Hersteller/Modell: {_selectedInverter.Hersteller} {_selectedInverter.Model}");
        md.AppendLine($"- MPPT-Spannungsbereich: {_selectedInverter.MpptSpannungsbereichV} V");
        md.AppendLine($"- Max. DC-Eingangsspannung: {_selectedInverter.MaxDcEingangsspannungV} V");
        md.AppendLine($"- Startspannung: {_selectedInverter.StartspannungV} V");
        md.AppendLine($"- Max. Betriebs-PV-Eingangsstrom: {_selectedInverter.MaxBetriebsPvEingangsstromA} A");
        md.AppendLine($"- Max. Eingangs-Kurzschlussstrom: {_selectedInverter.MaxEingangsKurzschlussstromA} A");
        md.AppendLine($"- Anzahl MPPT: {_selectedInverter.AnzahlDerMpptTrackers}, Max. Strings pro MPPT: {_selectedInverter.MaxStringsProMppt}");
        md.AppendLine();

        // Hinweise zu Berechnungen
        md.AppendLine("### Berechnungshinweise");
        md.AppendLine("- Spannungen und Ströme werden temperaturabhängig auf Basis der angegebenen Temperaturkoeffizienten abgeschätzt.");
        md.AppendLine("- Sicherheitsmargen werden auf Grenzwerte angewendet: z. B. Vdc_max reduziert, Startspannung und MPPT-Untergrenze erhöht.");
        md.AppendLine("- Strings in Serie erhöhen die Spannung proportional zur Modulanzahl, der Strom bleibt gleich. Parallelschaltung addiert Ströme.");
        md.AppendLine();

        // Pro MPPT
        var tMin = _parameters.MinTempC;
        var tMax = _parameters.MaxTempC;
        var m = _parameters.SicherheitsmargePct / 100.0;

        if (!TryParseRangeV(_selectedInverter.MpptSpannungsbereichV, out var mpptMin, out var mpptMax))
        {
            md.AppendLine("Warnung: MPPT-Spannungsbereich des Wechselrichters ist ungültig. Bericht ggf. unvollständig.");
            _reportMarkdown = md.ToString();
            ReportMarkdownTextBox.Text = _reportMarkdown;
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

        md.AppendLine("### Effektive Grenzen (mit Sicherheitsmarge)");
        md.AppendLine($"- Vdc_max={vdcMaxEff:F1} V, MPPT={mpptMinEff:F1}–{mpptMaxEff:F1} V, Start={vStartEff:F1} V, I_in_max={iInMaxEff:F2} A, I_sc_max={iScMaxEff:F2} A, Pdc_MPPT={pDcPerMpptEff:F0} W");
        md.AppendLine();

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = _stringConfigs.ElementAtOrDefault(i);
            md.AppendLine($"## MPPT {i + 1}");

            if (cfg is null || cfg.SelectedModule is null)
            {
                md.AppendLine("- Kein PV-Modul ausgewählt.");
                md.AppendLine();
                continue;
            }

            var modul = cfg.SelectedModule;
            var nModule = Math.Max(1, cfg.ModuleProString);
            var nStrings = Math.Max(1, cfg.ParalleleStrings);

            md.AppendLine($"- Modul: {modul.Hersteller} {modul.Model} (Pmax={modul.NominalleistungPmaxWp} Wp, UMPP={modul.SpannungImMppUmppV:F2} V, IMPP={modul.StromImMppImppA:F2} A, UOC={modul.LeerlaufspannungUocV:F2} V, ISC={modul.KurzschlusstromIscA:F2} A)");
            md.AppendLine($"- Einstellungen: Module/String={nModule}, Parallele Strings={nStrings}");
            md.AppendLine();

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

            md.AppendLine("### Ergebnisse (kondensiert)");
            md.AppendLine($"- String-Spannung OC: min={vStringVocMin:F1} V, max={vStringVocMax:F1} V");
            md.AppendLine($"- String-Spannung MPP: min={vStringVmpMin:F1} V, max={vStringVmpMax:F1} V");
            md.AppendLine($"- String-Ströme ISC: min={iStringScMin:F2} A, max={iStringScMax:F2} A");
            md.AppendLine($"- String-Ströme IMPP: min={iStringImppMin:F2} A, max={iStringImppMax:F2} A");
            md.AppendLine($"- Gesamtströme am MPPT: ISC min={iArrayScMin:F2} A / max={iArrayScMax:F2} A; IMPP min={iArrayImppMin:F2} A / max={iArrayImppMax:F2} A");
            md.AppendLine();

            md.AppendLine("### Empfehlungen und Grenzprüfung");
            var vStringVocTmin = nModule * vocTmin;
            var vStringVmpTmax = nModule * vmpTmax;
            var vStringVmpTmin = nModule * vmpTmin;

            var pStringMax = nModule * pmaxTmin;
            var pTotal = nStrings * pStringMax;

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
                         .DefaultIfEmpty(1)
                         .Min();

            md.AppendLine($"- Zulässige Module/String: {(nLower <= nUpper ? $"{nLower} … {nUpper}" : "kein gültiger Bereich")}");
            md.AppendLine($"- Zulässige parallele Strings/MPPT (Herstellerangabe): {(_selectedInverter.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt.ToString() : "n/a")}");
            md.AppendLine();

            md.AppendLine("### Detail-Hinweise");
            if (vStringVocTmin > vdcMaxEff + 1e-6)
                md.AppendLine($"- Überschreitung der max. DC-Spannung bei Tmin: {vStringVocTmin:F1} V > {vdcMaxEff:F1} V.");
            if (vStringVmpTmax < mpptMinEff - 1e-6)
                md.AppendLine($"- Unterschreitung MPPT-Untergrenze bei Tmax: {vStringVmpTmax:F1} V < {mpptMinEff:F1} V.");
            if (vStringVmpTmin > mpptMaxEff + 1e-6)
                md.AppendLine($"- Überschreitung MPPT-Obergrenze bei Tmin: {vStringVmpTmin:F1} V > {mpptMaxEff:F1} V.");
            if (vStringVmpTmax < vStartEff - 1e-6)
                md.AppendLine($"- Startspannung nicht erreicht: {vStringVmpTmax:F1} V < {vStartEff:F1} V.");
            if (iArrayScMax > iScMaxEff + 1e-6)
                md.AppendLine($"- Kurzschlussstrom-Grenze überschritten: {iArrayScMax:F2} A > {iScMaxEff:F2} A.");
            if (iArrayImppMax > iInMaxEff + 1e-6)
                md.AppendLine($"- Eingangsstrom-Grenze überschritten: {iArrayImppMax:F2} A > {iInMaxEff:F2} A.");
            if (pTotal > pDcPerMpptEff + 1e-6)
                md.AppendLine($"- DC-Leistungsgrenze überschritten: {pTotal:F0} W > {pDcPerMpptEff:F0} W pro MPPT.");
            md.AppendLine();
        }

        _reportMarkdown = md.ToString();
        ReportMarkdownTextBox.Text = _reportMarkdown;
    }

    // Ansicht aktualisieren (Markdown vs. HTML-Preview)
    private void UpdateReportView()
    {
        if (_reportPreviewMode)
        {
            ReportMarkdownTextBox.Visibility = Visibility.Collapsed;
            ReportPreviewContainer.Visibility = Visibility.Visible;

            var htmlBody = _markdown.ToHtml(_reportMarkdown);
            var htmlDoc = new StringBuilder();
            htmlDoc.AppendLine("<!DOCTYPE html>");
            htmlDoc.AppendLine("<html><head><meta charset=" + (char)34 + "utf-8" + (char)34 + "><title>Bericht</title></head><body>");
            htmlDoc.AppendLine(htmlBody);
            htmlDoc.AppendLine("</body></html>");

            try
            {
                ReportPreviewBrowser.NavigateToString(htmlDoc.ToString());
                _logger.LogInformation("Berichtsvorschau aktualisiert. Länge={Laenge}.", htmlBody.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aktualisieren der Berichtsvorschau.");
                MessageBox.Show("Fehler beim Aktualisieren der Berichtsvorschau. Details siehe Log.", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            ReportPreviewContainer.Visibility = Visibility.Collapsed;
            ReportMarkdownTextBox.Visibility = Visibility.Visible;
        }
    }

    // Umschalten der Ansicht
    private void OnToggleReportViewClick(object sender, RoutedEventArgs e)
    {
        _reportPreviewMode = !_reportPreviewMode;
        UpdateReportView();
        _logger.LogInformation("Bericht-Ansicht umgeschaltet: {Mode}.", _reportPreviewMode ? "Vorschau" : "Markdown");
    }

    // Markdown kopieren
    private void OnCopyReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_reportMarkdown ?? string.Empty);
            _logger.LogInformation("Markdown-Bericht in Zwischenablage kopiert. Länge={Laenge}.", (_reportMarkdown ?? string.Empty).Length);
            MessageBox.Show("Markdown-Bericht wurde in die Zwischenablage kopiert.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Kopieren des Markdown-Berichts.");
            MessageBox.Show("Fehler beim Kopieren. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Markdown speichern
    private void OnSaveReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "Markdown-Bericht speichern",
                Filter = "Markdown-Datei (*.md)|*.md|Textdatei (*.txt)|*.txt",
                FileName = "AnWa-Solar-Bericht.md",
                AddExtension = true,
                OverwritePrompt = true
            };
            var ok = dlg.ShowDialog(this);
            if (ok == true)
            {
                System.IO.File.WriteAllText(dlg.FileName, _reportMarkdown ?? string.Empty, new System.Text.UTF8Encoding(false));
                _logger.LogInformation("Markdown-Bericht gespeichert: {Path}.", dlg.FileName);
                MessageBox.Show("Markdown-Bericht wurde gespeichert.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Speichern des Markdown-Berichts.");
            MessageBox.Show("Fehler beim Speichern. Details siehe Log.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
