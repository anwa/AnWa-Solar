using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;
using AnWaSoft.Design.Wpf.Theming;

using AnWaSolar.Core;
using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Logging;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Projects;
using AnWaSolar.Core.Services;
using AnWaSolar.Services;

using MaterialDesignThemes.Wpf;

using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace AnWaSolar.Windows;

public partial class MainWindow : ChromeWindow
{
    #region Fields

    private readonly ILogger<MainWindow> _logger;
    private readonly IMarkdownService _markdown;
    private readonly IDataStore _dataStore;
    private readonly ISettingsService _settings;
    private readonly IUserDataLocation _userData;
    private readonly ILogFilePathProvider _logPaths;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ThemeService _theme;
    private readonly DatasheetService _datasheets;

    private Inverter? _selectedInverter;
    private readonly List<StringConfiguration> _stringConfigs = new();
    private readonly List<MpptUiRefs> _mpptRefs = new();

    // String configurations by MPPT number that the current inverter does not have; they come back when an inverter
    // with more MPPTs is selected again.
    private readonly Dictionary<int, StringConfiguration> _parkedConfigs = new();
    private StringCalculationResult? _lastResult;
    private CableCalculationResult? _lastCables;
    private CableSettings _cableSettings = new();
    private BatteryCalculationResult? _lastBattery;
    private BatterySettings _batterySettings = new();

    // The open project: everything entered is saved into it automatically.
    private readonly IProjectService _projects;
    private Project _project = new() { Name = L.T("Project") };
    private string? _projectPath;

    // Entries that are missing in the master data and come from the copy in the project.
    private readonly List<string> _fromProjectCopy = [];

    private CalculationParameters _parameters;

    // Report
    private string _reportMarkdown = string.Empty;
    private bool _reportPreviewMode = true;
    private string _previewHtml = string.Empty;
    private Task? _previewInit;
    private ManualWindow? _manualWindow;

