using System;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Logging;

public interface ILogFilePathProvider
{
    string GetCurrentLogFilePath();
}

public class LogFilePathProvider : ILogFilePathProvider
{
    private readonly string _logDir;

    public LogFilePathProvider(IConfiguration cfg)
    {
        var path = cfg["AnWaSolar:LogDirectory"] ?? "%LOCALAPPDATA%/AnWa-Solar/logs";
        _logDir = Environment.ExpandEnvironmentVariables(path);
        Directory.CreateDirectory(_logDir);
    }

    public string GetCurrentLogFilePath()
    {
        var date = DateTimeOffset.Now.ToString("yyyy-MM-dd");
        return Path.Combine(_logDir, $"log-{date}.jsonl");
    }
}

public class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly ILogFilePathProvider _pathProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFileLoggerProvider(ILogFilePathProvider pathProvider, JsonSerializerOptions jsonOptions)
    {
        _pathProvider = pathProvider;
        _jsonOptions = jsonOptions;
    }

    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(categoryName, _pathProvider, _jsonOptions);

    public void Dispose() { }
}

internal sealed class JsonFileLogger : ILogger
{
    private static readonly Lock s_writeLock = new();

    private readonly string _categoryName;
    private readonly ILogFilePathProvider _pathProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonFileLogger(string categoryName, ILogFilePathProvider pathProvider, JsonSerializerOptions jsonOptions)
    {
        _categoryName = categoryName;
        _pathProvider = pathProvider;
        _jsonOptions = jsonOptions;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // Scopes are not used.
        return default;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var record = new
        {
            ts = DateTimeOffset.Now,
            level = logLevel.ToString(),
            category = _categoryName,
            eventId = eventId.Id,
            message = formatter(state, exception),
            exception = exception?.ToString()
        };

        var line = JsonSerializer.Serialize(record, _jsonOptions);
        var path = _pathProvider.GetCurrentLogFilePath();

        // All loggers of the process share one lock, so concurrent writes never collide on the same file.
        lock (s_writeLock)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never break the application; a line that cannot be written is dropped.
            }
        }
    }
}
