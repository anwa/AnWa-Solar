using System.Globalization;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// The presentation of the results area, with the Deye SUN-18K-SG05LP3-EU-SM2 and the AIKO-A500-MAH60Db from
/// <see cref="StringCalculatorTests"/> (Tmin −20 °C, Tmax 70 °C, safety margin 5 %).
/// </summary>
public sealed class ResultPresenterTests
{
    public ResultPresenterTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    private static ResultOverview Present(params StringConfiguration[] strings) =>
        ResultPresenter.Present(StringCalculator.Calculate(Deye18K(), strings, Parameters()));

    [Fact]
    public void Without_inverter_the_overview_asks_for_one()
    {
        ResultOverview overview = ResultPresenter.Present(null);

        Assert.Equal(ResultState.Neutral, overview.State);
        Assert.Equal("No inverter selected", overview.Headline);
        Assert.Empty(overview.Mppts);
        Assert.Empty(overview.Limits);
    }

    [Fact]
    public void Valid_design_is_ok_and_shows_values_against_the_effective_limits()
    {
        ResultOverview overview = Present(Aiko(1, 11), Aiko(2, 11));

        Assert.Equal(ResultState.Ok, overview.State);
        Assert.Equal("All limits are met", overview.Headline);
        MpptView mppt = overview.Mppts[0];
        Assert.Equal("OK", mppt.StatusText);
        Assert.All(mppt.Rows, r => Assert.Equal(ResultState.Ok, r.State));

        ResultValueRow voc = mppt.Rows[0];
        Assert.Equal("String Voc", voc.Quantity);
        Assert.Equal("≤ 760.0 V", voc.Limit);
        Assert.Equal("Allowed modules per string: 6 … 14", mppt.AllowedRange);
        Assert.Equal("760.0 V", overview.Limits[0].Effective);
        Assert.Equal("800.0 V", overview.Limits[0].Datasheet);
    }

    [Fact]
    public void Too_many_modules_mark_the_voc_row_and_the_voltage_window_as_error()
    {
        // 18 × Voc(−20 °C) of the AIKO module exceeds the effective 760 V.
        ResultOverview overview = Present(Aiko(1, 18), Aiko(2, 11));

        Assert.Equal(ResultState.Error, overview.State);
        Assert.EndsWith("violated", overview.Headline, StringComparison.Ordinal);
        MpptView mppt = overview.Mppts[0];
        Assert.Equal(ResultState.Error, mppt.State);
        Assert.Equal(ResultState.Error, mppt.Rows[0].State);
        Assert.True(mppt.Voltage!.VocExceedsMaxDc);
        Assert.Contains(mppt.Notes, n => n.State == ResultState.Error && n.Text.StartsWith("Max. DC voltage exceeded", StringComparison.Ordinal));
        Assert.Equal(ResultState.Ok, overview.Mppts[1].State);
    }

    [Fact]
    public void Missing_module_is_a_warning_naming_the_mppt()
    {
        ResultOverview overview = Present(Aiko(1, 11), new StringConfiguration { MpptIndex = 2 });

        Assert.Equal(ResultState.Warning, overview.State);
        Assert.Equal("MPPT 2 has no PV module", overview.Headline);
        Assert.Equal("No module", overview.Mppts[1].StatusText);
        Assert.False(overview.Mppts[1].HasRows);
    }

    [Fact]
    public void All_disabled_mppts_give_a_neutral_overview()
    {
        StringConfiguration first = Aiko(1, 11);
        StringConfiguration second = Aiko(2, 11);
        first.IsEnabled = false;
        second.IsEnabled = false;

        ResultOverview overview = Present(first, second);

        Assert.Equal(ResultState.Neutral, overview.State);
        Assert.Equal("All MPPTs are disabled", overview.Headline);
        Assert.Equal("Disabled", overview.Mppts[0].StatusText);
    }

    [Fact]
    public void Invalid_mppt_range_is_reported_as_error()
    {
        Inverter inverter = Deye18K();
        inverter.MpptVoltageRangeV = "n/a";

        ResultOverview overview = ResultPresenter.Present(StringCalculator.Calculate(inverter, [Aiko(1, 11)], Parameters()));

        Assert.Equal(ResultState.Error, overview.State);
        Assert.Equal("Invalid inverter data", overview.Headline);
    }

    [Fact]
    public void Missing_vmpp_coefficient_adds_a_neutral_note()
    {
        MpptView mppt = Present(Aiko(1, 11), Aiko(2, 11)).Mppts[0];

        Assert.Contains(mppt.Notes, n => n.State == ResultState.Neutral && n.Text.Contains("Pmax coefficient", StringComparison.Ordinal));
    }
}
