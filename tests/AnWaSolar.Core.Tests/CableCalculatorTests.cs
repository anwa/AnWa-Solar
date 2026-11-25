using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Cable and fuse sizing with the shipped <c>Data/CableTables.json</c>, the AIKO-A500-MAH60Db (Isc 14.05 A, Impp
/// 13.21 A, Vmpp 37.9 V, no lead values: the default 0.5 m per lead and 4 mm² applies), the Trina TSM-445-NEG9R.28
/// (leads 4.0 mm², 1100/1100 mm), the Deye SUN-18K-SG05LP3-EU-SM2 (max. AC current 28.7 A, three-phase, battery
/// 330 A) and the Deye SE-F16 battery (charge 160 A, discharge 230 A).
/// </summary>
public sealed class CableCalculatorTests
{
    private static readonly CableTables s_tables =
        CableTables.Load(Path.Combine(AppContext.BaseDirectory, "Data", CableTables.FileName), out _)!;

    private static CableSettings Settings(double dcLength = 20, double acLength = 10) => new()
    {
        DcLengthsM = [dcLength, dcLength],
        DcInstallation = SolarCableInstallation.OnSurface,
        DcAmbientC = 60,
        AcLengthM = acLength,
        AcMethod = "C",
        AcAmbientC = 30,
    };

    private static CableCalculationResult Cables(CableSettings settings, params StringConfiguration[] strings) =>
        CableCalculator.Calculate(StringCalculator.Calculate(Deye18K(), strings, Parameters()), settings, s_tables);

    [Fact]
    public void Shipped_tables_are_complete_and_confirmed_by_the_user()
    {
        Assert.NotNull(s_tables);
        Assert.True(s_tables.Confirmed);
        Assert.True(s_tables.BatteryCable.Confirmed);
        Assert.Equal(56, s_tables.CopperConductivity);
        Assert.Equal(["A1", "B1", "B2", "C", "E"], s_tables.AcMethods);
    }

    private const double AikoLeadDropPct = 11 * 1.0 * 13.21 / (56 * 4.0) / (11 * 37.9) * 100; // 0.16 %

    [Fact]
    public void String_cable_meets_current_and_voltage_drop_with_the_module_leads()
    {
        // 11 modules: Vmpp 416.9 V. Design current 1.25 × 14.05 = 17.56 A (1.5 mm² carries 29 A on a surface). The
        // leads add 11 × (0.5 + 0.5) m on 4 mm² = 0.16 %; 20 m on 2.5 mm² drop 0.91 %, together 1.06 % > 1 %, so
        // 4 mm² (0.57 % + 0.16 % = 0.72 %).
        DcCableResult dc = Cables(Settings(), Aiko(1, 11), Aiko(2, 11)).Dc[0];

        Assert.Equal(17.5625, dc.DesignCurrentA, 6);
        Assert.Equal(4, dc.CrossSectionMm2);
        Assert.False(dc.LeadsFromDatasheet);
        Assert.Equal(1.0, dc.LeadLengthPerModuleM, 6);
        Assert.Equal(AikoLeadDropPct, dc.LeadDropPct, 6);
        Assert.Equal(2 * 20 * 13.21 / (56 * 4.0) / (11 * 37.9) * 100, dc.HomeRunDropPct!.Value, 6);
        Assert.Equal(dc.HomeRunDropPct!.Value + AikoLeadDropPct, dc.VoltageDropPct!.Value, 6);
        Assert.False(dc.FuseRequired);
        Assert.Equal(ResultState.Ok, dc.State);
    }

    [Fact]
    public void Lead_values_from_the_datasheet_replace_the_default()
    {
        // Trina TSM-445: 11 × 44.3 V = 487.3 V, Impp 10.05 A. Leads 11 × 2.2 m on 4 mm² = 0.22 %; 20 m on 1.5 mm²
        // would give 0.98 % + 0.22 % > 1 %, 2.5 mm² gives 0.59 % + 0.22 % = 0.81 %.
        DatasheetEntryBuilder.TryBuildModule(DatasheetEntryBuilderTests.Trina445(), out PVModule? trina);
        trina!.CableCrossSectionMm2 = 4.0;
        trina.CableLengthPlusMm = 1100;
        trina.CableLengthMinusMm = 1100;
        var config = new StringConfiguration { MpptIndex = 1, SelectedModule = trina, ModulesPerString = 11, ParallelStrings = 1 };

        DcCableResult dc = Cables(Settings(), config, Aiko(2, 11)).Dc[0];

        Assert.True(dc.LeadsFromDatasheet);
        Assert.Equal(2.2, dc.LeadLengthPerModuleM, 6);
        Assert.Equal(11 * 2.2 * 10.05 / (56 * 4.0) / (11 * 44.3) * 100, dc.LeadDropPct, 6);
        Assert.Equal(2.5, dc.CrossSectionMm2);
        Assert.Equal(0.8121, dc.VoltageDropPct!.Value, 3);
    }

    [Fact]
    public void Chosen_cross_section_is_checked_not_resized()
    {
        // 2.5 mm² chosen: it carries the current, but 0.91 % + 0.16 % = 1.06 % > 1 % is a warning.
        CableSettings settings = Settings();
        settings.DcCrossSectionMm2 = 2.5;

        DcCableResult dc = Cables(settings, Aiko(1, 11), Aiko(2, 11)).Dc[0];

        Assert.Equal(2.5, dc.CrossSectionMm2);
        Assert.True(dc.CrossSectionChosen);
        Assert.Equal(ResultState.Warning, dc.State);
        Assert.Contains(dc.Notes, n => n.State == ResultState.Warning && n.Text.Contains("above the limit", StringComparison.Ordinal));
    }

    [Fact]
    public void Chosen_cross_section_below_the_fuse_rating_is_an_error()
    {
        // 3 strings need gPV 25 A; two touching cables on a surface: 1.5 mm² carries 24 A < 25 A.
        CableSettings settings = Settings();
        settings.DcCrossSectionMm2 = 1.5;
        settings.DcInstallation = SolarCableInstallation.TwoTouchingOnSurface;
        StringConfiguration config = Aiko(1, 11, 3);
        config.SelectedModule!.MaxSeriesFuseA = 25;

        DcCableResult dc = Cables(settings, config, Aiko(2, 11)).Dc[0];

        Assert.Equal(ResultState.Error, dc.State);
        Assert.Contains(dc.Notes, n => n.State == ResultState.Error && n.Text.Contains("less than the required 25", StringComparison.Ordinal));
    }

    [Fact]
    public void Three_parallel_strings_need_fuses_within_the_module_rating()
    {
        // (3 − 1) × 17.56 A = 35.1 A > 25 A: fuses between 1.5 × 14.05 = 21.1 A and min(2.4 × 14.05, 25) = 25 A.
        StringConfiguration config = Aiko(1, 11, 3);
        config.SelectedModule!.MaxSeriesFuseA = 25;

        DcCableResult dc = Cables(Settings(), config, Aiko(2, 11)).Dc[0];

        Assert.True(dc.FuseRequired);
        Assert.Equal(21.075, dc.FuseMinA!.Value, 6);
        Assert.Equal(25, dc.FuseMaxA);
        Assert.Equal(25, dc.FuseRatingA);
        Assert.Equal(4, dc.CrossSectionMm2); // 2.5 mm² carries the 25 A fuse rating (39 A), but only 4 mm² meets the voltage drop
        Assert.Equal(ResultState.Warning, dc.State);
    }

    [Fact]
    public void Fuse_range_without_a_standard_rating_is_an_error()
    {
        // Max. series fuse 20 A < 1.5 × Isc = 21.1 A: no fuse can protect the strings.
        StringConfiguration config = Aiko(1, 11, 3);
        config.SelectedModule!.MaxSeriesFuseA = 20;

        DcCableResult dc = Cables(Settings(), config, Aiko(2, 11)).Dc[0];

        Assert.True(dc.FuseRequired);
        Assert.Null(dc.FuseRatingA);
        Assert.Equal(ResultState.Error, dc.State);
    }

    [Fact]
    public void Two_parallel_strings_without_fuse_rating_need_no_fuses()
    {
        DcCableResult dc = Cables(Settings(), Aiko(1, 11, 2), Aiko(2, 11)).Dc[0];

        Assert.False(dc.FuseRequired);
        Assert.All(dc.Notes, n => Assert.Equal(ResultState.Neutral, n.State)); // only the note about the default leads
    }

    [Fact]
    public void Ac_line_gets_the_next_breaker_and_a_cable_that_carries_it()
    {
        // 28.7 A → 32 A; method C, three loaded conductors: 4 mm² carries 32 A; 10 m drop √3 · 10 · 28.7 / (56 · 4) = 0.55 %.
        AcCableResult ac = Cables(Settings(), Aiko(1, 11), Aiko(2, 11)).Ac!;

        Assert.Equal(32, ac.BreakerA);
        Assert.Equal(4, ac.CrossSectionMm2);
        Assert.Equal(32, ac.CarryingCapacityA);
        Assert.Equal(Math.Sqrt(3) * 10 * 28.7 / (56 * 4) / 400 * 100, ac.VoltageDropPct!.Value, 6);
    }

    [Fact]
    public void Higher_ambient_temperature_needs_a_larger_ac_cable()
    {
        // 40 °C: factor 0.87; 4 mm² carries 27.8 A < 32 A, 6 mm² 35.7 A.
        CableSettings settings = Settings();
        settings.AcAmbientC = 40;

        Assert.Equal(6, Cables(settings, Aiko(1, 11), Aiko(2, 11)).Ac!.CrossSectionMm2);
    }

    [Fact]
    public void Missing_lengths_give_no_size_but_a_note()
    {
        CableCalculationResult cables = Cables(Settings(dcLength: 0, acLength: 0), Aiko(1, 11), Aiko(2, 11));

        Assert.Null(cables.Dc[0].CrossSectionMm2);
        Assert.Equal(ResultState.Neutral, cables.Dc[0].State);
        Assert.Equal(32, cables.Ac!.BreakerA);
        Assert.Null(cables.Ac.CrossSectionMm2);
    }

    [Fact]
    public void Unconfirmed_tables_make_the_card_a_warning_and_the_report_says_so()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), Aiko(2, 11)], Parameters());
        CableTables unconfirmed = CableTables.Load(Path.Combine(AppContext.BaseDirectory, "Data", CableTables.FileName), out _)!;
        unconfirmed.Confirmed = false;
        CableCalculationResult cables = CableCalculator.Calculate(result, Settings(), unconfirmed);

        CableView view = ResultPresenter.Present(result, cables, Settings()).Cables!;
        string report = ReportBuilder.Build(result, Parameters(), new DateTime(2026, 11, 24), cables, unconfirmed);

        Assert.Equal(ResultState.Warning, view.State);
        Assert.Contains(view.Notes, n => n.Text == CableTexts.Unconfirmed);
        Assert.Contains("## Cables and fuses", report, StringComparison.Ordinal);
        Assert.Contains(CableTexts.Unconfirmed, report, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmed_tables_give_no_note()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), Aiko(2, 11)], Parameters());
        CableCalculationResult cables = CableCalculator.Calculate(result, Settings(), s_tables);

        CableView view = ResultPresenter.Present(result, cables, Settings()).Cables!;

        Assert.Equal(ResultState.Ok, view.State);
        Assert.DoesNotContain(view.Notes, n => n.Text == CableTexts.Unconfirmed);
        Assert.DoesNotContain(CableTexts.Unconfirmed, ReportBuilder.Build(result, Parameters(), new DateTime(2026, 11, 25), cables, s_tables), StringComparison.Ordinal);
    }

    private static CableTables UnconfirmedBatteryTables()
    {
        CableTables tables = CableTables.Load(Path.Combine(AppContext.BaseDirectory, "Data", CableTables.FileName), out _)!;
        tables.BatteryCable.Confirmed = false;
        return tables;
    }

    private static CableCalculationResult BatteryCables(CableSettings settings, int parallel, CableTables? tables = null)
    {
        Inverter inverter = Deye18K();
        inverter.BatteryVoltageRangeV = "40-60";
        inverter.MaxBatteryChargeCurrentA = 330;
        inverter.MaxBatteryDischargeCurrentA = 330;
        inverter.BatteryInputs = 2;
        var seF16 = new Battery
        {
            Manufacturer = "Deye",
            Model = "SE-F16",
            NominalVoltageV = 51.2,
            VoltageRangeV = "44.8-57.6",
            NominalCapacityAh = 314,
            NominalEnergyKWh = 16,
            DepthOfDischargePct = 90,
            MaxChargeCurrentA = 160,
            MaxDischargeCurrentA = 230,
            MaxModulesInParallel = 32,
        };
        StringCalculationResult strings = StringCalculator.Calculate(inverter, [Aiko(1, 11), Aiko(2, 11)], Parameters());
        BatteryCalculationResult battery = BatteryCalculator.Calculate(inverter, seF16, 1, parallel);
        return CableCalculator.Calculate(strings, settings, tables ?? s_tables, battery);
    }

    [Fact]
    public void Battery_cables_without_busbar_run_from_each_battery_to_the_inverter()
    {
        // Each SE-F16 cable: Ib = min(230 A, 330 A) = 230 A → fuse 250 A → 70 mm² (251 A). Operating current of the
        // inverter 330 A shared by 2 cables: 2 · 2 m · 165 A / (56 · 70) = 0.168 V = 0.33 % of 51.2 V.
        CableSettings settings = Settings();
        settings.BatteryCableLengthM = 2;

        BatteryCableResult battery = BatteryCables(settings, 2).Battery!;

        Assert.False(battery.Busbar);
        Assert.Null(battery.MainCable);
        Assert.Equal(2, battery.BatteryCables.Count);
        Assert.Equal(230, battery.BatteryCables.DesignCurrentA);
        Assert.Equal(250, battery.BatteryCables.FuseA);
        Assert.Equal(70, battery.BatteryCables.CrossSectionMm2);
        Assert.Equal(2 * 2 * 165 / (56.0 * 70) / 51.2 * 100, battery.TotalDropPct!.Value, 6);
        Assert.Equal(ResultState.Ok, battery.State);
    }

    [Fact]
    public void Battery_cables_with_busbar_add_the_main_cable()
    {
        // Battery cables 1 m: 70 mm², fuse 250 A, 0.16 %. Main cable: Ib = max(320 A charge, 330 A discharge) = 330 A
        // → fuse 355 A → 150 mm² (406 A; 120 mm² carries 352 A); 2 · 3 m · 330 A / (56 · 150) = 0.24 V = 0.46 %.
        CableSettings settings = Settings();
        settings.BatteryBusbar = true;
        settings.BatteryCableLengthM = 1;
        settings.BatteryMainLengthM = 3;

        CableCalculationResult cables = BatteryCables(settings, 2);
        BatteryCableResult battery = cables.Battery!;

        Assert.Equal(70, battery.BatteryCables.CrossSectionMm2);
        BatteryCableRun main = battery.MainCable!;
        Assert.Equal(330, main.DesignCurrentA);
        Assert.Equal(355, main.FuseA);
        Assert.Equal(150, main.CrossSectionMm2);
        double expected = (2 * 1 * 165 / (56.0 * 70) + 2 * 3 * 330 / (56.0 * 150)) / 51.2 * 100;
        Assert.Equal(expected, battery.TotalDropPct!.Value, 6);
        Assert.Equal(ResultState.Ok, cables.State);
        Assert.Equal(ResultState.Warning, BatteryCables(settings, 2, UnconfirmedBatteryTables()).State); // unconfirmed values
    }

    [Fact]
    public void Battery_problems_are_reported()
    {
        // 4 batteries need 4 cables but the inverter has 2 inputs; a chosen 95 mm² main cable carries 304 A < 355 A.
        CableSettings settings = Settings();
        settings.BatteryCableLengthM = 2;
        Assert.Contains(BatteryCables(settings, 4).Battery!.Notes, n => n.State == ResultState.Warning && n.Text.Contains("2 battery inputs", StringComparison.Ordinal));

        settings.BatteryBusbar = true;
        settings.BatteryMainLengthM = 3;
        settings.BatteryMainCrossSectionMm2 = 95;
        BatteryCableResult battery = BatteryCables(settings, 2).Battery!;
        Assert.Equal(ResultState.Error, battery.State);
        Assert.Contains(battery.Notes, n => n.State == ResultState.Error && n.Text.Contains("less than the fuse rating 355", StringComparison.Ordinal));
    }

    [Fact]
    public void Battery_cables_appear_in_the_card_and_the_report()
    {
        CableSettings settings = Settings();
        settings.BatteryCableLengthM = 2;
        CableTables unconfirmed = UnconfirmedBatteryTables();
        CableCalculationResult cables = BatteryCables(settings, 2, unconfirmed);
        StringCalculationResult strings = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), Aiko(2, 11)], Parameters());

        CableView view = ResultPresenter.Present(strings, cables, settings).Cables!;
        string report = ReportBuilder.Build(strings, Parameters(), new DateTime(2026, 11, 25), cables, unconfirmed);
        Assert.DoesNotContain(ResultPresenter.Present(strings, BatteryCables(settings, 2), settings).Cables!.Notes, n => n.Text == CableTexts.BatteryUnconfirmed);

        Assert.Contains(view.Rows, r => r.Quantity == "Battery cables to the inverter (2 ×)" && r.Value.StartsWith("70 mm²", StringComparison.Ordinal));
        Assert.Contains(view.Notes, n => n.Text == CableTexts.BatteryUnconfirmed);
        Assert.Contains(view.Rows, r => r.Quantity == "MPPT 1 module leads" && r.Limit == "your default");
        Assert.Contains("Battery without busbar", report, StringComparison.Ordinal);
        Assert.Contains(CableTexts.BatteryUnconfirmed, report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(60, 1.0)]
    [InlineData(65, 0.91)]
    [InlineData(25, 1.0)]
    public void Temperature_factor_uses_the_next_higher_table_entry(double ambient, double expected)
    {
        Assert.Equal(expected, CableTables.Factor(s_tables.SolarCable.AmbientFactors, ambient));
    }

    [Fact]
    public void Values_above_the_table_have_no_factor()
    {
        Assert.Null(CableTables.Factor(s_tables.SolarCable.AmbientFactors, 120));
        Assert.Null(CableTables.Factor(s_tables.Grouping.Factors, 12));
    }
}
