using System.Text.Json;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

public sealed class JsonDataStoreTests : IDisposable
{
    private const string ValidInverter = """
        {
          "Manufacturer": "Deye",
          "Model": "SUN-18K-SG05LP3-EU-SM2",
          "Max. DC input power (W)": 28800,
          "Max. DC input voltage (V)": 800,
          "Start voltage (V)": 160,
          "MPPT voltage range (V)": "160-650",
          "Nominal DC input voltage (V)": 550,
          "Max. input current per MPPT (A)": 36,
          "Max. short-circuit current per MPPT (A)": 54,
          "MPPT count": 2,
          "Max. strings per MPPT": 2
        }
        """;

    private const string ValidModule = """
        {
          "Manufacturer": "AIKO",
          "Model": "AIKO-A500-MAH60Db",
          "Pmax (W)": 500,
          "Vmpp (V)": 37.9,
          "Impp (A)": 13.21,
          "Voc (V)": 45.02,
          "Isc (A)": 14.05,
          "Module efficiency (%)": 22.6,
          "Temperature coefficient of Pmax (%/°C)": -0.26,
          "Temperature coefficient of Voc (%/°C)": -0.22,
          "Temperature coefficient of Isc (%/°C)": 0.05,
          "Operating temperature min (°C)": -40,
          "Operating temperature max (°C)": 85
        }
        """;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    public JsonDataStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private JsonDataStore Load(string? inverters, string? modules)
    {
        if (inverters is not null)
        {
            File.WriteAllText(Path.Combine(_directory, JsonDataStore.InverterFileName), inverters);
        }

        if (modules is not null)
        {
            File.WriteAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName), modules);
        }

        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new TestPaths(_directory));
        store.Load();
        return store;
    }

    private static string Inverters(params string[] entries) => $$"""{ "Inverters": [ {{string.Join(",", entries)}} ] }""";

    private static string Modules(params string[] entries) => $$"""{ "Modules": [ {{string.Join(",", entries)}} ] }""";

    [Fact]
    public void Valid_files_load_without_issues()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Assert.Single(store.Inverters);
        Assert.Single(store.Modules);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Decimal_currents_and_voltages_are_accepted()
    {
        string inverter = ValidInverter
            .Replace("\"Max. input current per MPPT (A)\": 36", "\"Max. input current per MPPT (A)\": 12.5", StringComparison.Ordinal)
            .Replace("\"Start voltage (V)\": 160", "\"Start voltage (V)\": 162.5", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(inverter), Modules(ValidModule));

        Assert.Equal(12.5, Assert.Single(store.Inverters).MaxInputCurrentA);
        Assert.Equal(162.5, store.Inverters[0].StartVoltageV);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Invalid_entry_is_skipped_and_the_others_are_kept()
    {
        string textValue = ValidInverter
            .Replace("SUN-18K-SG05LP3-EU-SM2", "SUN-20K-SG05LP3-EU-SM2", StringComparison.Ordinal)
            .Replace("\"Max. input current per MPPT (A)\": 36", "\"Max. input current per MPPT (A)\": \"36 A\"", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter, textValue), Modules(ValidModule));

        Assert.Equal("SUN-18K-SG05LP3-EU-SM2", Assert.Single(store.Inverters).Model);
        DataLoadIssue issue = Assert.Single(store.Issues);
        Assert.Equal(JsonDataStore.InverterFileName, issue.FileName);
        Assert.Equal("Entry 2 (Deye SUN-20K-SG05LP3-EU-SM2)", issue.Entry);
    }

    [Fact]
    public void Entry_with_invalid_mppt_range_is_skipped()
    {
        string invalid = ValidInverter
            .Replace("SUN-18K", "SUN-12K", StringComparison.Ordinal)
            .Replace("\"160-650\"", "\"160 bis 650\"", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter, invalid), Modules(ValidModule));

        Assert.Single(store.Inverters);
        Assert.Contains("MPPT voltage range", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_with_inconsistent_values_is_skipped()
    {
        string invalid = ValidModule
            .Replace("AIKO-A500-MAH60Db", "AIKO-A505", StringComparison.Ordinal)
            .Replace("\"Voc (V)\": 45.02", "\"Voc (V)\": 30", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule, invalid));

        Assert.Single(store.Modules);
        Assert.Contains("Voc", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_entry_is_skipped()
    {
        JsonDataStore store = Load(Inverters(ValidInverter, ValidInverter.Replace("Deye", "DEYE", StringComparison.Ordinal)), Modules(ValidModule));

        Assert.Single(store.Inverters);
        Assert.Contains("duplicate", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unreadable_file_and_missing_file_are_reported()
    {
        JsonDataStore store = Load("{ \"Inverters\": [ ", modules: null);

        Assert.Empty(store.Inverters);
        Assert.Empty(store.Modules);
        Assert.Equal(2, store.Issues.Count);
        Assert.All(store.Issues, issue => Assert.Null(issue.Entry));
        Assert.Contains(store.Issues, issue => issue.FileName == JsonDataStore.ModuleFileName && issue.Message.Contains("not found", StringComparison.Ordinal));
    }

    [Fact]
    public void File_with_the_old_german_list_name_is_reported()
    {
        JsonDataStore store = Load("{ \"Wechselrichter\": [] }", Modules(ValidModule));

        Assert.Contains("\"Inverters\" is missing", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    private sealed class TestPaths(string directory) : IDataPathProvider
    {
        public string GetFilePath(string fileName) => Path.Combine(directory, fileName);
    }
}
