using System.Text.Json;

using AnWaSolar.Core.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Tests;

public sealed class JsonFileLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Concurrent_writes_keep_every_line()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AnWaSolar:LogDirectory"] = _directory })
            .Build();
        var pathProvider = new LogFilePathProvider(configuration);
        using var provider = new JsonFileLoggerProvider(pathProvider, new JsonSerializerOptions());

        const int Threads = 8;
        const int LinesPerThread = 200;
        Parallel.For(0, Threads, t =>
        {
            ILogger logger = provider.CreateLogger($"Test{t}");
            for (int i = 0; i < LinesPerThread; i++)
            {
                logger.LogInformation("Line {Index}", i);
            }
        });

        string[] lines = File.ReadAllLines(pathProvider.GetCurrentLogFilePath());
        Assert.Equal(Threads * LinesPerThread, lines.Length);
        foreach (string line in lines)
        {
            using JsonDocument document = JsonDocument.Parse(line);
            Assert.Equal("Information", document.RootElement.GetProperty("level").GetString());
        }
    }
}
