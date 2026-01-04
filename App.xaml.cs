using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AnWaSolar.Services;

namespace AnWaSolar;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();

        // Konfiguration laden (appsettings.json + Umgebungsvarianten)
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Globale JsonSerializerOptions (System.Text.Json)
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
        builder.Logging.AddConsole(); // hilfreich in Debug/VS Output

        builder.Services.AddSingleton(jsonOptions);
        builder.Services.AddSingleton<ILogFilePathProvider, LogFilePathProvider>();
        builder.Services.AddSingleton<ILoggerProvider, JsonFileLoggerProvider>(); // eigener JSON-File-Logger

        // Services
        builder.Services.AddSingleton<IMarkdownService, MarkdownService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        // Data-Services registrieren
        builder.Services.AddSingleton<IDataPathProvider, DataPathProvider>();
        builder.Services.AddSingleton<IDataStore, JsonDataStore>();

        // Windows über DI
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();

        // Daten beim Start laden
        var dataStore = _host.Services.GetRequiredService<IDataStore>();
        dataStore.Load();

        // Hauptfenster manuell erzeugen, damit DI greift
        var main = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("AnWa-Solar gestartet. Daten geladen. Version {Version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
