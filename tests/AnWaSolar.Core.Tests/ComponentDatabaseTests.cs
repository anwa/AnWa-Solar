using System.Text.Json;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

/// <summary>Editing series in the component database: field texts of entries, renaming and deleting own series.</summary>
public sealed class ComponentDatabaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static JsonDataStore Shipped()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new Location(Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N")))));
        store.Load();
        return store;
    }

    private JsonDataStore Store()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new Location(_root)));
        store.Load();
        return store;
    }

    private static string Json<T>(T entry) => JsonSerializer.Serialize(entry);

    [Fact]
    public void Field_texts_of_every_shipped_entry_build_the_same_entry()
    {
        JsonDataStore store = Shipped();

        foreach (PVModule module in store.Modules)
        {
            Assert.Empty(DatasheetEntryBuilder.TryBuildModule(DatasheetEntryBuilder.ToValues(module), out PVModule? built));
            Assert.Equal(Json(module), Json(built));
        }

        foreach (Inverter inverter in store.Inverters)
        {
            Assert.Empty(DatasheetEntryBuilder.TryBuildInverter(DatasheetEntryBuilder.ToValues(inverter), out Inverter? built));
            Assert.Equal(Json(inverter), Json(built));
        }

        foreach (Battery battery in store.Batteries)
        {
            Assert.Empty(DatasheetEntryBuilder.TryBuildBattery(DatasheetEntryBuilder.ToValues(battery), out Battery? built));
            Assert.Equal(Json(battery), Json(built));
        }
    }

    [Fact]
    public void Values_stored_as_zero_for_not_stated_stay_empty()
    {
        Inverter tenK = Shipped().Inverters.Single(i => i.Model == "SUN-10K-G06P3-EU-AM2");

        Dictionary<string, string?> values = DatasheetEntryBuilder.ToValues(tenK);

        Assert.Null(values[DatasheetFields.RatedAcPowerW]);
        Assert.Null(values[DatasheetFields.BatteryRangeV]);
        Assert.Equal(tenK.MpptVoltageRangeV, values[DatasheetFields.MpptRangeV]);
    }

    [Fact]
    public void Renamed_series_keeps_its_datasheet_and_replaces_the_old_file()
    {
        JsonDataStore store = Store();
        Battery seF16 = Shipped().Batteries.Single(b => b.Model == "SE-F16");
        string pdf = Path.Combine(_root, "se-f.pdf");
        Directory.CreateDirectory(_root);
        File.WriteAllText(pdf, "%PDF-1.7 se-f");
        Battery saved = store.SaveSeries(new ComponentSeries<Battery> { Manufacturer = "Deye", Series = "SE-F old", Variants = [seF16] }, pdf).Single();
        string datasheet = store.GetDatasheetPath(saved)!;

        store.SaveSeries(new ComponentSeries<Battery> { Manufacturer = "Deye", Series = "SE-F16 series", Variants = [seF16] }, datasheet, ("Deye", "SE-F old"));

        Battery renamed = store.Batteries.Single(b => b.IsUserEntry);
        Assert.Equal("SE-F16 series", renamed.Series);
        Assert.Equal("%PDF-1.7 se-f", File.ReadAllText(store.GetDatasheetPath(renamed)!));
        Assert.False(File.Exists(Path.Combine(_root, "Data", "Batteries", "Deye", "SE-F old.json")));
        Assert.False(File.Exists(Path.Combine(_root, "Data", "Batteries", "Deye", "SE-F old.pdf")));
    }

    [Fact]
    public void Power_classes_are_ordered_by_ascending_power()
    {
        JsonDataStore store = Store();
        Inverter[] deye = [.. store.Inverters.Where(i => i.Series == "SUN-SG05LP3-EU-SM2")];
        Assert.Equal([15000, 18000, 20000], deye.Select(i => i.RatedAcPowerW));
        Assert.All(store.Batteries.Where(b => b.Series == "SE-F").Zip(store.Batteries.Where(b => b.Series == "SE-F").Skip(1)),
            pair => Assert.True(pair.First.NominalEnergyKWh <= pair.Second.NominalEnergyKWh));

        store.SaveSeries(new ComponentSeries<Inverter> { Manufacturer = "Deye", Series = "SUN-SG05LP3-EU-SM2", Variants = [.. deye.Reverse()] }, null);

        Assert.Equal(["SUN-15K-SG05LP3-EU-SM2", "SUN-18K-SG05LP3-EU-SM2", "SUN-20K-SG05LP3-EU-SM2"],
            store.Inverters.Where(i => i.Series == "SUN-SG05LP3-EU-SM2").Select(i => i.Model));
        string json = File.ReadAllText(Path.Combine(_root, "Data", "Inverters", "Deye", "SUN-SG05LP3-EU-SM2.json"));
        Assert.True(json.IndexOf("SUN-15K", StringComparison.Ordinal) < json.IndexOf("SUN-20K", StringComparison.Ordinal));
    }

    [Fact]
    public void Deleting_an_own_copy_brings_back_the_shipped_series()
    {
        JsonDataStore store = Store();
        Inverter eighteen = store.Inverters.Single(i => i.Model == "SUN-18K-SG05LP3-EU-SM2");
        eighteen.StartVoltageV = 170;
        store.SaveSeries(new ComponentSeries<Inverter> { Manufacturer = "Deye", Series = "SUN-SG05LP3-EU-SM2", Variants = [eighteen] }, null);
        Assert.Equal(170, store.Inverters.Single(i => i.Model == "SUN-18K-SG05LP3-EU-SM2").StartVoltageV);
        Assert.DoesNotContain(store.Inverters, i => i.Model == "SUN-20K-SG05LP3-EU-SM2"); // the own series replaces the shipped one

        Assert.True(store.DeleteSeries<Inverter>("Deye", "SUN-SG05LP3-EU-SM2"));

        Assert.Equal(160, store.Inverters.Single(i => i.Model == "SUN-18K-SG05LP3-EU-SM2").StartVoltageV);
        Assert.Contains(store.Inverters, i => i.Model == "SUN-20K-SG05LP3-EU-SM2");
        Assert.False(Directory.Exists(Path.Combine(_root, "Data", "Inverters", "Deye")));
        Assert.False(store.DeleteSeries<Inverter>("Deye", "SUN-SG05LP3-EU-SM2"));
    }

    private sealed class Location(string path) : IUserDataLocation
    {
        public string DirectoryPath { get; } = path;
    }
}
