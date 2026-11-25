using System;
using System.IO;
using System.Text.Json;
using System.Windows;

using AnWaSoft.Design.Wpf.Theming;

using AnWaSolar.Core;
using AnWaSolar.Core.Logging;
using AnWaSolar.Core.Projects;
using AnWaSolar.Core.Services;
using AnWaSolar.Services;
using AnWaSolar.Windows;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnWaSolar;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        // Configuration: appsettings.json and environment-specific variants
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Shared JsonSerializerOptions (System.Text.Json)
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.AddDebug();
        builder.Logging.AddConsole();

        builder.Services.AddSingleton(jsonOptions);
        builder.Services.AddSingleton<ILogFilePathProvider, LogFilePathProvider>();
        builder.Services.AddSingleton<ILoggerProvider, JsonFileLoggerProvider>();

        // Services
        builder.Services.AddSingleton<IMarkdownService, MarkdownService>();
        builder.Services.AddSingleton<IUserDataLocation, UserDataLocation>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IProjectService, ProjectService>();

        // Master data
        builder.Services.AddSingleton<IDataPathProvider, DataPathProvider>();
        builder.Services.AddSingleton<IDataStore, JsonDataStore>();
        builder.Services.AddSingleton<DatasheetService>();

        // Theme
        builder.Services.AddSingleton<ThemeService>();

        // Windows through dependency injection
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();

        // Language and number format of the preferences (default: the Windows display language), before any window.
        AppPreferences preferences = _host.Services.GetRequiredService<ISettingsService>().LoadPreferences();
        L.SetLanguage(preferences.Language ?? (System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == L.German ? L.German : L.English));
        System.Globalization.CultureInfo culture = L.Culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        UiTranslator.Register();

        // Apply the saved theme before the first window is shown
        _host.Services.GetRequiredService<ThemeService>().Apply(ThemeMapping.ToPreference(_host.Services.GetRequiredService<ISettingsService>().LoadTheme()));

        // Load the master data at startup
        var dataStore = _host.Services.GetRequiredService<IDataStore>();
        dataStore.Load();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("AnWa-Solar started. Master data loaded. Version {Version}", AppVersion.Current);

        // The project dialog comes first; closing it ends the application.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var projects = _host.Services.GetRequiredService<IProjectService>();
        var settings = _host.Services.GetRequiredService<ISettingsService>();
        MigrateSettingsToProject(projects, settings, dataStore, logger);
        if (StartProjectWindow.Choose(null, projects, settings, logger) is not { } chosen)
        {
            logger.LogInformation("No project chosen; AnWa-Solar ends.");
            Shutdown();
            return;
        }

        // Create the main window through dependency injection
        var main = _host.Services.GetRequiredService<MainWindow>();
        main.OpenProject(chosen.Path, chosen.Project);
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }

    // Versions before projects kept the design in the settings; it becomes the first project.
    private static void MigrateSettingsToProject(IProjectService projects, ISettingsService settings, IDataStore store, ILogger logger)
    {
        try
        {
            if (projects.List().Count > 0 || ProjectMigration.FromSettings(settings, store, L.T("My project"), DateTime.Now) is not { } project)
            {
                return;
            }

            string path = projects.Create(project);
            settings.AddRecentProject(path);
            logger.LogInformation("The design of the settings was taken over as project {Path}.", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "The design of the settings could not be taken over as a project.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
