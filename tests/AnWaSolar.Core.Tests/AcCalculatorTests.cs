using System.Globalization;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// AC side with the datasheet values of the Deye SUN-18K-SG05LP3-EU-SM2 (18 kW, 19.8 kVA, 26.1/28.7 A at 230/400 V,
/// three-phase) and the AIKO-A500-MAH60Db (500 W) from <see cref="StringCalculatorTests"/>.
/// </summary>
public sealed class AcCalculatorTests
{
    public AcCalculatorTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    private static AcCalculationResult Ac(Inverter inverter, CalculationParameters parameters, params StringConfiguration[] strings) =>
        StringCalculator.Calculate(inverter, strings, parameters).Ac!;

    [Fact]
    public void Dc_ac_ratio_uses_the_pv_peak_power_of_the_calculated_mppts()
    {
        // 2 MPPTs × 11 modules × 2 strings × 500 W = 22 kWp; 22 / 18 = 1.222.
        AcCalculationResult ac = Ac(Deye18K(), Parameters(), Aiko(1, 11, 2), Aiko(2, 11, 2));

        Assert.Equal(22000, ac.PvPeakPowerW, 6);
        Assert.Equal(22000.0 / 18000, ac.DcAcRatio!.Value, 6);
        Assert.Equal(19800, ac.ApparentPowerVA);
        Assert.Equal(28.7, ac.MaxAcCurrentA);
        Assert.Empty(ac.Checks);
    }

    [Fact]
    public void Small_pv_power_gives_a_low_ratio_warning()
    {
        // 2 × 11 × 1 × 500 W = 11 kWp; 11 / 18 = 0.61 < 0.9.
        AcCalculationResult ac = Ac(Deye18K(), Parameters(), Aiko(1, 11), Aiko(2, 11));

        AcCheck check = Assert.Single(ac.Checks);
        Assert.Equal(AcCheckKind.DcAcRatioLow, check.Kind);
        Assert.Equal(ResultState.Warning, check.State);
        Assert.False(ac.HasErrors);
    }

    [Fact]
    public void Disabled_mppt_does_not_count()
    {
        StringConfiguration second = Aiko(2, 11, 2);
        second.IsEnabled = false;

        AcCalculationResult ac = Ac(Deye18K(), Parameters(), Aiko(1, 11, 2), second);

        Assert.Equal(11000, ac.PvPeakPowerW, 6);
    }

    [Fact]
    public void Single_phase_inverter_above_the_unbalanced_load_limit_is_an_error()
    {
        Inverter inverter = Deye18K();
        inverter.AcPhases = 1;
        inverter.MaxAcApparentPowerVA = 5000;
        inverter.RatedAcPowerW = 5000;

        StringCalculationResult result = StringCalculator.Calculate(inverter, [Aiko(1, 11), Aiko(2, 11)], Parameters());

        AcCheck check = Assert.Single(result.Ac!.Checks, c => c.Kind == AcCheckKind.UnbalancedLoad);
        Assert.Equal(ResultState.Error, check.State);
        Assert.Equal(4600, check.Limit);
        Assert.False(result.IsOk);
    }

    [Fact]
    public void System_above_the_registration_limit_is_a_warning()
    {
        Inverter inverter = Deye18K();
        inverter.MaxAcApparentPowerVA = 33000;

        AcCalculationResult ac = Ac(inverter, Parameters(), Aiko(1, 11, 2), Aiko(2, 11, 2));

        AcCheck check = Assert.Single(ac.Checks, c => c.Kind == AcCheckKind.RegistrationLimit);
        Assert.Equal(ResultState.Warning, check.State);
        Assert.Equal(30000, check.Limit);
    }

    [Fact]
    public void Feed_in_limit_caps_the_feed_in_power()
    {
        CalculationParameters parameters = Parameters();
        parameters.FeedInLimitPct = 60;

        AcCalculationResult ac = Ac(Deye18K(), parameters, Aiko(1, 11, 2), Aiko(2, 11, 2));

        Assert.Equal(13200, ac.FeedInCapW!.Value, 6);
        Assert.Equal(ResultState.Neutral, Assert.Single(ac.Checks).State);
    }

    [Fact]
    public void Missing_ac_values_are_reported_and_not_estimated()
    {
        Inverter inverter = Deye18K();
        inverter.RatedAcPowerW = 0;
        inverter.MaxAcApparentPowerVA = 0;
        inverter.MaxAcCurrentA = 0;
        inverter.AcPhases = 0;

        AcCalculationResult ac = Ac(inverter, Parameters(), Aiko(1, 11, 2), Aiko(2, 11, 2));

        Assert.Null(ac.DcAcRatio);
        Assert.Null(ac.ApparentPowerVA);
        Assert.Equal(["rated AC power", "AC phases", "max. AC current"], ac.MissingValues);
        Assert.Equal(AcCheckKind.AcValuesMissing, Assert.Single(ac.Checks).Kind);
    }

    [Fact]
    public void Ac_card_and_report_show_the_values()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11, 2), Aiko(2, 11, 2)], Parameters());

        AcView view = ResultPresenter.Present(result).Ac!;
        string report = ReportBuilder.Build(result, Parameters(), new DateTime(2026, 11, 24));

        Assert.Equal(ResultState.Ok, view.State);
        Assert.Contains(view.Rows, r => r.Quantity == "DC/AC ratio" && r.Value == "1.22" && r.Limit == "0.9 … 1.3");
        Assert.Contains("## AC side and grid connection", report, StringComparison.Ordinal);
        Assert.Contains("DC/AC ratio: 1.22", report, StringComparison.Ordinal);
    }
}
