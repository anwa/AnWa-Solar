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

    public JsonDataStoreTests()
    {
        Directory.CreateDirectory(_directory);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", Calculation.CableTables.FileName), Path.Combine(_directory, Calculation.CableTables.FileName));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", JsonDataStore.BatteryFileName), Path.Combine(_directory, JsonDataStore.BatteryFileName));
    }

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

    private string UserDirectory => Path.Combine(_directory, "user");

    [Fact]
    public void Own_entry_replaces_the_shipped_entry_and_new_ones_are_added()
    {
        Directory.CreateDirectory(UserDirectory);
        string ownInverter = ValidInverter.Replace("\"Start voltage (V)\": 160", "\"Start voltage (V)\": 180", StringComparison.Ordinal);
        string otherInverter = ValidInverter.Replace("SUN-18K", "SUN-20K", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(UserDirectory, JsonDataStore.InverterFileName), Inverters(ownInverter, otherInverter));

        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Assert.Equal(2, store.Inverters.Count);
        Assert.Equal(180, store.Inverters[0].StartVoltageV);
        Assert.All(store.Inverters, i => Assert.True(i.IsUserEntry));
        Assert.False(store.Modules[0].IsUserEntry);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Invalid_own_file_is_reported_with_its_own_name()
    {
        Directory.CreateDirectory(UserDirectory);
        File.WriteAllText(Path.Combine(UserDirectory, JsonDataStore.ModuleFileName), "{ broken");

        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Assert.Single(store.Modules);
        Assert.Equal("Modules.json (own data)", Assert.Single(store.Issues).FileName);
    }

    [Fact]
    public void Saving_writes_the_own_file_and_copies_the_datasheet()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        string shipped = File.ReadAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName));
        string pdf = Path.Combine(_directory, "trina.pdf");
        File.WriteAllText(pdf, "%PDF-1.7 test");
        Dictionary<string, string?> values = DatasheetEntryBuilderTests.Trina445();
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildModule(values, out Models.PVModule? module));

        Models.PVModule saved = store.SaveModule(module!, pdf);

        Assert.True(saved.IsUserEntry);
        Assert.Equal("Datasheets/Trina TSM-445-NEG9R.28.pdf", saved.DatasheetFile);
        Assert.Equal(2, store.Modules.Count);
        Assert.Equal(File.ReadAllText(pdf), File.ReadAllText(store.GetDatasheetPath(saved)!));
        Assert.Equal(shipped, File.ReadAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName)));
        string own = File.ReadAllText(Path.Combine(UserDirectory, JsonDataStore.ModuleFileName));
        Assert.Contains("\"Temperature coefficient of Voc (%/°C)\": -0.24", own, StringComparison.Ordinal);
        Assert.DoesNotContain("Vmpp (%/°C)", own, StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_again_replaces_the_own_entry()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildInverter(DatasheetEntryBuilderTests.Deye18K(), out Models.Inverter? first));
        store.SaveInverter(first!, datasheetSourcePath: null);
        Dictionary<string, string?> changed = DatasheetEntryBuilderTests.Deye18K();
        changed[Datasheets.DatasheetFields.StartVoltageV] = "170";
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildInverter(changed, out Models.Inverter? second));

        Models.Inverter saved = store.SaveInverter(second!, datasheetSourcePath: null);

        Assert.Equal(170, saved.StartVoltageV);
        Assert.Equal(170, Assert.Single(store.Inverters).StartVoltageV);
        Assert.Null(store.GetDatasheetPath(saved));
    }

    [Fact]
    public void Unreadable_own_file_is_not_overwritten()
    {
        Directory.CreateDirectory(UserDirectory);
        string ownPath = Path.Combine(UserDirectory, JsonDataStore.InverterFileName);
        File.WriteAllText(ownPath, "{ broken");
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildInverter(DatasheetEntryBuilderTests.Deye18K(), out Models.Inverter? inverter));

        Assert.Throws<IOException>(() => store.SaveInverter(inverter!, datasheetSourcePath: null));
        Assert.Equal("{ broken", File.ReadAllText(ownPath));
    }

    [Fact]
    public void Datasheet_path_outside_the_user_folder_is_not_opened()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        var entry = new Models.Inverter { IsUserEntry = true, DatasheetFile = "../" + JsonDataStore.ModuleFileName };

        Assert.Null(store.GetDatasheetPath(entry));
    }

    [Fact]
    public void Datasheet_file_name_replaces_invalid_characters()
    {
        var module = new Models.PVModule { Manufacturer = "Ja-Solar", Model = "JAM54D40-460/LB" };

        Assert.Equal("Ja-Solar JAM54D40-460_LB.pdf", JsonDataStore.DatasheetFileName(module));
    }

    private sealed class TestPaths(string directory) : IDataPathProvider
    {
        public string GetFilePath(string fileName) => Path.Combine(directory, fileName);

        public string UserDirectory => Path.Combine(directory, "user");
    }
}
