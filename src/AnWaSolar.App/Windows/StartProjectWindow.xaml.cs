using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Projects;
using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AnWaSolar.Windows;

/// <summary>A project chosen in the project dialog: its file and its content.</summary>
public sealed record ChosenProject(string Path, Project Project);

/// <summary>
/// Chooses the project: the last one (preselected, Enter opens it), one of the five most recent, another project file,
/// or a new project.
/// </summary>
public partial class StartProjectWindow : ChromeWindow
{
    private readonly IProjectService _projects;
    private readonly ILogger _logger;

    private StartProjectWindow(IProjectService projects, ISettingsService settings, ILogger logger)
    {
        _projects = projects;
        _logger = logger;
        InitializeComponent();

        // The recent projects that still exist; without any, the most recently changed projects of the folder.
        List<ProjectInfo> recent = [.. settings.LoadRecentProjects().Where(File.Exists).Select(TryInfo).OfType<ProjectInfo>()];
        if (recent.Count == 0)
        {
            recent = [.. projects.List().Take(SettingsService.MaxRecentProjects)];
        }

        RecentList.ItemsSource = recent.Select((p, i) => new Item(p, i == 0)).ToList();
        RecentList.SelectedIndex = recent.Count > 0 ? 0 : -1;
        EmptyText.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OpenButton.IsEnabled = recent.Count > 0;
        Loaded += (_, _) =>
        {
            if (RecentList.ItemContainerGenerator.ContainerFromIndex(0) is UIElement first)
            {
                first.Focus();
            }
        };
    }

    public ChosenProject? Result { get; private set; }

    /// <summary>Shows the dialog; null when it was cancelled.</summary>
    public static ChosenProject? Choose(Window? owner, IProjectService projects, ISettingsService settings, ILogger logger)
    {
        var window = new StartProjectWindow(projects, settings, logger);
        if (owner is not null)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.CancelButton.Content = "Exit";
        }

        return window.ShowDialog() == true ? window.Result : null;
    }

    private sealed record Item(ProjectInfo Info, bool Last)
    {
        public string Name => Info.Name;

        public string Details => string.Join(" · ", new[]
        {
            Last ? "Last project" : null,
            Info.Customer,
            "changed " + Info.ModifiedAt.ToString("g", CultureInfo.CurrentCulture),
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private ProjectInfo? TryInfo(string path)
    {
        try
        {
            return _projects.Load(path).Info(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Recent project {Path} skipped.", path);
            return null;
        }
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (RecentList.SelectedItem is Item item)
        {
            Open(item.Info.Path);
        }
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => OnOpenClick(sender, e);

    private void OnOtherClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_projects.DirectoryPath);
        var dialog = new OpenFileDialog
        {
            Title = "Open project",
            Filter = "AnWa-Solar project (*.json)|*.json",
            InitialDirectory = _projects.DirectoryPath,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true)
        {
            Open(dialog.FileName);
        }
    }

    private void OnNewClick(object sender, RoutedEventArgs e)
    {
        var info = new ProjectInfoWindow("New project", null, null, null, isNew: true) { Owner = this };
        if (info.ShowDialog() != true)
        {
            return;
        }

        try
        {
            DateTime now = DateTime.Now;
            var project = new Project
            {
                Name = info.ProjectName,
                Customer = info.Customer,
                Address = info.Address,
                Notes = info.Notes,
                CreatedAt = now,
                ModifiedAt = now,
            };
            Result = new ChosenProject(_projects.Create(project), project);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating a project.");
            MessageDialog.Show(this, $"The project could not be created.\n\n{ex.Message}", Title, StatusKind.Error);
        }
    }

    private void Open(string path)
    {
        try
        {
            Result = new ChosenProject(path, _projects.Load(path));
            DialogResult = true;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error opening the project {Path}.", path);
            MessageDialog.Show(this, ex.Message, Title, StatusKind.Error);
        }
    }
}
