using System.Text.Json;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

public sealed class JsonDataStoreTests : IDisposable
{
    private const string ValidInverter = """
        {
          "Hersteller": "Deye",
          "Model": "SUN-18K-SG05LP3-EU-SM2",
          "Max.DC-Eingangsleistung (W)": 28800,
          "Max.DC-Eingangsspannung (V)": 800,
          "Startspannung (V)": 160,
          "MPPT-Spannungsbereich (V)": "160-650",
          "Nenn-DC-Eingangsspannung (V)": 550,
          "Max. Betriebs-PV-Eingangsstrom (A)": 36,
          "Max. Eingangs-Kurzschlussstrom (A)": 54,
          "Anzahl der MPP Trackers": 2,
          "Max. Strings pro MPPT": 2
        }
        """;

    private const string ValidModule = """
        {
          "Hersteller": "AIKO",
          "Model": "AIKO-A500-MAH60Db",
          "Nominalleistung-PMAX (Wp)": 500,
          "Spannung im MPP-UMPP (V)": 37.9,
          "Strom im MPP-IMPP (A)": 13.21,
          "Leerlaufspannung-UOC (V)": 45.02,
          "Kurzschlusstrom-ISC (A)": 14.05,
          "Modulwirkungsgrad η m (%)": 22.6,
          "Temperaturkoeffzient von PMAX (%/°C)": -0.26,
          "Temperaturkoeffzient von VOC (%/°C)": -0.22,
          "Temperaturkoeffzient von ISC (%/°C)": 0.05,
          "Betriebstemperatur-Min (°C)": -40,
          "Betriebstemperatur-Max (°C)": 85
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

    private static string Inverters(params string[] entries) => $$"""{ "Wechselrichter": [ {{string.Join(",", entries)}} ] }""";

    private static string Modules(params string[] entries) => $$"""{ "Module": [ {{string.Join(",", entries)}} ] }""";

    [Fact]
    public void Valid_files_load_without_issues()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Assert.Single(store.Wechselrichter);
        Assert.Single(store.Module);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Decimal_currents_and_voltages_are_accepted()
    {
        string inverter = ValidInverter
            .Replace("\"Max. Betriebs-PV-Eingangsstrom (A)\": 36", "\"Max. Betriebs-PV-Eingangsstrom (A)\": 12.5", StringComparison.Ordinal)
            .Replace("\"Startspannung (V)\": 160", "\"Startspannung (V)\": 162.5", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(inverter), Modules(ValidModule));

        Assert.Equal(12.5, Assert.Single(store.Wechselrichter).MaxBetriebsPvEingangsstromA);
        Assert.Equal(162.5, store.Wechselrichter[0].StartspannungV);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Invalid_entry_is_skipped_and_the_others_are_kept()
    {
        string textValue = ValidInverter
            .Replace("SUN-18K-SG05LP3-EU-SM2", "SUN-20K-SG05LP3-EU-SM2", StringComparison.Ordinal)
            .Replace("\"Max. Betriebs-PV-Eingangsstrom (A)\": 36", "\"Max. Betriebs-PV-Eingangsstrom (A)\": \"36 A\"", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter, textValue), Modules(ValidModule));

        Assert.Equal("SUN-18K-SG05LP3-EU-SM2", Assert.Single(store.Wechselrichter).Model);
        DataLoadIssue issue = Assert.Single(store.Issues);
        Assert.Equal(JsonDataStore.InverterFileName, issue.FileName);
        Assert.Equal("Eintrag 2 (Deye SUN-20K-SG05LP3-EU-SM2)", issue.Entry);
    }

    [Fact]
    public void Entry_with_invalid_mppt_range_is_skipped()
    {
        string invalid = ValidInverter
            .Replace("SUN-18K", "SUN-12K", StringComparison.Ordinal)
            .Replace("\"160-650\"", "\"160 bis 650\"", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter, invalid), Modules(ValidModule));

        Assert.Single(store.Wechselrichter);
        Assert.Contains("MPPT-Spannungsbereich", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_with_inconsistent_values_is_skipped()
    {
        string invalid = ValidModule
            .Replace("AIKO-A500-MAH60Db", "AIKO-A505", StringComparison.Ordinal)
            .Replace("\"Leerlaufspannung-UOC (V)\": 45.02", "\"Leerlaufspannung-UOC (V)\": 30", StringComparison.Ordinal);

        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule, invalid));

        Assert.Single(store.Module);
        Assert.Contains("UOC", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_entry_is_skipped()
    {
        JsonDataStore store = Load(Inverters(ValidInverter, ValidInverter.Replace("Deye", "DEYE", StringComparison.Ordinal)), Modules(ValidModule));

        Assert.Single(store.Wechselrichter);
        Assert.Contains("doppelt", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unreadable_file_and_missing_file_are_reported()
    {
        JsonDataStore store = Load("{ \"Wechselrichter\": [ ", modules: null);

        Assert.Empty(store.Wechselrichter);
        Assert.Empty(store.Module);
        Assert.Equal(2, store.Issues.Count);
        Assert.All(store.Issues, issue => Assert.Null(issue.Entry));
        Assert.Contains(store.Issues, issue => issue.FileName == JsonDataStore.ModuleFileName && issue.Message.Contains("nicht gefunden", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_list_is_reported()
    {
        JsonDataStore store = Load("{ \"Inverters\": [] }", Modules(ValidModule));

        Assert.Contains("\"Wechselrichter\" fehlt", Assert.Single(store.Issues).Message, StringComparison.Ordinal);
    }

    private sealed class TestPaths(string directory) : IDataPathProvider
    {
        public string GetFilePath(string fileName) => Path.Combine(directory, fileName);
    }
}
