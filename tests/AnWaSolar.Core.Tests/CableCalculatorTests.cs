using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Cable and fuse sizing with the shipped <c>Data/CableTables.json</c>, the AIKO-A500-MAH60Db (Isc 14.05 A, Impp
/// 13.21 A, Vmpp 37.9 V) and the Deye SUN-18K-SG05LP3-EU-SM2 (max. AC current 28.7 A, three-phase).
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
        Assert.Equal(56, s_tables.CopperConductivity);
        Assert.Equal(["A1", "B1", "B2", "C", "E"], s_tables.AcMethods);
    }

    [Fact]
    public void String_cable_meets_current_and_voltage_drop()
    {
        // 11 modules: Vmpp 416.9 V. Design current 1.25 × 14.05 = 17.56 A (1.5 mm² carries 29 A on a surface), but
        // 20 m on 1.5 mm² drop 2 · 20 · 13.21 / (56 · 1.5) = 6.29 V = 1.51 % > 1 %; 2.5 mm² drops 0.91 %.
        DcCableResult dc = Cables(Settings(), Aiko(1, 11), Aiko(2, 11)).Dc[0];

        Assert.Equal(17.5625, dc.DesignCurrentA, 6);
        Assert.Equal(2.5, dc.CrossSectionMm2);
        Assert.Equal(2 * 20 * 13.21 / (56 * 2.5) / (11 * 37.9) * 100, dc.VoltageDropPct!.Value, 6);
        Assert.False(dc.FuseRequired);
        Assert.Equal(ResultState.Ok, dc.State);
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
        Assert.Equal(2.5, dc.CrossSectionMm2); // carries the 25 A fuse rating (39 A) and meets the voltage drop
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
        Assert.Empty(dc.Notes);
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
