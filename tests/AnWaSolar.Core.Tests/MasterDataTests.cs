using System.Text.Json;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Checks the master data files that are shipped with the application.
/// </summary>
public sealed class MasterDataTests
{
    private static JsonDataStore LoadShippedData()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new NoUserData()));
        store.Load();
        return store;
    }

    [Fact]
    public void Shipped_files_load_completely()
    {
        JsonDataStore store = LoadShippedData();

        Assert.NotEmpty(store.Modules);
        Assert.NotEmpty(store.Inverters);
        Assert.NotEmpty(store.Batteries);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void Modules_have_consistent_datasheet_values()
    {
        foreach (Models.PVModule module in LoadShippedData().Modules)
        {
            string name = module.DisplayName;
            Assert.False(string.IsNullOrWhiteSpace(module.Manufacturer), name);
            Assert.False(string.IsNullOrWhiteSpace(module.Model), name);
            Assert.True(module.PmaxW > 0, name);
            Assert.True(module.VocV > module.VmppV, $"{name}: UOC must exceed UMPP");
            Assert.True(module.IscA > module.ImppA, $"{name}: ISC must exceed IMPP");
            Assert.True(module.VocTempCoeffPctPerC < 0, $"{name}: VOC coefficient must be negative");
            Assert.True(module.IscTempCoeffPctPerC > 0, $"{name}: ISC coefficient must be positive (the current rises with the temperature)");
        }
    }

    [Fact]
    public void Inverters_have_consistent_datasheet_values()
    {
        foreach (Models.Inverter inverter in LoadShippedData().Inverters)
        {
            string name = inverter.DisplayName;
            Assert.True(inverter.MpptCount > 0, name);
            Assert.True(inverter.MaxStringsPerMppt > 0, name);
            Assert.True(inverter.MaxDcInputVoltageV > 0, name);
            Assert.True(inverter.MaxShortCircuitCurrentA >= inverter.MaxInputCurrentA, name);
            Assert.Matches(@"^\d+-\d+$", inverter.MpptVoltageRangeV);
        }
    }

    [Fact]
    public void Batteries_and_battery_inputs_have_consistent_datasheet_values()
    {
        JsonDataStore store = LoadShippedData();
        foreach (Models.Battery battery in store.Batteries)
        {
            string name = battery.DisplayName;
            Assert.True(Calculation.StringCalculator.TryParseVoltageRange(battery.VoltageRangeV, out double min, out double max), name);
            Assert.InRange(battery.NominalVoltageV, min, max);
            Assert.True(battery.NominalEnergyKWh > 0 && battery.MaxChargeCurrentA > 0 && battery.MaxDischargeCurrentA > 0, name);
        }

        foreach (Models.Inverter inverter in store.Inverters.Where(i => i.HasBatteryInput))
        {
            Assert.True(inverter.MaxBatteryChargeCurrentA > 0 && inverter.MaxBatteryDischargeCurrentA > 0, inverter.DisplayName);
        }

        Assert.Equal(3, store.Inverters.Count(i => i.HasBatteryInput));
    }

    // Only the shipped files: a user data folder that does not exist.
    private sealed class NoUserData : IUserDataLocation
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));
    }
}
