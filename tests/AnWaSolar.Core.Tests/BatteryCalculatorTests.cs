using System.Text.Json;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Battery compatibility with the shipped master data: Deye SUN-18K-SG05LP3-EU-SM2 (battery 40–60 V, max. charge and
/// discharge current 330 A), Deye SE-F12/SE-F16 (51.2 V, 44.8–57.6 V, 12.03/16 kWh, charge 230/160 A, discharge
/// 230 A, 90 % DoD, max. 32 in parallel) and Felicity FLA48314-EU (51.2 V, 44.8–57.6 V, 16 kWh, 160 A, ≥ 95 % DoD,
/// max. 15 in parallel).
/// </summary>
public sealed class BatteryCalculatorTests
{
    private static readonly JsonDataStore s_store = LoadShipped();

    private static JsonDataStore LoadShipped()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(),
            new DataPathProvider(new TempUserData()));
        store.Load();
        return store;
    }

    private static Inverter Inverter(string model) => s_store.Inverters.Single(i => i.Model == model);

    private static Battery Battery(string model) => s_store.Batteries.Single(b => b.Model == model);

    private static BatteryCalculationResult Check(string battery, int series, int parallel, string inverter = "SUN-18K-SG05LP3-EU-SM2") =>
        BatteryCalculator.Calculate(Inverter(inverter), Battery(battery), series, parallel);

    [Fact]
    public void Two_se_f16_in_parallel_fit_the_sun_18k()
    {
        // Bank 44.8–57.6 V lies within 40–60 V. Charge: 2 × 160 A = 320 A < 330 A, so the bank limits it to
        // 320 A · 51.2 V = 16.38 kW; discharge: 2 × 230 A = 460 A, the inverter limits it to 330 A · 51.2 V = 16.90 kW.
        BatteryCalculationResult result = Check("SE-F16", 1, 2);

        Assert.Equal(ResultState.Ok, result.State);
        Assert.Equal(51.2, result.BankNominalV!.Value, 6);
        Assert.Equal((44.8, 57.6), result.BankRangeV);
        Assert.Equal((40.0, 60.0), result.InverterRangeV);
        Assert.Equal(320, result.ChargeCurrentA);
        Assert.Equal(16384, result.ChargePowerW!.Value, 6);
        Assert.Equal(330, result.DischargeCurrentA);
        Assert.Equal(16896, result.DischargePowerW!.Value, 6);
        Assert.Equal(32, result.NominalEnergyKWh!.Value, 6);
        Assert.Equal(28.8, result.UsableEnergyKWh!.Value, 6); // 90 % DoD
        Assert.Contains(result.Checks, c => c.Kind == BatteryCheckKind.ChargeLimitedByBattery && c.State == ResultState.Neutral);
        Assert.DoesNotContain(result.Checks, c => c.Kind == BatteryCheckKind.DischargeLimitedByBattery);
    }

    [Fact]
    public void One_se_f12_is_limited_by_its_own_currents()
    {
        // 230 A charge and discharge < 330 A: 230 A · 51.2 V = 11.78 kW.
        BatteryCalculationResult result = Check("SE-F12", 1, 1);

        Assert.Equal(230, result.ChargeCurrentA);
        Assert.Equal(230, result.DischargeCurrentA);
        Assert.Equal(11776, result.DischargePowerW!.Value, 6);
        Assert.Equal(10.827, result.UsableEnergyKWh!.Value, 6); // 12.03 kWh · 90 %
        Assert.Equal(ResultState.Ok, result.State);
    }

    [Fact]
    public void Low_voltage_modules_in_series_exceed_the_battery_voltage_range()
    {
        // 2 × 51.2 V = 102.4 V nominal, max. 115.2 V > 60 V; the datasheet allows no series connection.
        BatteryCalculationResult result = Check("SE-F16", 2, 1);

        Assert.Equal(ResultState.Error, result.State);
        Assert.Contains(result.Checks, c => c.Kind == BatteryCheckKind.NominalVoltageOutside);
        Assert.Contains(result.Checks, c => c is { Kind: BatteryCheckKind.MaxVoltageAbove, Actual: 115.2, Limit: 60 });
        Assert.Contains(result.Checks, c => c is { Kind: BatteryCheckKind.SeriesNotStated, State: ResultState.Warning });
    }

    [Fact]
    public void More_modules_in_parallel_than_the_datasheet_allows_is_an_error()
    {
        BatteryCalculationResult felicity = Check("FLA48314-EU", 1, 16);

        BatteryCheck check = Assert.Single(felicity.Checks, c => c.Kind == BatteryCheckKind.ParallelExceeded);
        Assert.Equal((16, 15), (check.Actual, check.Limit));
        Assert.Equal(ResultState.Error, felicity.State);
        Assert.Equal(15.2, Check("FLA48314-EU", 1, 1).UsableEnergyKWh!.Value, 6); // 16 kWh · 95 %
    }

    [Fact]
    public void Inverter_without_battery_input_is_an_error()
    {
        BatteryCalculationResult result = Check("SE-F12", 1, 1, inverter: "SUN-10K-G06P3-EU-AM2");

        Assert.Contains(result.Checks, c => c is { Kind: BatteryCheckKind.NoBatteryInput, State: ResultState.Error });
        Assert.Null(result.InverterRangeV);
        Assert.Null(result.ChargeCurrentA);
    }

    [Fact]
    public void Bank_below_the_inverter_range_is_a_warning()
    {
        // A module range reaching below 40 V: the inverter stops discharging earlier.
        Battery battery = Battery("SE-F12");
        battery = new Battery
        {
            Manufacturer = battery.Manufacturer,
            Model = battery.Model,
            NominalVoltageV = battery.NominalVoltageV,
            VoltageRangeV = "38-57.6",
            NominalEnergyKWh = battery.NominalEnergyKWh,
            MaxChargeCurrentA = battery.MaxChargeCurrentA,
            MaxDischargeCurrentA = battery.MaxDischargeCurrentA,
        };

        BatteryCalculationResult result = BatteryCalculator.Calculate(Inverter("SUN-18K-SG05LP3-EU-SM2"), battery, 1, 1);

        Assert.Contains(result.Checks, c => c is { Kind: BatteryCheckKind.MinVoltageBelow, State: ResultState.Warning, Actual: 38, Limit: 40 });
        Assert.Contains(result.Checks, c => c.Kind == BatteryCheckKind.UsableEnergyMissing);
        Assert.Equal(ResultState.Warning, result.State);
    }

    [Fact]
    public void Missing_battery_is_a_warning()
    {
        BatteryCalculationResult result = BatteryCalculator.Calculate(Inverter("SUN-18K-SG05LP3-EU-SM2"), null, 1, 1);

        Assert.Equal(BatteryCheckKind.BatteryMissing, Assert.Single(result.Checks).Kind);
        Assert.Equal(ResultState.Warning, result.State);
    }

    [Fact]
    public void Card_report_and_text_show_the_battery()
    {
        StringCalculationResult strings = StringCalculator.Calculate(Inverter("SUN-18K-SG05LP3-EU-SM2"), [Aiko(1, 11), Aiko(2, 11)], Parameters());
        BatteryCalculationResult battery = Check("SE-F16", 2, 1);

        ResultOverview overview = ResultPresenter.Present(strings, null, null, battery);
        string report = ReportBuilder.Build(strings, Parameters(), new DateTime(2026, 11, 25), null, null, battery);
        string text = CalculationTextFormatter.Format(strings, null, battery);

        Assert.Equal(ResultState.Error, overview.Battery!.State);
        Assert.Equal(ResultState.Error, overview.State);
        Assert.Contains(overview.Battery.Rows, r => r.Quantity == "Bank voltage" && r.State == ResultState.Error);
        Assert.Contains("## Battery storage", report, StringComparison.Ordinal);
        Assert.Contains("Deye SE-F16, 2 in series × 1 in parallel", report, StringComparison.Ordinal);
        Assert.Contains("exceeds the max. battery voltage of the inverter", text, StringComparison.Ordinal);
    }

    private sealed class TempUserData : IUserDataLocation
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));
    }
}
