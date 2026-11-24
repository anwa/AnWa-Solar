using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

using AnWaSolar.Core;
using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

using MaterialDesignThemes.Wpf;

using Microsoft.Extensions.Logging;
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

    // Unterdrückung für Slider-/Toggle-Events bei programmatischen Updates
    private bool _suppressSliderEvents = false;
    private bool _suppressToggleEvents = false;

    #endregion

    #region Konstruktor

    public MainWindow(ILogger<MainWindow> logger, IMarkdownService markdown, IDataStore dataStore, ISettingsService settings)
    {
        InitializeComponent();
        Title = $"AnWa-Solar {AppVersion.Current}";
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
                ToolTip = "String aktivieren/deaktivieren"
            };
            try
            {
                var style = FindResource("MaterialDesignSwitchLightToggleButton") as Style;
                if (style is not null) toggle.Style = style;
            }
            catch { }
            toggle.Checked += (s, e) =>
            {
                if (_suppressToggleEvents) return;
                cfg.IsEnabled = true;
                var refs = sp.Tag as MpptUiRefs;
                UpdateMpptUiState(cfg, refs);
                PersistLastSelection();
                Recalculate();
            };
            toggle.Unchecked += (s, e) =>
            {
                if (_suppressToggleEvents) return;
                cfg.IsEnabled = false;
                var refs = sp.Tag as MpptUiRefs;
                UpdateMpptUiState(cfg, refs);
                PersistLastSelection();
                Recalculate();
            };
            rightHeaderPanel.Children.Add(toggle);

            var btnSelect = new Button { Content = "PV-Modul auswählen", Margin = new Thickness(8, 0, 0, 0) };
            btnSelect.Click += (s, e) => OnSelectModuleForMppt(cfg, sp);
            rightHeaderPanel.Children.Add(btnSelect);

            // Entfernen-Button: initial nicht angezeigt (kein Modul ausgewählt)
            var btnRemove = new Button { Content = "Modul entfernen", Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
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
            catch { }
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
            catch { }
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

            // Toggle initial nach Konfiguration setzen, ohne Events
            _suppressToggleEvents = true;
            toggle.IsChecked = cfg.IsEnabled;
            _suppressToggleEvents = false;

            // Initialer UI-State
            UpdateMpptUiState(cfg, sp.Tag as MpptUiRefs);

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

    // Zentraler UI-State-Update für Buttons und Summary je MPPT
    private void UpdateMpptUiState(StringConfiguration cfg, MpptUiRefs? refs)
    {
        if (refs is null) return;

        // Toggle-Status synchronisieren (programmgesteuert, Events unterdrücken)
        _suppressToggleEvents = true;
        refs.Toggle.IsChecked = cfg.IsEnabled;
        _suppressToggleEvents = false;

        // Summary
        refs.SummaryText.Text = cfg.SelectedModule is null
            ? "Kein PV-Modul ausgewählt."
            : $"{cfg.SelectedModule.Hersteller} {cfg.SelectedModule.Model} ({cfg.SelectedModule.NominalleistungPmaxWp} Wp, UMPP={cfg.SelectedModule.SpannungImMppUmppV:F2} V, IMPP={cfg.SelectedModule.StromImMppImppA:F2} A)";

        // Button-Texte und Sichtbarkeit
        refs.SelectButton.Content = cfg.SelectedModule is null ? "PV-Modul auswählen" : "PV-Modul ändern";

        // Entfernen-Button nur anzeigen, wenn ein Modul gewählt ist
        refs.RemoveButton.Visibility = cfg.SelectedModule is null ? Visibility.Collapsed : Visibility.Visible;

        // Enablement abhängig vom Toggle-Status
        var enabled = cfg.IsEnabled;
        // Toggle bleibt immer bedienbar, damit eine Reaktivierung möglich ist
        refs.SelectButton.IsEnabled = enabled;
        refs.RemoveButton.IsEnabled = enabled && refs.RemoveButton.Visibility == Visibility.Visible;
    }

    #endregion

    #region UI-Erstellung Strings
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
                UpdateMpptUiState(cfg, refs);
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
                    $"{_selectedInverter.Hersteller} {_selectedInverter.Model} | {CalculationTextFormatter.FormatInverterSummary(_selectedInverter)}";
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

                var refs = container.Tag as MpptUiRefs;
                UpdateMpptUiState(cfg, refs);

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

            var refs = container.Tag as MpptUiRefs;
            if (refs is not null)
            {
                // Entfernen-Button ausblenden, Select-Button-Text anpassen
                UpdateMpptUiState(cfg, refs);

                // Slider zurücksetzen (Module: 1..30, Markierung aus)
                _suppressSliderEvents = true;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = 30;
                refs.NSlider.IsSelectionRangeEnabled = false;
                refs.NSlider.Value = Math.Max(1, cfg.ModuleProString);

                // Strings-Slider bleibt nach WR-Spezifikation
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
                $"{_selectedInverter.Hersteller} {_selectedInverter.Model} | {CalculationTextFormatter.FormatInverterSummary(_selectedInverter)}";
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

        StringCalculationResult result = StringCalculator.Calculate(_selectedInverter, _stringConfigs, _parameters);
        ResultsOutput.Text = CalculationTextFormatter.Format(result);

        if (result.HasValidMpptRange)
        {
            UpdateMpptControls(result);

            _logger.LogInformation("String-Berechnung aktualisiert. OK={Ok}.", result.IsOk);
            if (!result.IsOk)
            {
                _logger.LogWarning("Einschränkungen: {Messages}", string.Join(" | ", CalculationTextFormatter.GetMessages(result)));
            }
        }

        BuildMarkdownReport();
        UpdateReportView();
    }

    // Adjusts buttons and sliders of every MPPT section to the calculation result.
    private void UpdateMpptControls(StringCalculationResult result)
    {
        _suppressSliderEvents = true;
        try
        {
            foreach (MpptResult mppt in result.Mppts)
            {
                if (mppt.Configuration is not { } cfg)
                {
                    continue;
                }

                MpptUiRefs? refs = GetMpptUiRefs(mppt.MpptIndex - 1);
                UpdateMpptUiState(cfg, refs);
                if (refs is null)
                {
                    continue;
                }

                bool enabled = mppt.Status != MpptStatus.Disabled;
                refs.NSlider.IsEnabled = enabled;
                refs.SSlider.IsEnabled = enabled;
                if (!enabled)
                {
                    continue;
                }

                // Module slider: 1 .. (highest allowed count + 5), allowed range marked; without module 1 .. 30.
                int moduleMax = mppt.Status == MpptStatus.Calculated && mppt.MaxModulesPerString >= 1
                    ? mppt.MaxModulesPerString + 5
                    : 30;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = Math.Max(1, moduleMax);
                if (mppt.Status == MpptStatus.Calculated && mppt.HasValidModuleRange && mppt.MaxModulesPerString >= 1)
                {
                    refs.NSlider.IsSelectionRangeEnabled = true;
                    refs.NSlider.SelectionStart = mppt.MinModulesPerString;
                    refs.NSlider.SelectionEnd = mppt.MaxModulesPerString;
                }
                else
                {
                    refs.NSlider.IsSelectionRangeEnabled = false;
                }

                // The value is only kept inside the slider range, not clamped to the allowed range.
                cfg.ModuleProString = Math.Clamp(cfg.ModuleProString, 1, (int)refs.NSlider.Maximum);
                refs.NSlider.Value = cfg.ModuleProString;

                // String slider: 1 .. inverter specification.
                int specMax = _selectedInverter?.MaxStringsProMppt > 0 ? _selectedInverter.MaxStringsProMppt : 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(1, specMax);
                cfg.ParalleleStrings = Math.Clamp(cfg.ParalleleStrings, 1, (int)refs.SSlider.Maximum);
                refs.SSlider.Value = cfg.ParalleleStrings;
            }
        }
        finally
        {
            _suppressSliderEvents = false;
        }
    }

    private MpptUiRefs? GetMpptUiRefs(int index)
    {
        if (index < 0 || index >= StringsPanel.Children.Count)
        {
            return null;
        }

        return ((StringsPanel.Children[index] as Card)?.Content as StackPanel)?.Tag as MpptUiRefs;
    }

    #endregion

    #region Bericht

    // Builds the Markdown report from the current configuration.
    private void BuildMarkdownReport()
    {
        StringCalculationResult? result = _selectedInverter is null
            ? null
            : StringCalculator.Calculate(_selectedInverter, _stringConfigs, _parameters);
        _reportMarkdown = ReportBuilder.Build(result, _parameters, DateTime.Now);
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
