using System.IO;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Services;

using Microsoft.Win32;

namespace AnWaSolar.Windows;

/// <summary>Language, theme, folders and the defaults of new projects.</summary>
public partial class SettingsWindow : ChromeWindow
{
    private readonly CableTables? _tables;
    private readonly string _defaultProjectsFolder;

    public SettingsWindow(AppPreferences current, AppTheme theme, CableTables? tables, string defaultProjectsFolder)
    {
        _tables = tables;
        _defaultProjectsFolder = defaultProjectsFolder;
        InitializeComponent();
        Preferences = current.Clone();
        Theme = theme;

        LanguageCombo.ItemsSource = new[]
        {
            new KeyValuePair<string?, string>(null, L.T("Windows language")),
            new KeyValuePair<string?, string>(L.German, "Deutsch"),
            new KeyValuePair<string?, string>(L.English, "English"),
        };
        LanguageCombo.DisplayMemberPath = "Value";
        LanguageCombo.SelectedValuePath = "Key";
        LanguageCombo.SelectedValue = Preferences.Language;
        if (LanguageCombo.SelectedIndex < 0)
        {
            LanguageCombo.SelectedIndex = 0;
        }

        ThemeCombo.ItemsSource = new[]
        {
            new KeyValuePair<AppTheme, string>(AppTheme.System, L.T("Windows theme")),
            new KeyValuePair<AppTheme, string>(AppTheme.Light, L.T("Light")),
            new KeyValuePair<AppTheme, string>(AppTheme.Dark, L.T("Dark")),
        };
        ThemeCombo.DisplayMemberPath = "Value";
        ThemeCombo.SelectedValuePath = "Key";
        ThemeCombo.SelectedValue = theme;

        ProjectsFolderInput.Text = Preferences.ProjectsFolder ?? defaultProjectsFolder;
        ExportFolderInput.Text = Preferences.ExportFolder ?? string.Empty;
        UpdateDefaultsText();
    }

    public AppPreferences Preferences { get; }

    public AppTheme Theme { get; private set; }

    /// <summary>True when a change needs a restart (language or projects folder).</summary>
    public bool RestartNeeded { get; private set; }

    private void UpdateDefaultsText()
    {
        CalculationParameters p = Preferences.DefaultParameters;
        DefaultsText.Text = L.F($"Cell temperature {p.MinTempC} … {p.MaxTempC} °C, safety margin {p.SafetyMarginPct} %, DC ambient {Preferences.DefaultCables.DcAmbientC} °C, module leads {Preferences.DefaultCables.ModuleLeadLengthM} m");
    }

    private string? ChooseFolder(string current)
    {
        var dialog = new OpenFolderDialog { Title = L.T("Choose a folder"), InitialDirectory = Directory.Exists(current) ? current : string.Empty };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void OnBrowseProjectsClick(object sender, RoutedEventArgs e)
    {
        if (ChooseFolder(ProjectsFolderInput.Text) is { } folder)
        {
            ProjectsFolderInput.Text = folder;
        }
    }

    private void OnDefaultProjectsClick(object sender, RoutedEventArgs e) => ProjectsFolderInput.Text = _defaultProjectsFolder;

    private void OnBrowseExportClick(object sender, RoutedEventArgs e)
    {
        if (ChooseFolder(ExportFolderInput.Text) is { } folder)
        {
            ExportFolderInput.Text = folder;
        }
    }

    private void OnDefaultExportClick(object sender, RoutedEventArgs e) => ExportFolderInput.Text = string.Empty;

    private void OnDefaultParametersClick(object sender, RoutedEventArgs e)
    {
        var window = new ParametersWindow(Preferences.DefaultParameters) { Owner = this };
        if (window.ShowDialog() == true)
        {
            Preferences.DefaultParameters = window.Parameters;
            UpdateDefaultsText();
        }
    }

    private void OnDefaultCablesClick(object sender, RoutedEventArgs e)
    {
        if (_tables is null)
        {
            MessageDialog.Show(this, L.T("The cable tables (Data/CableTables.json) could not be loaded; cables and fuses cannot be sized."), Title, StatusKind.Warning);
            return;
        }

        var window = new CableSettingsWindow(Preferences.DefaultCables, _tables, mpptCount: 2) { Owner = this };
        if (window.ShowDialog() == true)
        {
            Preferences.DefaultCables = window.Settings;
            UpdateDefaultsText();
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string projects = ProjectsFolderInput.Text.Trim();
        string export = ExportFolderInput.Text.Trim();
        foreach (string folder in new[] { projects, export }.Where(f => f.Length > 0))
        {
            if (!Path.IsPathFullyQualified(folder))
            {
                MessageDialog.Show(this, L.F($"The folder \"{folder}\" is not a full path, for example D:\\Solar\\Projects."), Title, StatusKind.Error);
                return;
            }
        }

        if (projects.Length == 0)
        {
            projects = _defaultProjectsFolder;
        }

        try
        {
            Directory.CreateDirectory(projects);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageDialog.Show(this, L.F($"The projects folder cannot be created: {ex.Message}"), Title, StatusKind.Error);
            return;
        }

        string? language = LanguageCombo.SelectedValue as string;
        string? projectsFolder = string.Equals(Path.GetFullPath(projects).TrimEnd('\\'), Path.GetFullPath(_defaultProjectsFolder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? null : projects;
        RestartNeeded = language != Preferences.Language || !string.Equals(projectsFolder, Preferences.ProjectsFolder, StringComparison.OrdinalIgnoreCase);
        Preferences.Language = language;
        Preferences.ProjectsFolder = projectsFolder;
        Preferences.ExportFolder = export.Length == 0 ? null : export;
        Theme = ThemeCombo.SelectedValue is AppTheme theme ? theme : AppTheme.System;
        DialogResult = true;
    }
}