    // Local host name of the report preview; requests to it are answered by OnPreviewResourceRequested.
    private const string PreviewHost = "report.anwasolar.invalid";
    private const string PreviewPageUrl = "https://" + PreviewHost + "/report.html";
    private static string ReportTitle => L.T("AnWa-Solar report");
    private TaskCompletionSource<bool>? _navigation;
    private bool _exportingReport;
    private ulong _navigationId;
    private static readonly string s_katexDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "katex");

    // Suppress slider and toggle events while values are set programmatically
    private bool _suppressSliderEvents = false;
    private bool _suppressToggleEvents = false;

    // Debounced saving of the last selection
    private static readonly TimeSpan s_persistDelay = TimeSpan.FromMilliseconds(500);
    private readonly DispatcherTimer _persistTimer;

    #endregion

    #region Constructor

    public MainWindow(
        ILogger<MainWindow> logger,
        ILoggerFactory loggerFactory,
        IMarkdownService markdown,
        IDataStore dataStore,
        ISettingsService settings,
        IUserDataLocation userData,
        ILogFilePathProvider logPaths,
        ThemeService theme,
        DatasheetService datasheets,
        IProjectService projects)
    {
        _datasheets = datasheets;
        _projects = projects;
        _persistTimer = new DispatcherTimer { Interval = s_persistDelay };
        _persistTimer.Tick += (_, _) =>
        {
            _persistTimer.Stop();
            WriteProject();
        };
        Closing += (_, _) => FlushPendingPersist();
        Loaded += (_, _) => ShowDataIssues();

        InitializeComponent();
        Title = L.F($"AnWa-Solar {AppVersion.Current}");
        CopyrightText.Text = AppInfo.Copyright(typeof(MainWindow).Assembly);
        _logger = logger;
        _markdown = markdown;
        _dataStore = dataStore;
        _settings = settings;
        _userData = userData;
        _logPaths = logPaths;
        _loggerFactory = loggerFactory;
        _theme = theme;
        _theme.EffectiveThemeChanged += OnEffectiveThemeChanged;
        Closed += (_, _) => _theme.EffectiveThemeChanged -= OnEffectiveThemeChanged;
        UpdateThemeButton();
        InputBindings.Add(new KeyBinding(new ActionCommand(OpenManual), Key.F1, ModifierKeys.None));

        _logger.LogInformation("Main window initialized.");

        // The project is opened by OpenProject before the window is shown.
        _parameters = new CalculationParameters();
    }

    /// <summary>Shows a project; the previous project is saved first.</summary>
    public void OpenProject(string path, Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        FlushPendingPersist();

        _projectPath = path;
        _project = project;
        _parameters = project.Parameters.Clone();
        _cableSettings = project.Cables.Clone();
        _batterySettings = project.Battery.Clone();
        _selectedInverter = null;
        _stringConfigs.Clear();
        _parkedConfigs.Clear();
        UpdateParametersSummary();
        UpdateProjectCard();
        if (!_settings.AddRecentProject(path))
        {
            _logger.LogWarning("The list of recent projects could not be saved.");
        }

        RestoreFromProject();
        if (_selectedInverter is null)
        {
            ShowInverter();
            BuildStringsUi();
            Recalculate();
        }

        _logger.LogInformation("Project {Name} opened from {Path}.", project.Name, path);
        if (_fromProjectCopy.Count > 0)
        {
            Dispatcher.BeginInvoke(new Action(() => MessageDialog.Show(this,
                L.T("These entries are not in the master data; the values saved in the project are used:\n\n• ") + string.Join("\n• ", _fromProjectCopy)
                + L.T("\n\nImport their datasheets to add them to the master data."), L.T("Project"), StatusKind.Warning)), DispatcherPriority.ApplicationIdle);
        }
    }

    private void UpdateProjectCard()
    {
        Title = L.F($"AnWa-Solar {AppVersion.Current} – {_project.Name}");
        ProjectNameText.Text = _project.Name;
        string details = string.Join(" · ", new[] { _project.Customer, _project.Address?.ReplaceLineEndings(", ") }.Where(s => !string.IsNullOrWhiteSpace(s)));
        ProjectDetailsText.Text = details;
        ProjectDetailsText.Visibility = details.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    #endregion

    #region String sections

    // Creates one configuration card per MPPT of _selectedInverter. The module, the counts and the enabled state of
    // every MPPT number are kept: changing the inverter does not change the strings; values the new inverter does not
    // allow are shown as violations.
    private void BuildStringsUi()
    {
        foreach (StringConfiguration previous in _stringConfigs)
        {
            _parkedConfigs[previous.MpptIndex] = previous;
        }

        StringsPanel.Children.Clear();
        _stringConfigs.Clear();
        _mpptRefs.Clear();

        if (_selectedInverter is null)
        {
            StringsLabel.Visibility = Visibility.Collapsed;
            return;
        }

        int mpptCount = Math.Max(1, _selectedInverter.MpptCount);
        int specMaxStrings = _selectedInverter.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : 10;

        for (int i = 0; i < mpptCount; i++)
        {
            var cfg = new StringConfiguration { MpptIndex = i + 1, ModulesPerString = 10, ParallelStrings = 1, IsEnabled = true };
            if (_parkedConfigs.Remove(cfg.MpptIndex, out StringConfiguration? kept))
            {
                cfg.ModulesPerString = kept.ModulesPerString;
                cfg.ParallelStrings = kept.ParallelStrings;
                cfg.IsEnabled = kept.IsEnabled;
                cfg.SelectedModule = FindModule(kept.SelectedModule);
            }

            _stringConfigs.Add(cfg);

            var root = new StackPanel();
            var card = new Card { Content = root, Style = (Style)FindResource("AnWaSoft.Card") };
            AutomationProperties.SetName(card, L.F($"MPPT {cfg.MpptIndex} configuration"));

            // Header: title and the enable switch
            var header = new DockPanel();
            var toggle = new ToggleButton
            {
                ToolTip = L.T("Enable or disable this MPPT"),
                Style = (Style)FindResource("MaterialDesignSwitchToggleButton"),
            };
            AutomationProperties.SetName(toggle, L.F($"MPPT {cfg.MpptIndex} enabled"));
            DockPanel.SetDock(toggle, Dock.Right);
            header.Children.Add(toggle);
            var icon = new PackIcon { Kind = PackIconKind.SolarPanel, Width = 22, Height = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(ForegroundProperty, "MaterialDesign.Brush.Primary");
            header.Children.Add(icon);
            header.Children.Add(new TextBlock { Text = $"MPPT {cfg.MpptIndex}", Style = (Style)FindResource("AnWaSoft.Text.CardTitle") });
            root.Children.Add(header);

            var body = new StackPanel();
            root.Children.Add(body);

            // Module: name and key values, select and remove buttons
            var moduleBox = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 8, 10), Margin = new Thickness(0, 12, 0, 0) };
            moduleBox.SetResourceReference(Border.BackgroundProperty, "AnWaSoft.Brush.PanelBackground");
            var moduleRow = new DockPanel();
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(buttons, Dock.Right);
            var selectText = new TextBlock { Text = L.T("Select module"), VerticalAlignment = VerticalAlignment.Center };
            var btnSelect = new Button
            {
                Style = (Style)FindResource("MaterialDesignOutlinedButton"),
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Content = IconText(PackIconKind.SwapHorizontal, selectText),
            };
            AutomationProperties.SetName(btnSelect, L.F($"Select PV module for MPPT {cfg.MpptIndex}"));
            btnSelect.Click += (_, _) => OnSelectModuleForMppt(cfg);
            var btnRemove = new Button
            {
                Style = (Style)FindResource("MaterialDesignIconButton"),
                Width = 32,
                Height = 32,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = L.T("Remove the PV module"),
                Content = new PackIcon { Kind = PackIconKind.DeleteOutline, Width = 18, Height = 18 },
                Visibility = Visibility.Collapsed,
            };
            AutomationProperties.SetName(btnRemove, L.F($"Remove PV module from MPPT {cfg.MpptIndex}"));
            btnRemove.Click += (_, _) => OnRemoveModuleForMppt(cfg);
            var btnDatasheet = new Button
            {
                Style = (Style)FindResource("MaterialDesignIconButton"),
                Width = 32,
                Height = 32,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = L.T("Show the datasheet of the module"),
                Content = new PackIcon { Kind = PackIconKind.FilePdfBox, Width = 18, Height = 18 },
                Visibility = Visibility.Collapsed,
            };
            AutomationProperties.SetName(btnDatasheet, L.F($"Datasheet of the PV module of MPPT {cfg.MpptIndex}"));
            btnDatasheet.Click += (_, _) =>
            {
                if (cfg.SelectedModule is { } selected)
                {
                    _datasheets.Show(this, selected);
                }
            };
            buttons.Children.Add(btnSelect);
            buttons.Children.Add(btnDatasheet);
            buttons.Children.Add(btnRemove);
            moduleRow.Children.Add(buttons);
            var moduleText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var moduleName = new TextBlock { FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var moduleSpecs = Muted(string.Empty, 12);
            moduleText.Children.Add(moduleName);
            moduleText.Children.Add(moduleSpecs);
            moduleRow.Children.Add(moduleText);
            moduleBox.Child = moduleRow;
            body.Children.Add(moduleBox);

            // Modules per string
            (DockPanel nRow, TextBlock nValue) = SliderHeader(L.T("Modules per string"));
            nRow.Margin = new Thickness(0, 14, 0, 0);
            var nSlider = new Slider { Minimum = 1, Maximum = Math.Max(30, cfg.ModulesPerString), TickFrequency = 1, IsSnapToTickEnabled = true, Value = cfg.ModulesPerString };
            AutomationProperties.SetName(nSlider, L.F($"Modules per string, MPPT {cfg.MpptIndex}"));
            var nAllowed = Muted(string.Empty, 12);
            body.Children.Add(nRow);
            body.Children.Add(nSlider);
            body.Children.Add(nAllowed);

            // Parallel strings
            (DockPanel sRow, TextBlock sValue) = SliderHeader(L.T("Parallel strings"));
            sRow.Margin = new Thickness(0, 12, 0, 0);
            var sSlider = new Slider { Minimum = 1, Maximum = Math.Max(specMaxStrings, cfg.ParallelStrings), TickFrequency = 1, IsSnapToTickEnabled = true, Value = cfg.ParallelStrings };
            AutomationProperties.SetName(sSlider, L.F($"Parallel strings, MPPT {cfg.MpptIndex}"));
            var sAllowed = Muted(_selectedInverter.MaxStringsPerMppt > 0
                ? L.F($"The inverter allows up to {_selectedInverter.MaxStringsPerMppt} strings per MPPT.")
                : L.T("The datasheet states no maximum number of strings per MPPT."), 12);
            body.Children.Add(sRow);
            body.Children.Add(sSlider);
            body.Children.Add(sAllowed);

            var refs = new MpptUiRefs(toggle, body, moduleName, moduleSpecs, selectText, btnSelect, btnRemove, btnDatasheet, nSlider, nValue, nAllowed, sSlider, sValue);
            _mpptRefs.Add(refs);

            toggle.Checked += (_, _) => OnMpptToggled(cfg, refs, true);
            toggle.Unchecked += (_, _) => OnMpptToggled(cfg, refs, false);
            nSlider.ValueChanged += (_, _) =>
            {
                nValue.Text = ((int)Math.Round(nSlider.Value)).ToString(CultureInfo.CurrentCulture);
                if (_suppressSliderEvents) return;
                cfg.ModulesPerString = Math.Max(1, (int)Math.Round(nSlider.Value));
                PersistLastSelection();
                Recalculate();
            };
            sSlider.ValueChanged += (_, _) =>
            {
                sValue.Text = ((int)Math.Round(sSlider.Value)).ToString(CultureInfo.CurrentCulture);
                if (_suppressSliderEvents) return;
                cfg.ParallelStrings = Math.Max(1, (int)Math.Round(sSlider.Value));
                PersistLastSelection();
                Recalculate();
            };
            nValue.Text = cfg.ModulesPerString.ToString(CultureInfo.CurrentCulture);
            sValue.Text = cfg.ParallelStrings.ToString(CultureInfo.CurrentCulture);

            UpdateMpptUiState(cfg, refs);
            StringsPanel.Children.Add(card);
        }

        StringsLabel.Visibility = Visibility.Visible;
    }

    private void OnMpptToggled(StringConfiguration cfg, MpptUiRefs refs, bool enabled)
    {
        if (_suppressToggleEvents) return;
        cfg.IsEnabled = enabled;
        UpdateMpptUiState(cfg, refs);
        PersistLastSelection();
        Recalculate();
    }

    // Controls of one MPPT card, so that sliders, texts and the switch can be updated
    private sealed record MpptUiRefs(
        ToggleButton Toggle,
        FrameworkElement Body,
        TextBlock ModuleName,
        TextBlock ModuleSpecs,
        TextBlock SelectText,
        Button SelectButton,
        Button RemoveButton,
        Button DatasheetButton,
        Slider NSlider,
        TextBlock NValue,
        TextBlock NAllowed,
        Slider SSlider,
        TextBlock SValue);

    // Updates switch, module texts and buttons of one MPPT card
    private void UpdateMpptUiState(StringConfiguration cfg, MpptUiRefs? refs)
    {
        if (refs is null) return;

        _suppressToggleEvents = true;
        refs.Toggle.IsChecked = cfg.IsEnabled;
        _suppressToggleEvents = false;

        PVModule? module = cfg.SelectedModule;
        refs.ModuleName.Text = module is null ? L.T("No PV module selected") : $"{module.Manufacturer} {module.Model}";
        refs.ModuleSpecs.Text = module is null
            ? L.T("Select a module from the master data.")
            : L.F($"{module.PmaxW} W · Voc {module.VocV:F2} V · Vmpp {module.VmppV:F2} V · Impp {module.ImppA:F2} A");
        refs.SelectText.Text = module is null ? L.T("Select module") : L.T("Change");
        refs.RemoveButton.Visibility = module is null ? Visibility.Collapsed : Visibility.Visible;
        refs.DatasheetButton.Visibility = _datasheets.HasDatasheet(module) ? Visibility.Visible : Visibility.Collapsed;

        // The switch stays enabled so that the MPPT can be enabled again
        bool enabled = cfg.IsEnabled;
        refs.Body.IsEnabled = enabled;
        refs.Body.Opacity = enabled ? 1.0 : 0.45;
        if (module is null)
        {
            refs.NAllowed.Text = L.T("The allowed range is shown once a module is selected.");
        }
    }

    private static StackPanel IconText(PackIconKind kind, TextBlock text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new PackIcon { Kind = kind, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(text);
        return panel;
    }

    private TextBlock Muted(string text, double fontSize)
    {
        return new TextBlock { Text = text, FontSize = fontSize, Style = (Style)FindResource("AnWaSoft.Text.Muted") };
    }

    private static (DockPanel Row, TextBlock Value) SliderHeader(string label)
    {
        var row = new DockPanel();
        var value = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return (row, value);
    }

    // Label and value of an inverter or parameter value; a tile has a tinted background and a larger value.
    private FrameworkElement ValueTile(string label, string value, bool tile)
    {
        var panel = new StackPanel();
        panel.Children.Add(Muted(label, 11));
        panel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = tile ? 18 : 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = value,
        });
        if (!tile)
        {
            panel.Margin = new Thickness(0, 8, 8, 0);
            return panel;
        }

        var border = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 8, 0), Child = panel };
        border.SetResourceReference(Border.BackgroundProperty, "AnWaSoft.Brush.PanelBackground");
        return border;
    }

    // Shows the selected inverter with its datasheet values.
    private void ShowInverter()
    {
        InverterSpecs.Children.Clear();
        InverterDatasheetButton.Visibility = _datasheets.HasDatasheet(_selectedInverter) ? Visibility.Visible : Visibility.Collapsed;
        if (_selectedInverter is not { } inv)
        {
            InverterNameText.Text = L.T("No inverter selected");
            SelectInverterText.Text = L.T("Select");
            InverterSpecs.Children.Add(Muted(L.T("Select an inverter from the master data to start."), 13));
            InverterSpecs.Columns = 1;
            return;
        }

        InverterNameText.Text = $"{inv.Manufacturer} {inv.Model}";
        SelectInverterText.Text = L.T("Change");
        InverterSpecs.Columns = 3;
        CultureInfo c = CultureInfo.CurrentCulture;
        InverterSpecs.Children.Add(ValueTile(L.T("Max. DC voltage"), string.Create(c, $"{inv.MaxDcInputVoltageV} V"), false));
        InverterSpecs.Children.Add(ValueTile(L.T("MPPT range"), $"{inv.MpptVoltageRangeV} V", false));
        InverterSpecs.Children.Add(ValueTile(L.T("Start voltage"), string.Create(c, $"{inv.StartVoltageV} V"), false));
        InverterSpecs.Children.Add(ValueTile(L.T("Max. input current"), string.Create(c, $"{inv.MaxInputCurrentA} A"), false));
        InverterSpecs.Children.Add(ValueTile(L.T("Max. Isc"), string.Create(c, $"{inv.MaxShortCircuitCurrentA} A"), false));
        InverterSpecs.Children.Add(ValueTile(L.T("AC power"), inv.RatedAcPowerW > 0
            ? string.Create(c, $"{inv.RatedAcPowerW / 1000:0.#} kW") + (inv.AcPhases > 0 ? L.F($" · {inv.AcPhases}-phase") : string.Empty)
            : L.T("not stated"), false));
        InverterSpecs.Children.Add(ValueTile(L.T("MPPTs / strings"), string.Create(c, $"{inv.MpptCount} / {(inv.MaxStringsPerMppt > 0 ? inv.MaxStringsPerMppt.ToString(c) : L.T("n/a"))}"), false));
    }

    #endregion

    #region Top bar

    // Switches System → Light → Dark → System and saves the choice.
    // Save dialogs of exports start in the export folder of the preferences.
    private void ApplyExportFolder(SaveFileDialog dialog)
    {
        if (_settings.LoadPreferences().ExportFolder is { } folder && Directory.Exists(folder))
        {
            dialog.InitialDirectory = folder;
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        string defaultProjects = Path.Combine(_userData.DirectoryPath, ProjectService.FolderName);
        var window = new SettingsWindow(_settings.LoadPreferences(), _settings.LoadTheme(), _dataStore.CableTables, defaultProjects) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (!_settings.SavePreferences(window.Preferences) || !_settings.SaveTheme(window.Theme))
        {
            MessageDialog.Show(this, L.T("The settings could not be saved. See the log for details."), L.T("Settings"), StatusKind.Error);
            return;
        }

        _theme.Apply(ThemeMapping.ToPreference(window.Theme));
        UpdateThemeButton();
        if (window.RestartNeeded && MessageDialog.Confirm(this, L.T("The language and the projects folder apply after a restart. Restart AnWa-Solar now?"),
            L.T("Settings"), L.T("Restart"), L.T("Later")))
        {
            FlushPendingPersist();
            WriteProject();
            if (Environment.ProcessPath is { } exe)
            {
                System.Diagnostics.Process.Start(exe);
            }

            Application.Current.Shutdown();
        }
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        ThemePreference next = ThemeService.Next(_theme.Preference);
        _theme.Apply(next);
        if (!_settings.SaveTheme(ThemeMapping.ToAppTheme(next)))
        {
            _logger.LogWarning("The theme could not be saved.");
        }

        UpdateThemeButton();
    }

    private void OnEffectiveThemeChanged(object? sender, bool dark)
    {
        WebViewSupport.ApplyColorScheme(ReportPreviewBrowser.CoreWebView2, dark);
        UpdateThemeButton();
    }

    private void UpdateThemeButton()
    {
        ThemeIcon.Kind = ThemeService.IconFor(_theme.Preference);
        ThemeButton.ToolTip = _theme.Preference switch
        {
            ThemePreference.Light => L.T("Theme: light (click for dark)"),
            ThemePreference.Dark => L.T("Theme: dark (click for system)"),
            _ => L.F($"Theme: system, currently {(_theme.IsDark ? L.T("dark") : L.T("light"))} (click for light)"),
        };
    }

    private void OnManualClick(object sender, RoutedEventArgs e) => OpenManual();

    // Opens the manual, or brings an open manual window to the front.
    private void OpenManual()
    {
        if (_manualWindow is { IsLoaded: true })
        {
            _manualWindow.Activate();
            return;
        }

        _manualWindow = new ManualWindow(_markdown, _userData, _theme, _loggerFactory.CreateLogger<ManualWindow>());
        _manualWindow.Closed += (_, _) => _manualWindow = null;
        _manualWindow.Show();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow(_userData, _logPaths) { Owner = this };
        about.ShowDialog();
    }

    /// <summary>Minimal ICommand for key bindings.</summary>
    private sealed class ActionCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
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
        text.AppendLine(L.T("Some master data could not be loaded:"));
        text.AppendLine();
        foreach (DataLoadIssue issue in issues.Take(MaxShown))
        {
            text.AppendLine($"• {issue}");
        }

        if (issues.Count > MaxShown)
        {
            text.AppendLine(L.F($"• … and {issues.Count - MaxShown} more (see log)."));
        }

        MessageDialog.Show(this, text.ToString().TrimEnd(), L.T("Master data"), StatusKind.Warning);
    }

    private void OnSelectInverterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new SelectInverterWindow(_datasheets) { Owner = this };
            bool accepted = win.ShowDialog() == true;
            if (win.Imported)
            {
                ReloadMasterData();
            }

            if (accepted && win.Selected is not null)
            {
                _selectedInverter = win.Selected;
                ShowInverter();
                _logger.LogInformation("Inverter selected: {Manufacturer} {Model}.", _selectedInverter.Manufacturer, _selectedInverter.Model);

                BuildStringsUi();
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the inverter selection.");
            MessageDialog.Show(this, L.T("Error in the inverter selection. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    private void OnSelectModuleForMppt(StringConfiguration cfg)
    {
        try
        {
            var win = new SelectModuleWindow(_datasheets) { Owner = this };
            bool accepted = win.ShowDialog() == true;
            if (win.Imported)
            {
                ReloadMasterData();
            }

            if (accepted && win.Selected is not null)
            {
                cfg.SelectedModule = win.Selected;
                UpdateMpptUiState(cfg, RefsOf(cfg));

                _logger.LogInformation("PV module selected for MPPT {Mppt}: {Manufacturer} {Model}.", cfg.MpptIndex, cfg.SelectedModule.Manufacturer, cfg.SelectedModule.Model);
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the module selection.");
            MessageDialog.Show(this, L.T("Error in the module selection. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    private void OnRemoveModuleForMppt(StringConfiguration cfg)
    {
        try
        {
            cfg.SelectedModule = null;

            if (RefsOf(cfg) is { } refs)
            {
                UpdateMpptUiState(cfg, refs);

                // Reset the sliders (modules 1..30, no allowed range); the string slider keeps the inverter specification
                _suppressSliderEvents = true;
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = Math.Max(30, cfg.ModulesPerString);
                refs.NSlider.IsSelectionRangeEnabled = false;
                refs.NSlider.Value = Math.Max(1, cfg.ModulesPerString);

                int maxBySpec = _selectedInverter?.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(maxBySpec, cfg.ParallelStrings);
                refs.SSlider.Value = Math.Max(1, cfg.ParallelStrings);
                _suppressSliderEvents = false;
            }

            PersistLastSelection();
            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing the module.");
            MessageDialog.Show(this, L.T("Error removing the module. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    private void OnEditParametersClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new ParametersWindow(_parameters) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _parameters = win.Parameters;
                PersistLastSelection();
                _logger.LogInformation("Calculation parameters applied: Tmin={Tmin}, Tmax={Tmax}, safety margin={Margin} %.",
                    _parameters.MinTempC, _parameters.MaxTempC, _parameters.SafetyMarginPct);
                UpdateParametersSummary();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the parameter dialog.");
            MessageDialog.Show(this, L.T("Error in the parameter dialog. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Imports a PDF datasheet into the own master data.
    private async void OnImportDatasheetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await _datasheets.ImportAsync(this, preferredKind: null) is { } entry)
            {
                ReloadMasterData();
                Snackbar.MessageQueue?.Enqueue(L.F($"{entry.Manufacturer} {entry.Model} was saved and can be selected now."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogError(ex, "Error importing a datasheet.");
            MessageDialog.Show(this, L.T("The datasheet could not be imported. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Opens the component database; the selection is resolved again when the master data changed.
    private void OnComponentDatabaseClick(object sender, RoutedEventArgs e)
    {
        var window = new ComponentDatabaseWindow(_datasheets) { Owner = this };
        window.ShowDialog();
        if (window.Changed)
        {
            ReloadMasterData();
            Snackbar.MessageQueue?.Enqueue(L.T("The master data was updated."));
        }
    }

    private void OnInverterDatasheetClick(object sender, RoutedEventArgs e)
    {
        if (_selectedInverter is not null)
        {
            _datasheets.Show(this, _selectedInverter);
        }
    }

    // The master data changed (import): the selection is resolved again, so that the calculation uses the saved values.
    private void ReloadMasterData()
    {
        _persistTimer.Stop();
        WriteProject();
        RestoreFromProject();
    }

    private void OnEditCablesClick(object sender, RoutedEventArgs e)
    {
        if (_dataStore.CableTables is not { } tables)
        {
            MessageDialog.Show(this, L.T("The cable tables (Data/CableTables.json) could not be loaded; cables and fuses cannot be sized."), L.T("Cables"), StatusKind.Warning);
            return;
        }

        var win = new CableSettingsWindow(_cableSettings, tables, _selectedInverter?.MpptCount ?? 1) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _cableSettings = win.Settings;
            PersistLastSelection();
            Recalculate();
        }
    }

    private void UpdateCableSummary()
    {
        CultureInfo c = CultureInfo.CurrentCulture;
        int mppts = _selectedInverter?.MpptCount ?? 0;
        string dc = mppts == 0
            ? L.T("DC: select an inverter")
            : "DC " + string.Join(", ", Enumerable.Range(1, mppts).Select(i => _cableSettings.DcLength(i) > 0
                ? L.F($"MPPT {i} {_cableSettings.DcLength(i):0.#} m")
                : L.F($"MPPT {i} –")));
        string ac = _cableSettings.AcLengthM > 0 ? L.F($"AC {_cableSettings.AcLengthM:0.#} m, method {_cableSettings.AcMethod}") : L.T("AC –");
        string battery = !_batterySettings.Enabled ? string.Empty
            : _cableSettings.BatteryCableLengthM <= 0 ? L.T(" · battery –")
            : _cableSettings.BatteryBusbar
                ? L.F($" · battery {_cableSettings.BatteryCableLengthM:0.#} m + busbar {_cableSettings.BatteryMainLengthM:0.#} m")
                : L.F($" · battery {_cableSettings.BatteryCableLengthM:0.#} m");
        CableSummaryText.Text = $"{dc} · {ac}{battery}";
    }

    private void OnEditBatteryClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new BatterySettingsWindow(_batterySettings, _selectedInverter, _datasheets) { Owner = this };
            bool accepted = win.ShowDialog() == true;
            if (win.Imported)
            {
                ReloadMasterData();
            }

            if (accepted)
            {
                _batterySettings = win.Settings;
                PersistLastSelection();
                Recalculate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in the battery dialog.");
            MessageDialog.Show(this, L.T("Error in the battery dialog. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // The current master data entry of a module (after an import the entry object is new).
    private PVModule? FindModule(PVModule? module) => module is null ? null : _dataStore.Modules.FirstOrDefault(m =>
        string.Equals(m.Manufacturer, module.Manufacturer, StringComparison.OrdinalIgnoreCase)
        && string.Equals(m.Model, module.Model, StringComparison.OrdinalIgnoreCase));

    // The master data entry of the battery, or the copy in the project when the master data lacks it.
    private Battery? FindBattery(SelectedRef? reference)
    {
        if (reference is null)
        {
            return null;
        }

        static bool Same(IMasterDataEntry e, SelectedRef r) => string.Equals(e.Manufacturer, r.Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Model, r.Model, StringComparison.OrdinalIgnoreCase);
        return _dataStore.Batteries.FirstOrDefault(b => Same(b, reference))
            ?? (_project.BatteryData is { } copy && Same(copy, reference) ? copy : null);
    }

    // The master data entry with the key of a copy, or the copy itself (noted) when the master data lacks it.
    private T? Resolve<T>(IEnumerable<T> entries, T? copy, string kind)
        where T : class, IMasterDataEntry
    {
        if (copy is null)
        {
            return null;
        }

        T? entry = entries.FirstOrDefault(e => string.Equals(e.Manufacturer, copy.Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Model, copy.Model, StringComparison.OrdinalIgnoreCase));
        if (entry is null && !_fromProjectCopy.Contains($"{kind} {copy.Manufacturer} {copy.Model}"))
        {
            _fromProjectCopy.Add($"{kind} {copy.Manufacturer} {copy.Model}");
        }

        return entry ?? copy;
    }

    // The battery card is shown for hybrid inverters and while a battery is planned.
    private void UpdateBatterySummary()
    {
        bool hybrid = _selectedInverter?.HasBatteryInput == true;
        BatteryCard.Visibility = _selectedInverter is not null && (hybrid || _batterySettings.Enabled) ? Visibility.Visible : Visibility.Collapsed;
        BatterySummaryText.Text = !_batterySettings.Enabled
            ? (hybrid ? L.T("No battery planned") : L.T("The inverter has no battery input"))
            : _batterySettings.Battery is { } b
                ? string.Create(CultureInfo.CurrentCulture, $"{b.Manufacturer} {b.Model} · {_batterySettings.InSeries} × {_batterySettings.InParallel}")
                : L.T("No battery selected");
    }

    // Copies the results as plain text.
    private void OnCopyResultsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = _lastResult is null ? ResultPresenter.Present(null).Details : CalculationTextFormatter.Format(_lastResult, _lastCables, _lastBattery);
            Clipboard.SetText(text);
            Snackbar.MessageQueue?.Enqueue(L.T("Results copied to the clipboard."));
        }
        catch (Exception ex) when (ex is COMException or ExternalException)
        {
            _logger.LogError(ex, "Error copying the results.");
            MessageDialog.Show(this, L.T("The results could not be copied. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    #endregion

    #region Calculation

    private void UpdateParametersSummary()
    {
        ParameterTiles.Children.Clear();
        CultureInfo c = CultureInfo.CurrentCulture;
        ParameterTiles.Children.Add(ValueTile(L.T("Min. cell temp."), string.Create(c, $"{_parameters.MinTempC} °C"), true));
        ParameterTiles.Children.Add(ValueTile(L.T("Max. cell temp."), string.Create(c, $"{_parameters.MaxTempC} °C"), true));
        ParameterTiles.Children.Add(ValueTile(L.T("Margin"), string.Create(c, $"{_parameters.SafetyMarginPct} %"), true));
    }

    // Shows the design of the project: master data entries by their key, the copies in the project when missing.
    private void RestoreFromProject()
    {
        try
        {
            _fromProjectCopy.Clear();
            if (Resolve(_dataStore.Batteries, _project.BatteryData, L.T("Battery")) is { } battery && _batterySettings.Battery is null)
            {
                _batterySettings.Battery = new SelectedRef { Manufacturer = battery.Manufacturer, Model = battery.Model };
            }

            if (Resolve(_dataStore.Inverters, _project.Inverter, L.T("Inverter")) is not { } wr) return;

            _selectedInverter = wr;
            ShowInverter();
            BuildStringsUi();

            foreach (ProjectString sel in _project.Strings)
            {
                var cfg = _stringConfigs.FirstOrDefault(c => c.MpptIndex == sel.MpptIndex);
                if (cfg is null) continue;

                // Enabled state
                cfg.IsEnabled = sel.IsEnabled;

                // Counts as saved; values the inverter does not allow are shown as violations
                cfg.ModulesPerString = Math.Max(1, sel.ModulesPerString);
                cfg.ParallelStrings = Math.Max(1, sel.ParallelStrings);

                // Module: the master data entry, or the copy in the project
                cfg.SelectedModule = Resolve(_dataStore.Modules, sel.Module, L.T("PV module"));

                UpdateMpptUiState(cfg, RefsOf(cfg));
            }

            Recalculate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring the project.");
        }
    }

    // Schedules saving the project. Slider drags change the configuration many times per second, so the project file
    // is written once the input has been idle for the debounce interval, and on closing or switching the project.
    private void PersistLastSelection()
    {
        if (_projectPath is null) return;

        _persistTimer.Stop();
        _persistTimer.Start();
    }

    private void FlushPendingPersist()
    {
        if (!_persistTimer.IsEnabled) return;

        _persistTimer.Stop();
        WriteProject();
    }

    // Writes the current design with copies of the selected master data entries into the project file.
    private void WriteProject()
    {
        if (_projectPath is null) return;

        try
        {
            _project.Parameters = _parameters.Clone();
            _project.Cables = _cableSettings.Clone();
            _project.Battery = _batterySettings.Clone();
            _project.BatteryData = _batterySettings.Battery is null ? null : FindBattery(_batterySettings.Battery);
            if (_selectedInverter is not null)
            {
                _project.Inverter = _selectedInverter;
                _project.Strings = [.. _stringConfigs.Select(c => new ProjectString
                {
                    MpptIndex = c.MpptIndex,
                    Module = c.SelectedModule,
                    ModulesPerString = c.ModulesPerString,
                    ParallelStrings = c.ParallelStrings,
                    IsEnabled = c.IsEnabled,
                })];
            }

            _projects.Save(_projectPath, _project);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error saving the project {Path}.", _projectPath);
            Snackbar.MessageQueue?.Enqueue(L.T("The project could not be saved. See the log for details."));
        }
    }

    // Runs the calculation and shows the result, the MPPT controls and the report.
    private void Recalculate()
    {
        _lastResult = _selectedInverter is null
            ? null
            : StringCalculator.Calculate(_selectedInverter, _stringConfigs, _parameters);

        _lastBattery = _lastResult is { HasValidMpptRange: true } && _batterySettings.Enabled
            ? BatteryCalculator.Calculate(_lastResult.Inverter, FindBattery(_batterySettings.Battery), _batterySettings.InSeries, _batterySettings.InParallel)
            : null;
        UpdateBatterySummary();
        _lastCables = _lastResult is { HasValidMpptRange: true } && _dataStore.CableTables is { } tables
            ? CableCalculator.Calculate(_lastResult, _cableSettings, tables, _lastBattery)
            : null;
        UpdateCableSummary();
        ResultOverview overview = ResultPresenter.Present(_lastResult, _lastCables, _cableSettings, _lastBattery);
        ResultsPanel.DataContext = overview;
        StatusBar.DataContext = overview;
        LimitsCard.Visibility = overview.Limits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_lastResult is { HasValidMpptRange: true } result)
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

    // Adjusts sliders and texts of every MPPT card to the calculation result.
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

                MpptUiRefs? refs = RefsOf(cfg);
                UpdateMpptUiState(cfg, refs);
                if (refs is null || mppt.Status == MpptStatus.Disabled)
                {
                    continue;
                }

                // Module slider: 1 .. (highest allowed count + 5), allowed range marked; without module 1 .. 30. The
                // range always contains the current value, so that a value outside the allowed range stays visible.
                bool calculated = mppt.Status == MpptStatus.Calculated;
                int moduleMax = Math.Max(calculated && mppt.MaxModulesPerString >= 1 ? mppt.MaxModulesPerString + 5 : 30, cfg.ModulesPerString);
                refs.NSlider.Minimum = 1;
                refs.NSlider.Maximum = Math.Max(1, moduleMax);
                if (calculated && mppt.HasValidModuleRange && mppt.MaxModulesPerString >= 1)
                {
                    refs.NSlider.IsSelectionRangeEnabled = true;
                    refs.NSlider.SelectionStart = mppt.MinModulesPerString;
                    refs.NSlider.SelectionEnd = mppt.MaxModulesPerString;
                }
                else
                {
                    refs.NSlider.IsSelectionRangeEnabled = false;
                }

                if (calculated)
                {
                    refs.NAllowed.Text = mppt.HasValidModuleRange
                        ? L.F($"Allowed: {mppt.MinModulesPerString} … {mppt.MaxModulesPerString} modules per string")
                        : L.T("No module count per string meets all voltage limits.");
                }

                // The value is not clamped to the allowed range.
                refs.NSlider.Value = cfg.ModulesPerString;
                refs.NValue.Text = cfg.ModulesPerString.ToString(CultureInfo.CurrentCulture);

                // String slider: 1 .. inverter specification.
                int specMax = _selectedInverter?.MaxStringsPerMppt > 0 ? _selectedInverter.MaxStringsPerMppt : 10;
                refs.SSlider.Minimum = 1;
                refs.SSlider.Maximum = Math.Max(specMax, cfg.ParallelStrings);
                refs.SSlider.Value = cfg.ParallelStrings;
                refs.SValue.Text = cfg.ParallelStrings.ToString(CultureInfo.CurrentCulture);
            }
        }
        finally
        {
            _suppressSliderEvents = false;
        }
    }

    private MpptUiRefs? RefsOf(StringConfiguration cfg)
    {
        int index = _stringConfigs.IndexOf(cfg);
        return index >= 0 && index < _mpptRefs.Count ? _mpptRefs[index] : null;
    }

    #endregion

    #region Report

    // Builds the Markdown report from the last calculation.
    private void BuildMarkdownReport()
    {
        _reportMarkdown = ReportBuilder.Build(_lastResult, _parameters, DateTime.Now, _lastCables, _dataStore.CableTables, _lastBattery,
            _projectPath is null ? null : _project.Info(_projectPath));
        ReportMarkdownTextBox.Text = _reportMarkdown;
    }

    // Shows the Markdown source or the HTML preview. The preview is only rendered while the report tab is shown.
    private void UpdateReportView()
    {
        if (_reportPreviewMode)
        {
            ReportMarkdownTextBox.Visibility = Visibility.Collapsed;
            ReportPreviewContainer.Visibility = Visibility.Visible;
            if (ReportTab.IsSelected)
            {
                _ = ShowPreviewAsync();
            }
        }
        else
        {
            ReportPreviewContainer.Visibility = Visibility.Collapsed;
            ReportMarkdownTextBox.Visibility = Visibility.Visible;
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, MainTabs) && ReportTab.IsSelected && !_exportingReport)
        {
            UpdateReportView();
        }
    }

    // Renders the current report into the WebView2 preview. The page and the KaTeX files are served from a local
    // host name that never reaches the network.
    private async Task ShowPreviewAsync()
    {
        try
        {
            string body = _markdown.ToHtml(_reportMarkdown);
            _previewHtml = HtmlDocument.Create(body, ReportTitle);
            if (!await EnsurePreviewAsync())
            {
                return;
            }

            _navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ReportPreviewBrowser.CoreWebView2.Navigate(PreviewPageUrl);
            _logger.LogInformation("Report preview updated. Length={Length}.", body.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating the report preview.");
            MessageDialog.Show(this, L.T("Error updating the report preview. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Creates the WebView2 environment once. Its data folder lives in the per-user data folder, because the
    // installation folder is read-only. Returns false and switches back to Markdown when WebView2 is missing.
    private async Task<bool> EnsurePreviewAsync()
    {
        if (ReportPreviewBrowser.CoreWebView2 is not null)
        {
            return true;
        }

        try
        {
            _previewInit ??= InitializePreviewAsync();
            await _previewInit;
            return true;
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _previewInit = null;
            WebViewSupport.ResetEnvironment();
            _logger.LogError(ex, "The report preview (WebView2) could not be started.");
            ReportViewSelector.SelectedIndex = 1;
            MessageDialog.Show(this, L.T("The report preview needs the Microsoft Edge WebView2 Runtime, which could not be started. The Markdown source is shown instead. See the log for details."),
                L.T("Report preview"), StatusKind.Warning);
            return false;
        }
    }

    private async Task InitializePreviewAsync()
    {
        CoreWebView2Environment environment = await WebViewSupport.GetEnvironmentAsync(_userData);
        await ReportPreviewBrowser.EnsureCoreWebView2Async(environment);

        CoreWebView2 core = ReportPreviewBrowser.CoreWebView2;
        WebViewSupport.ApplyColorScheme(core, _theme.IsDark);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.AddWebResourceRequestedFilter($"https://{PreviewHost}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnPreviewResourceRequested;
        core.NavigationStarting += (_, e) =>
        {
            // The preview only shows the report; links do not navigate away.
            if (!e.Uri.StartsWith($"https://{PreviewHost}/", StringComparison.Ordinal))
            {
                e.Cancel = true;
                return;
            }

            _navigationId = e.NavigationId;
        };

        // KaTeX renders on DOMContentLoaded, so the formulas are rendered when the navigation is completed.
        core.NavigationCompleted += (_, e) =>
        {
            // Only the latest navigation reports: an older one ends as replaced, not as the result of the current one.
            if (e.NavigationId == _navigationId && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            {
                _navigation?.TrySetResult(e.IsSuccess);
            }
        };
    }

    // Renders the current report in the preview and waits until it is loaded; false when WebView2 is not available.
    private async Task<bool> RenderPreviewForExportAsync()
    {
        if (!_reportPreviewMode)
        {
            ReportViewSelector.SelectedIndex = 0;
        }

        await ShowPreviewAsync();
        if (ReportPreviewBrowser.CoreWebView2 is null || _navigation is not { } navigation)
        {
            return false;
        }

        try
        {
            return await navigation.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "The report preview did not finish loading.");
            return false;
        }
    }

    // Saves the report as one HTML file: the rendered preview (formulas as HTML), KaTeX style and fonts embedded.
    private async void OnExportHtmlClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = L.T("Export report as HTML"),
            Filter = "HTML file (*.html)|*.html",
            FileName = "AnWa-Solar-Report.html",
            AddExtension = true,
            OverwritePrompt = true,
        };
        ApplyExportFolder(dlg);
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            string html;
            if (await RenderPreviewForExportAsync())
            {
                string json = await ReportPreviewBrowser.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
                html = System.Text.Json.JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            }
            else
            {
                // Without WebView2 the formulas stay as KaTeX source text.
                html = HtmlDocument.Create(_markdown.ToHtml(_reportMarkdown), ReportTitle);
            }

            File.WriteAllText(dlg.FileName, HtmlDocument.CreateStandalone(html, s_katexDirectory), new UTF8Encoding(false));
            _logger.LogInformation("Report exported as HTML: {Path}.", dlg.FileName);
            Snackbar.MessageQueue?.Enqueue(L.F($"Report exported: {Path.GetFileName(dlg.FileName)}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or System.Text.Json.JsonException or InvalidOperationException)
        {
            _logger.LogError(ex, "Error exporting the report as HTML.");
            MessageDialog.Show(this, L.T("The report could not be exported. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Prints the preview to a PDF file (A4, light colours, title, date and page numbers).
    private async void OnExportPdfClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = L.T("Export report as PDF"),
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = "AnWa-Solar-Report.pdf",
            AddExtension = true,
            OverwritePrompt = true,
        };
        ApplyExportFolder(dlg);
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            switch (await PrintReportPdfAsync(dlg.FileName))
            {
                case null:
                    MessageDialog.Show(this, L.T("The PDF export needs the report preview (Microsoft Edge WebView2 Runtime), which is not available."), L.T("Export PDF"), StatusKind.Warning);
                    return;
                case false:
                    MessageDialog.Show(this, L.T("The PDF file could not be written."), L.T("Export PDF"), StatusKind.Error);
                    return;
                default:
                    break;
            }

            _logger.LogInformation("Report exported as PDF: {Path}.", dlg.FileName);
            Snackbar.MessageQueue?.Enqueue(L.F($"Report exported: {Path.GetFileName(dlg.FileName)}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            _logger.LogError(ex, "Error exporting the report as PDF.");
            MessageDialog.Show(this, L.T("The report could not be exported. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Prints the report as an A4 PDF; null without report preview (WebView2), false when the file was not written.
    private async Task<bool?> PrintReportPdfAsync(string path)
    {
        // The preview lives in the report tab and is only created while the tab is shown (project export from the
        // string calculation tab); the previous tab comes back afterwards.
        // The tab change must not start a second preview navigation: it would cancel the one of the export.
        object previousTab = MainTabs.SelectedItem;
        _exportingReport = true;
        try
        {
            if (!ReportTab.IsSelected)
            {
                ReportTab.IsSelected = true;
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            }

            return await PrintLoadedReportPdfAsync(path);
        }
        finally
        {
            MainTabs.SelectedItem = previousTab;
            _exportingReport = false;
        }
    }

    private async Task<bool?> PrintLoadedReportPdfAsync(string path)
    {
        if (!await RenderPreviewForExportAsync())
        {
            return null;
        }

        {
            CoreWebView2 core = ReportPreviewBrowser.CoreWebView2;
            CoreWebView2PrintSettings settings = core.Environment.CreatePrintSettings();
            settings.Orientation = CoreWebView2PrintOrientation.Portrait;
            settings.PageWidth = 8.27; // A4 in inches
            settings.PageHeight = 11.69;
            settings.MarginTop = settings.MarginBottom = 0.6;
            settings.MarginLeft = settings.MarginRight = 0.6;
            settings.ShouldPrintBackgrounds = true;
            settings.ShouldPrintHeaderAndFooter = true;
            settings.HeaderTitle = _projectPath is null ? ReportTitle : $"{ReportTitle} – {_project.Name}";
            settings.FooterUri = string.Empty;

            bool saved;
            WebViewSupport.ApplyColorScheme(core, dark: false);
            try
            {
                saved = await core.PrintToPdfAsync(path, settings);
            }
            finally
            {
                WebViewSupport.ApplyColorScheme(core, _theme.IsDark);
            }

            return saved;
        }
    }

    #region Project

    private void OnEditProjectClick(object sender, RoutedEventArgs e)
    {
        var win = new ProjectInfoWindow(_project.Name, _project.Customer, _project.Address, _project.Notes, isNew: false) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _project.Name = win.ProjectName;
            _project.Customer = win.Customer;
            _project.Address = win.Address;
            _project.Notes = win.Notes;
            UpdateProjectCard();
            PersistLastSelection();
            Recalculate();
        }
    }

    // Opens another project or creates a new one; the current project is saved first.
    private void OnSwitchProjectClick(object sender, RoutedEventArgs e)
    {
        FlushPendingPersist();
        WriteProject();
        if (StartProjectWindow.Choose(this, _projects, _settings, _logger) is { } chosen)
        {
            OpenProject(chosen.Path, chosen.Project);
        }
    }

    // Exports the project as a ZIP file: the report as PDF, the datasheets and the project file in one folder.
    private async void OnExportProjectClick(object sender, RoutedEventArgs e)
    {
        string folder = ProjectService.FileNameOf(_project.Name);
        var dlg = new SaveFileDialog
        {
            Title = L.T("Export project"),
            Filter = "ZIP file (*.zip)|*.zip",
            FileName = folder + ".zip",
            AddExtension = true,
            OverwritePrompt = true,
        };
        ApplyExportFolder(dlg);
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        string tempPdf = Path.Combine(Path.GetTempPath(), $"AnWa-Solar-{Guid.NewGuid():N}.pdf");
        try
        {
            FlushPendingPersist();
            WriteProject();
            bool? printed = await PrintReportPdfAsync(tempPdf);
            var datasheets = new List<ExportDatasheet>();
            void Add(IMasterDataEntry? entry)
            {
                if (entry is not null)
                {
                    datasheets.Add(new ExportDatasheet($"{entry.Manufacturer} {entry.Model}", _dataStore.GetDatasheetPath(entry)));
                }
            }

            Add(_selectedInverter);
            foreach (StringConfiguration cfg in _stringConfigs.Where(c => c.IsEnabled))
            {
                Add(cfg.SelectedModule);
            }

            if (_batterySettings.Enabled)
            {
                Add(FindBattery(_batterySettings.Battery));
            }

            IReadOnlyList<string> files = ProjectExporter.WriteZip(dlg.FileName, _project, printed == true ? tempPdf : null, _reportMarkdown, datasheets);
            _logger.LogInformation("Project exported to {Path}: {Files}.", dlg.FileName, string.Join(", ", files));
            Snackbar.MessageQueue?.Enqueue(printed == true
                ? L.F($"Project exported: {Path.GetFileName(dlg.FileName)}")
                : L.F($"Project exported without PDF report (no report preview): {Path.GetFileName(dlg.FileName)}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            _logger.LogError(ex, "Error exporting the project.");
            MessageDialog.Show(this, L.T("The project could not be exported. See the log for details."), L.T("Error"), StatusKind.Error);
        }
        finally
        {
            if (File.Exists(tempPdf))
            {
                File.Delete(tempPdf);
            }
        }
    }

    #endregion

    private void OnPreviewResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        CoreWebView2Environment environment = ReportPreviewBrowser.CoreWebView2.Environment;
        string path = new Uri(e.Request.Uri).AbsolutePath;
        if (path == "/report.html")
        {
            var page = new MemoryStream(Encoding.UTF8.GetBytes(_previewHtml));
            e.Response = environment.CreateWebResourceResponse(page, 200, "OK", "Content-Type: text/html; charset=utf-8");
        }
        else if (HtmlDocument.TryResolveAsset(s_katexDirectory, path, out string file, out string contentType))
        {
            e.Response = environment.CreateWebResourceResponse(File.OpenRead(file), 200, "OK", ($"Content-Type: {contentType}"));
        }
        else
        {
            e.Response = environment.CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
        }
    }

    // Switches between preview (index 0) and Markdown source (index 1). Also raised while the window is created.
    private void OnReportViewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_logger is null || ReportMarkdownTextBox is null)
        {
            return;
        }

        bool preview = ReportViewSelector.SelectedIndex != 1;
        if (preview == _reportPreviewMode)
        {
            return;
        }

        _reportPreviewMode = preview;
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
            Snackbar.MessageQueue?.Enqueue(L.T("Markdown report copied to the clipboard."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error copying the Markdown report.");
            MessageDialog.Show(this, L.T("Error copying the report. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    // Saves the Markdown report
    private void OnSaveReportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = L.T("Save Markdown report"),
                Filter = "Markdown file (*.md)|*.md|Text file (*.txt)|*.txt",
                FileName = "AnWa-Solar-Report.md",
                AddExtension = true,
                OverwritePrompt = true
            };
            ApplyExportFolder(dlg);
            if (dlg.ShowDialog(this) == true)
            {
                File.WriteAllText(dlg.FileName, _reportMarkdown ?? string.Empty, new UTF8Encoding(false));
                _logger.LogInformation("Markdown report saved: {Path}.", dlg.FileName);
                Snackbar.MessageQueue?.Enqueue(L.F($"Report saved: {Path.GetFileName(dlg.FileName)}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving the Markdown report.");
            MessageDialog.Show(this, L.T("Error saving the report. See the log for details."), L.T("Error"), StatusKind.Error);
        }
    }

    #endregion
}
