using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
    #region Fields

    private readonly ILogger<MainWindow> _logger;
    private readonly IMarkdownService _markdown;
    private readonly IDataStore _dataStore;
    private readonly ISettingsService _settings;

    private Inverter? _selectedInverter;
    private readonly List<StringConfiguration> _stringConfigs = new();

    private CalculationParameters _parameters;

    // Report
    private string _reportMarkdown = string.Empty;
    private bool _reportPreviewMode = false;

    // Suppress slider and toggle events while values are set programmatically
    private bool _suppressSliderEvents = false;
    private bool _suppressToggleEvents = false;

    // Debounced saving of the last selection
    private static readonly TimeSpan s_persistDelay = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherTimer _persistTimer;

    #endregion

    #region Constructor

    public MainWindow(ILogger<MainWindow> logger, IMarkdownService markdown, IDataStore dataStore, ISettingsService settings)
    {
        _persistTimer = new DispatcherTimer { Interval = s_persistDelay };
        _persistTimer.Tick += (_, _) =>
        {
            _persistTimer.Stop();
            WriteLastSelection();
        };
        Closing += (_, _) => FlushPendingPersist();
        Loaded += (_, _) => ShowDataIssues();

        InitializeComponent();
        Title = $"AnWa-Solar {AppVersion.Current}";
        _logger = logger;
        _markdown = markdown;
        _dataStore = dataStore;
        _settings = settings;

        _logger.LogInformation("Main window initialized.");

        // Load and show the parameters
        _parameters = _settings.LoadParameters();
        UpdateParametersSummary();

        // Restore the last selection
        TryRestoreLastSelection();

        // No inverter could be restored
        if (_selectedInverter is null)
        {
            InverterSummaryText.Text = "No inverter selected.";
            SelectInverterButton.Content = "Select inverter";
            StringsCard.Visibility = System.Windows.Visibility.Collapsed;
            ResultsOutput.Text = "No calculation yet.";
        }

        // Create the initial report
        BuildMarkdownReport();
        UpdateReportView();
    }

    #endregion

    #region String sections

    // Creates one section per MPPT of _selectedInverter
    private void BuildStringsUi()
    {
        StringsPanel.Children.Clear();
        _stringConfigs.Clear();

        if (_selectedInverter is null) return;

        var mpptCount = Math.Max(1, _selectedInverter.MpptCount);

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = new StringConfiguration { MpptIndex = i + 1, ModulesPerString = 10, ParallelStrings = 1, IsEnabled = true };
            _stringConfigs.Add(cfg);

            var card = new Card { Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12) };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            card.Content = sp;

            // Header: title on the left, toggle and module buttons on the right
            var header = new DockPanel();

            var title = new TextBlock { Text = $"MPPT {cfg.MpptIndex}", FontWeight = FontWeights.Bold, FontSize = 14 };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);

            var rightHeaderPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(rightHeaderPanel, Dock.Right);

            var toggle = new System.Windows.Controls.Primitives.ToggleButton
            {
                ToolTip = "Enable or disable the string"
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

            var btnSelect = new Button { Content = "Select PV module", Margin = new Thickness(8, 0, 0, 0) };
            btnSelect.Click += (s, e) => OnSelectModuleForMppt(cfg, sp);
            rightHeaderPanel.Children.Add(btnSelect);

            // Remove button: hidden until a module is selected
            var btnRemove = new Button { Content = "Remove module", Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
            btnRemove.Click += (s, e) => OnRemoveModuleForMppt(cfg, sp);
            rightHeaderPanel.Children.Add(btnRemove);

            header.Children.Add(rightHeaderPanel);
            sp.Children.Add(header);

            // Summary text
            var summary = new TextBlock { Text = "No PV module selected.", Margin = new Thickness(0, 6, 0, 8) };
            sp.Children.Add(summary);

            // Slider grid (3/4 left, 1/4 right)
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left column: modules per string
            var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 8, 0) };
            var lblN = new TextBlock { Text = "Modules per string" };
            var nSlider = new Slider
            {
                Minimum = 1,
                Maximum = 30, // initial value; set by the calculation
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
            nSlider.Value = cfg.ModulesPerString;
            nSlider.ValueChanged += (s, e) =>
            {
                if (_suppressSliderEvents) return;
                var val = (int)Math.Round(nSlider.Value);
                if (val < 1) val = 1;
                cfg.ModulesPerString = val;
                PersistLastSelection();
                Recalculate();
            };
            leftPanel.Children.Add(lblN);
            leftPanel.Children.Add(nSlider);

            // Right column: parallel strings
            var rightPanel2 = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(8, 0, 0, 0) };
            var lblS = new TextBlock { Text = "Parallel strings" };
            var specMaxStrings = _selectedInverter.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : 10;
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
            sSlider.Value = cfg.ParallelStrings;
            sSlider.ValueChanged += (ss, ee) =>
            {
                if (_suppressSliderEvents) return;
                var val = (int)Math.Round(sSlider.Value);
                if (val < 1) val = 1;
                var maxBySpec = _selectedInverter?.MaxStringsPerMppt ?? int.MaxValue;
                if (maxBySpec > 0 && val > maxBySpec) val = maxBySpec;
                cfg.ParallelStrings = val;
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

            // Keep references to the controls
            sp.Tag = new MpptUiRefs
            {
                SummaryText = summary,
                Toggle = toggle,
                SelectButton = btnSelect,
                RemoveButton = btnRemove,
                NSlider = nSlider,
                SSlider = sSlider
            };

            // Set the toggle from the configuration without raising events
            _suppressToggleEvents = true;
            toggle.IsChecked = cfg.IsEnabled;
            _suppressToggleEvents = false;

            // Initial control state
            UpdateMpptUiState(cfg, sp.Tag as MpptUiRefs);

            StringsPanel.Children.Add(card);
        }

        StringsCard.Visibility = Visibility.Visible;
    }

    // Controls of one MPPT section, so that sliders and toggle can be updated
    private sealed class MpptUiRefs
    {
        public TextBlock SummaryText { get; set; } = new TextBlock();
        public System.Windows.Controls.Primitives.ToggleButton Toggle { get; set; } = new System.Windows.Controls.Primitives.ToggleButton();
        public Button SelectButton { get; set; } = new Button();
        public Button RemoveButton { get; set; } = new Button();
        public Slider NSlider { get; set; } = new Slider();
        public Slider SSlider { get; set; } = new Slider();
    }

    // Updates buttons and summary of one MPPT section
    private void UpdateMpptUiState(StringConfiguration cfg, MpptUiRefs? refs)
    {
        if (refs is null) return;

        // Sync the toggle without raising events
        _suppressToggleEvents = true;
        refs.Toggle.IsChecked = cfg.IsEnabled;
        _suppressToggleEvents = false;

        // Summary
        refs.SummaryText.Text = cfg.SelectedModule is null
            ? "No PV module selected."
            : $"{cfg.SelectedModule.Manufacturer} {cfg.SelectedModule.Model} ({cfg.SelectedModule.PmaxW} W, Vmpp={cfg.SelectedModule.VmppV:F2} V, Impp={cfg.SelectedModule.ImppA:F2} A)";

        // Button texts and visibility
        refs.SelectButton.Content = cfg.SelectedModule is null ? "Select PV module" : "Change PV module";

        // The remove button is only visible when a module is selected
        refs.RemoveButton.Visibility = cfg.SelectedModule is null ? Visibility.Collapsed : Visibility.Visible;

        // Buttons follow the toggle state
        var enabled = cfg.IsEnabled;
        // The toggle itself stays enabled so the string can be enabled again
        refs.SelectButton.IsEnabled = enabled;
        refs.RemoveButton.IsEnabled = enabled && refs.RemoveButton.Visibility == Visibility.Visible;
    }

    #endregion

    #region String sections
    private void RefreshStringsUiSummaries()
    {
        if (_selectedInverter is null) return;
        var mpptCount = Math.Max(1, _selectedInverter.MpptCount);

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

    // Shows master data entries that could not be loaded, once the window is visible.
    private void ShowDataIssues()
    {
        IReadOnlyList<DataLoadIssue> issues = _dataStore.Issues;
        if (issues.Count == 0) return;

        const int MaxShown = 10;
        var text = new StringBuilder();
        text.AppendLine("Some master data could not be loaded:");
        text.AppendLine();
        foreach (DataLoadIssue issue in issues.Take(MaxShown))
        {
            text.AppendLine($"• {issue}");
        }

        if (issues.Count > MaxShown)
        {
            text.AppendLine($"• … and {issues.Count - MaxShown} more (see log).");
        }

        MessageBox.Show(this, text.ToString(), "Master data", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnSelectInverterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new SelectInverterWindow(_dataStore.Inverters);
            win.Owner = this;
            var res = win.ShowDialog();
            if (res == true && win.Selected is not null)
            {
                _selectedInverter = win.Selected;
                InverterSummaryText.Text =
                    $"{_selectedInverter.Manufacturer} {_selectedInverter.Model} | {CalculationTextFormatter.FormatInverterSummary(_selectedInverter)}";
                SelectInverterButton.Content = "Change inverter";

                _logger.LogInformation("Inverter selected: {Manufacturer} {Model}.", _selectedInverter.Manufacturer, _selectedInverter.Model);

                BuildStringsUi();
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the inverter selection.");
            MessageBox.Show("Error in the inverter selection. See the log for details.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSelectModuleForMppt(StringConfiguration cfg, StackPanel container)
    {
        try
        {
            var win = new SelectModuleWindow(_dataStore.Modules);
            win.Owner = this;
            var res = win.ShowDialog();
            if (res == true && win.Selected is not null)
            {
                cfg.SelectedModule = win.Selected;

                var refs = container.Tag as MpptUiRefs;
                UpdateMpptUiState(cfg, refs);

                _logger.LogInformation("PV module selected for MPPT {Mppt}: {Manufacturer} {Model}.", cfg.MpptIndex, cfg.SelectedModule.Manufacturer, cfg.SelectedModule.Model);
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the module selection.");
            MessageBox.Show("Error in the module selection. See the log for details.", "Error",
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
                // Hide the remove button and update the select button
                UpdateMpptUiState(cfg, refs);

                // Reset the sliders (modules 1..30, no allowed range)
                _suppressSliderEvents = true;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = 30;
                refs.NSlider.IsSelectionRangeEnabled = false;
                refs.NSlider.Value = Math.Max(1, cfg.ModulesPerString);

                // The string slider keeps the inverter specification
                var maxBySpec = _selectedInverter?.MaxStringsPerMppt ?? 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(1, maxBySpec);
                refs.SSlider.Value = Math.Max(1, Math.Min(cfg.ParallelStrings, (int)refs.SSlider.Maximum));
                _suppressSliderEvents = false;
            }

            PersistLastSelection();
            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing the module.");
            MessageBox.Show("Error removing the module. See the log for details.", "Error",
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
                    MessageBox.Show("The parameters could not be saved. They apply until the application is closed.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                _parameters = win.Parameters;
                _logger.LogInformation("Calculation parameters applied: Tmin={Tmin}, Tmax={Tmax}, safety margin={Margin} %.",
                    _parameters.MinTempC, _parameters.MaxTempC, _parameters.SafetyMarginPct);
                UpdateParametersSummary();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the parameter dialog.");
            MessageBox.Show("Error in the parameter dialog. See the log for details.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Calculation

    private void UpdateParametersSummary()
    {
        ParametersSummaryText.Text = $"Cell temperature Tmin={_parameters.MinTempC} °C, Tmax={_parameters.MaxTempC} °C, safety margin={_parameters.SafetyMarginPct} %";
    }

    private void TryRestoreLastSelection()
    {
        try
        {
            var last = _settings.LoadLastSelection();
            if (last.Inverter is null) return;

            var wr = _dataStore.Inverters.FirstOrDefault(x =>
                string.Equals(x.Manufacturer, last.Inverter.Manufacturer, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Model, last.Inverter.Model, StringComparison.OrdinalIgnoreCase));
            if (wr is null) return;

            _selectedInverter = wr;
            InverterSummaryText.Text =
                $"{_selectedInverter.Manufacturer} {_selectedInverter.Model} | {CalculationTextFormatter.FormatInverterSummary(_selectedInverter)}";
            SelectInverterButton.Content = "Change inverter";

            BuildStringsUi();

            foreach (var sel in last.Strings)
            {
                var cfg = _stringConfigs.FirstOrDefault(c => c.MpptIndex == sel.MpptIndex);
                if (cfg is null) continue;

                // Enabled state
                cfg.IsEnabled = sel.IsEnabled;

                // Counts, limited to the inverter specification
                cfg.ModulesPerString = Math.Max(1, sel.ModulesPerString);
                var maxBySpec = _selectedInverter.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : int.MaxValue;
                cfg.ParallelStrings = Math.Max(1, Math.Min(sel.ParallelStrings, maxBySpec));

                // Module reference
                if (sel.Module is not null)
                {
                    var mod = _dataStore.Modules.FirstOrDefault(x =>
                        string.Equals(x.Manufacturer, sel.Module.Manufacturer, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Model, sel.Module.Model, StringComparison.OrdinalIgnoreCase));
                    cfg.SelectedModule = mod;
                }
            }

            RefreshStringsUiSummaries();
            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring the last selection.");
        }
    }

    // Schedules saving the last selection. Slider drags change the configuration many times per second, so the
    // settings file is written once the input has been idle for the debounce interval, and on closing.
    private void PersistLastSelection()
    {
        if (_selectedInverter is null) return;

        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void FlushPendingPersist()
    {
        if (!_persistTimer.IsEnabled) return;

        _persistTimer.Stop();
        WriteLastSelection();
    }

    private void WriteLastSelection()
    {
        try
        {
            if (_selectedInverter is null) return;

            var sel = new LastSelection
            {
                Inverter = new SelectedRef
                {
                    Manufacturer = _selectedInverter.Manufacturer,
                    Model = _selectedInverter.Model
                },
                Strings = _stringConfigs.Select(c => new MpptStringSelection
                {
                    MpptIndex = c.MpptIndex,
                    ModulesPerString = c.ModulesPerString,
                    ParallelStrings = c.ParallelStrings,
                    IsEnabled = c.IsEnabled,
                    Module = c.SelectedModule is null ? null : new SelectedRef
                    {
                        Manufacturer = c.SelectedModule.Manufacturer,
                        Model = c.SelectedModule.Model
                    }
                }).ToList()
            };

            var ok = _settings.SaveLastSelection(sel);
            if (!ok)
            {
                _logger.LogWarning("The last selection could not be saved.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving the last selection.");
        }
    }

    private void Recalculate()
    {
        if (_selectedInverter is null)
        {
            ResultsOutput.Text = "Please select an inverter first.";
            BuildMarkdownReport();
            UpdateReportView();
            return;
        }

        StringCalculationResult result = StringCalculator.Calculate(_selectedInverter, _stringConfigs, _parameters);
        ResultsOutput.Text = CalculationTextFormatter.Format(result);

        if (result.HasValidMpptRange)
        {
            UpdateMpptControls(result);

            _logger.LogInformation("String calculation updated. OK={Ok}.", result.IsOk);
            if (!result.IsOk)
            {
                _logger.LogWarning("Restrictions: {Messages}", string.Join(" | ", CalculationTextFormatter.GetMessages(result)));
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
                cfg.ModulesPerString = Math.Clamp(cfg.ModulesPerString, 1, (int)refs.NSlider.Maximum);
                refs.NSlider.Value = cfg.ModulesPerString;

                // String slider: 1 .. inverter specification.
                int specMax = _selectedInverter?.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(1, specMax);
                cfg.ParallelStrings = Math.Clamp(cfg.ParallelStrings, 1, (int)refs.SSlider.Maximum);
                refs.SSlider.Value = cfg.ParallelStrings;
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

    #region Report

    // Builds the Markdown report from the current configuration.
    private void BuildMarkdownReport()
    {
        StringCalculationResult? result = _selectedInverter is null
            ? null
            : StringCalculator.Calculate(_selectedInverter, _stringConfigs, _parameters);
        _reportMarkdown = ReportBuilder.Build(result, _parameters, DateTime.Now);
        ReportMarkdownTextBox.Text = _reportMarkdown;
    }

    // Shows the Markdown source or the HTML preview
    private void UpdateReportView()
    {
        if (_reportPreviewMode)
        {
            ReportMarkdownTextBox.Visibility = Visibility.Collapsed;
            ReportPreviewContainer.Visibility = Visibility.Visible;

            var htmlBody = _markdown.ToHtml(_reportMarkdown);
            var htmlDoc = new StringBuilder();
            htmlDoc.AppendLine("<!DOCTYPE html>");
            htmlDoc.AppendLine("<html><head><meta charset=" + (char)34 + "utf-8" + (char)34 + "><title>Report</title></head><body>");
            htmlDoc.AppendLine(htmlBody);
            htmlDoc.AppendLine("</body></html>");

            try
            {
                ReportPreviewBrowser.NavigateToString(htmlDoc.ToString());
                _logger.LogInformation("Report preview updated. Length={Length}.", htmlBody.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating the report preview.");
                MessageBox.Show("Error updating the report preview. See the log for details.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            ReportPreviewContainer.Visibility = Visibility.Collapsed;
            ReportMarkdownTextBox.Visibility = Visibility.Visible;
        }
    }

    // Switches between Markdown and preview
    private void OnToggleReportViewClick(object sender, RoutedEventArgs e)
    {
        _reportPreviewMode = !_reportPreviewMode;
        UpdateReportView();
        _logger.LogInformation("Report view switched: {Mode}.", _reportPreviewMode ? "preview" : "Markdown");
    }

    // Copies the Markdown report
    private void OnCopyReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_reportMarkdown ?? string.Empty);
            _logger.LogInformation("Markdown report copied to the clipboard. Length={Length}.", (_reportMarkdown ?? string.Empty).Length);
            MessageBox.Show("The Markdown report was copied to the clipboard.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying the Markdown report.");
            MessageBox.Show("Error copying the report. See the log for details.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Saves the Markdown report
    private void OnSaveReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Markdown report",
                Filter = "Markdown file (*.md)|*.md|Text file (*.txt)|*.txt",
                FileName = "AnWa-Solar-Report.md",
                AddExtension = true,
                OverwritePrompt = true
            };
            var ok = dlg.ShowDialog(this);
            if (ok == true)
            {
                System.IO.File.WriteAllText(dlg.FileName, _reportMarkdown ?? string.Empty, new System.Text.UTF8Encoding(false));
                _logger.LogInformation("Markdown report saved: {Path}.", dlg.FileName);
                MessageBox.Show("The Markdown report was saved.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving the Markdown report.");
            MessageBox.Show("Error saving the report. See the log for details.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
