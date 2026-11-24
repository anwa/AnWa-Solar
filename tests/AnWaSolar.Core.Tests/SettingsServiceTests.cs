using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private const string LegacyAppSettings = """
        {
          "AnWaSolar": {
            "LastSelection": {
              "Inverter": { "Hersteller": "Deye", "Model": "SUN-18K-SG05LP3-EU-SM2" },
              "Strings": [
                { "MpptIndex": 1, "ModuleProString": 11, "ParalleleStrings": 1, "IsEnabled": true,
                  "Module": { "Hersteller": "AIKO", "Model": "AIKO-A500-MAH60Db" } },
                { "MpptIndex": 2, "ModuleProString": 10, "ParalleleStrings": 1, "IsEnabled": false }
              ]
            },
            "Parameters": { "MinTempC": -15, "MaxTempC": 65, "SicherheitsmargePct": 5 }
          }
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    private string UserDirectory => Path.Combine(_root, "user");

    private string LegacyPath => Path.Combine(_root, "appsettings.json");

    private string SettingsPath => Path.Combine(UserDirectory, SettingsService.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private SettingsService Create()
    {
        Directory.CreateDirectory(_root);
        return new SettingsService(NullLogger<SettingsService>.Instance, new TestLocation(UserDirectory), LegacyPath);
    }

    [Fact]
    public void Without_files_defaults_are_returned_and_nothing_is_written()
    {
        SettingsService service = Create();

        CalculationParameters parameters = service.LoadParameters();
        LastSelection selection = service.LoadLastSelection();

        Assert.Equal(-20, parameters.MinTempC);
        Assert.Equal(70, parameters.MaxTempC);
        Assert.Null(selection.Inverter);
        Assert.Empty(selection.Strings);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Saved_values_are_read_by_a_new_instance()
    {
        SettingsService first = Create();
        Assert.True(first.SaveParameters(new CalculationParameters { MinTempC = -25, MaxTempC = 75, SicherheitsmargePct = 3 }));
        Assert.True(first.SaveLastSelection(new LastSelection
        {
            Inverter = new SelectedRef { Hersteller = "Deye", Model = "SUN-18K-SG05LP3-EU-SM2" },
            Strings = [new MpptStringSelection { MpptIndex = 1, ModuleProString = 12, ParalleleStrings = 2, IsEnabled = false }],
        }));

        SettingsService second = Create();
        CalculationParameters parameters = second.LoadParameters();
        LastSelection selection = second.LoadLastSelection();

        Assert.Equal(-25, parameters.MinTempC);
        Assert.Equal(75, parameters.MaxTempC);
        Assert.Equal(3, parameters.SicherheitsmargePct);
        Assert.Equal("SUN-18K-SG05LP3-EU-SM2", selection.Inverter?.Model);
        MpptStringSelection mppt = Assert.Single(selection.Strings);
        Assert.Equal(12, mppt.ModuleProString);
        Assert.Equal(2, mppt.ParalleleStrings);
        Assert.False(mppt.IsEnabled);
    }

    [Fact]
    public void Writing_leaves_no_temporary_file()
    {
        SettingsService service = Create();

        Assert.True(service.SaveParameters(new CalculationParameters()));

        Assert.True(File.Exists(SettingsPath));
        Assert.Empty(Directory.GetFiles(UserDirectory, "*.tmp"));
    }

    [Fact]
    public void Returned_objects_are_copies()
    {
        SettingsService service = Create();
        CalculationParameters parameters = service.LoadParameters();

        parameters.MaxTempC = 99;

        Assert.Equal(70, service.LoadParameters().MaxTempC);
    }

    [Fact]
    public void Legacy_appsettings_are_imported_once()
    {
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(_root).FullName, "appsettings.json"), LegacyAppSettings);

        LastSelection selection = Create().LoadLastSelection();

        Assert.Equal("Deye", selection.Inverter?.Hersteller);
        Assert.Equal(2, selection.Strings.Count);
        Assert.Equal("AIKO-A500-MAH60Db", selection.Strings[0].Module?.Model);
        Assert.False(selection.Strings[1].IsEnabled);
        Assert.True(File.Exists(SettingsPath));

        // The new file now takes precedence over a changed legacy file.
        File.WriteAllText(LegacyPath, LegacyAppSettings.Replace("\"MaxTempC\": 65", "\"MaxTempC\": 40", StringComparison.Ordinal));
        Assert.Equal(65, Create().LoadParameters().MaxTempC);
    }

    [Fact]
    public void Legacy_file_without_user_values_is_not_imported()
    {
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(_root).FullName, "appsettings.json"),
            """{ "AnWaSolar": { "Markdown": { "UseAdvancedExtensions": false } } }""");

        Assert.Null(Create().LoadLastSelection().Inverter);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Unreadable_settings_file_is_backed_up_and_defaults_are_used()
    {
        Directory.CreateDirectory(UserDirectory);
        File.WriteAllText(SettingsPath, "{ not json");

        CalculationParameters parameters = Create().LoadParameters();

        Assert.Equal(70, parameters.MaxTempC);
        string backup = Assert.Single(Directory.GetFiles(UserDirectory, SettingsService.FileName + ".invalid-*"));
        Assert.Equal("{ not json", File.ReadAllText(backup));
    }

    [Fact]
    public void Invalid_parameters_fall_back_to_defaults()
    {
        Directory.CreateDirectory(UserDirectory);
        File.WriteAllText(SettingsPath, """{ "SchemaVersion": 1, "Parameters": { "MinTempC": 50, "MaxTempC": 40, "SicherheitsmargePct": 5 } }""");

        CalculationParameters parameters = Create().LoadParameters();

        Assert.Equal(-20, parameters.MinTempC);
        Assert.Equal(70, parameters.MaxTempC);
    }

    private sealed class TestLocation(string directoryPath) : IUserDataLocation
    {
        public string DirectoryPath { get; } = directoryPath;
    }
}
