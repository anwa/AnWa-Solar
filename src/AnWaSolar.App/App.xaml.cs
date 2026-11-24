using System;
using System.IO;
using System.Text.Json;
using System.Windows;

using AnWaSolar.Core;
using AnWaSolar.Core.Logging;
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

        // Master data
        builder.Services.AddSingleton<IDataPathProvider, DataPathProvider>();
        builder.Services.AddSingleton<IDataStore, JsonDataStore>();

        // Theme
        builder.Services.AddSingleton<ThemeService>();

        // Windows through dependency injection
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();

        // Apply the saved theme before the first window is shown
        _host.Services.GetRequiredService<ThemeService>().Apply(_host.Services.GetRequiredService<ISettingsService>().LoadTheme());

        // Load the master data at startup
        var dataStore = _host.Services.GetRequiredService<IDataStore>();
        dataStore.Load();

        // Create the main window through dependency injection
        var main = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("AnWa-Solar started. Master data loaded. Version {Version}", AppVersion.Current);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
