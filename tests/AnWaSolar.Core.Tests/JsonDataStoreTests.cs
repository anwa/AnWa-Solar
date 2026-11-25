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
        CopyFolder(Path.Combine(AppContext.BaseDirectory, "Data", "Batteries"), Path.Combine(_directory, "Batteries"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static void CopyFolder(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
    }

    private static Models.ComponentSeries<T> Series<T>(string manufacturer, string series, params T[] variants)
        where T : class, Models.IMasterDataEntry => new() { Manufacturer = manufacturer, Series = series, Variants = [.. variants] };

    private static Models.Inverter Deye(string model, double startVoltage = 160)
    {
        Dictionary<string, string?> values = DatasheetEntryBuilderTests.Deye18K();
        values[Datasheets.DatasheetFields.Model] = model;
        values[Datasheets.DatasheetFields.StartVoltageV] = startVoltage.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildInverter(values, out Models.Inverter? inverter));
        return inverter!;
    }

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
    public void Saving_a_series_writes_one_file_with_the_datasheet_next_to_it()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        string shipped = File.ReadAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName));
        string pdf = Path.Combine(_directory, "trina.pdf");
        File.WriteAllText(pdf, "%PDF-1.7 test");
        Assert.Empty(Datasheets.DatasheetEntryBuilder.TryBuildModule(DatasheetEntryBuilderTests.Trina445(), out Models.PVModule? module));

        Models.PVModule saved = Assert.Single(store.SaveSeries(Series("Trina", "TSM-NEG9R.28", module!), pdf));

        string folder = Path.Combine(UserDirectory, "Modules", "Trina");
        Assert.True(saved.IsUserEntry);
        Assert.Equal("TSM-NEG9R.28", saved.Series);
        Assert.Equal("Modules/Trina/TSM-NEG9R.28.pdf", saved.DatasheetFile);
        Assert.Equal(2, store.Modules.Count);
        Assert.Equal(File.ReadAllText(pdf), File.ReadAllText(store.GetDatasheetPath(saved)!));
        Assert.Equal(shipped, File.ReadAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName)));
        string own = File.ReadAllText(Path.Combine(folder, "TSM-NEG9R.28.json"));
        Assert.Contains("\"Series\": \"TSM-NEG9R.28\"", own, StringComparison.Ordinal);
        Assert.Contains("\"Datasheet file\": \"TSM-NEG9R.28.pdf\"", own, StringComparison.Ordinal);
        Assert.Contains("\"Temperature coefficient of Voc (%/°C)\": -0.24", own, StringComparison.Ordinal);
        Assert.DoesNotContain("Vmpp (%/°C)", own, StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_again_replaces_the_series_and_keeps_its_datasheet()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        string pdf = Path.Combine(_directory, "deye.pdf");
        File.WriteAllText(pdf, "%PDF-1.7 test");
        store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2")), pdf);

        IReadOnlyList<Models.Inverter> saved = store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2",
            Deye("SUN-18K-SG05LP3-EU-SM2", 170), Deye("SUN-20K-SG05LP3-EU-SM2")), datasheetSourcePath: null);

        Assert.Equal(2, saved.Count);
        Assert.Equal(2, store.Inverters.Count); // the own SUN-18K replaces the shipped one
        Assert.Equal(170, store.Inverters.Single(i => i.Model == "SUN-18K-SG05LP3-EU-SM2").StartVoltageV);
        Assert.All(saved, i => Assert.NotNull(store.GetDatasheetPath(i)));
    }

    [Fact]
    public void Invalid_series_is_not_saved()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        Models.Inverter invalid = Deye("SUN-20K-SG05LP3-EU-SM2");
        invalid.MpptCount = 0;

        Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2"), invalid), null));
        Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2"), Deye("SUN-18K-SG05LP3-EU-SM2")), null));
        Assert.Throws<ArgumentException>(() => store.SaveSeries(Series("Deye", " ", Deye("SUN-18K-SG05LP3-EU-SM2")), null));
        Assert.False(Directory.Exists(Path.Combine(UserDirectory, "Inverters")));
    }

    [Fact]
    public void Unreadable_own_series_file_is_not_overwritten()
    {
        string ownPath = Path.Combine(UserDirectory, "Inverters", "Deye", "SUN-SG05LP3-EU-SM2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ownPath)!);
        File.WriteAllText(ownPath, "{ broken");
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Assert.Contains(store.Issues, i => i.FileName == "Inverters/Deye/SUN-SG05LP3-EU-SM2.json (own data)");
        Assert.Throws<IOException>(() => store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2")), null));
        Assert.Equal("{ broken", File.ReadAllText(ownPath));
    }

    [Fact]
    public void Own_single_list_file_is_converted_into_series_files()
    {
        Directory.CreateDirectory(Path.Combine(UserDirectory, JsonDataStore.DatasheetFolder));
        File.WriteAllText(Path.Combine(UserDirectory, JsonDataStore.DatasheetFolder, "Deye SUN-14K.pdf"), "%PDF-1.7 deye");
        string own = ValidInverter.Replace("SUN-18K", "SUN-14K", StringComparison.Ordinal)
            .Replace("\"Max. strings per MPPT\": 2", "\"Max. strings per MPPT\": 2, \"Datasheet file\": \"Datasheets/Deye SUN-14K.pdf\"", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(UserDirectory, JsonDataStore.InverterFileName), Inverters(own));

        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));

        Models.Inverter converted = store.Inverters.Single(i => i.IsUserEntry);
        Assert.Equal("SUN-14K-SG05LP3-EU-SM2", converted.Series);
        Assert.Equal("Inverters/Deye/SUN-14K-SG05LP3-EU-SM2.pdf", converted.DatasheetFile);
        Assert.Equal("%PDF-1.7 deye", File.ReadAllText(store.GetDatasheetPath(converted)!));
        Assert.True(File.Exists(Path.Combine(UserDirectory, JsonDataStore.InverterFileName + ".migrated")));
        Assert.False(File.Exists(Path.Combine(UserDirectory, JsonDataStore.InverterFileName)));
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Saving_a_series_moves_its_models_out_of_other_own_series()
    {
        JsonDataStore store = Load(Inverters(ValidInverter), Modules(ValidModule));
        store.SaveSeries(Series("Deye", "SUN-18K-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2")), null);

        store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2"), Deye("SUN-20K-SG05LP3-EU-SM2")), null);

        Assert.False(File.Exists(Path.Combine(UserDirectory, "Inverters", "Deye", "SUN-18K-SG05LP3-EU-SM2.json")));
        Assert.All(store.Inverters, i => Assert.Equal("SUN-SG05LP3-EU-SM2", i.Series));
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Series_file_keeps_its_valid_variants_and_an_own_series_replaces_the_shipped_one()
    {
        string shipped = Path.Combine(_directory, "Inverters", "Deye", "SUN-SG05LP3-EU-SM2.json");
        Directory.CreateDirectory(Path.GetDirectoryName(shipped)!);
        File.WriteAllText(shipped, $$"""
            { "Manufacturer": "Deye", "Series": "SUN-SG05LP3-EU-SM2", "Variants": [
              {{ValidInverter}},
              {{ValidInverter.Replace("SUN-18K", "SUN-20K", StringComparison.Ordinal).Replace("\"MPPT count\": 2", "\"MPPT count\": 0", StringComparison.Ordinal)}},
              {{ValidInverter.Replace("SUN-18K", "SUN-15K", StringComparison.Ordinal)}} ] }
            """);
        File.Delete(Path.Combine(_directory, JsonDataStore.InverterFileName));
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new TestPaths(_directory));
        File.WriteAllText(Path.Combine(_directory, JsonDataStore.ModuleFileName), Modules(ValidModule));
        store.Load();

        Assert.Equal(["SUN-18K-SG05LP3-EU-SM2", "SUN-15K-SG05LP3-EU-SM2"], store.Inverters.Select(i => i.Model));
        DataLoadIssue issue = Assert.Single(store.Issues);
        Assert.Equal("Inverters/Deye/SUN-SG05LP3-EU-SM2.json", issue.FileName);
        Assert.Contains("SUN-20K", issue.Entry, StringComparison.Ordinal);

        store.SaveSeries(Series("Deye", "SUN-SG05LP3-EU-SM2", Deye("SUN-18K-SG05LP3-EU-SM2", 170)), null);

        Assert.Equal(170, Assert.Single(store.Inverters).StartVoltageV); // the shipped SUN-15K belongs to the replaced series
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
