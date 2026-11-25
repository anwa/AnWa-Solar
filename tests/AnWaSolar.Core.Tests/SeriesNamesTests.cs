using AnWaSolar.Core.Models;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>Series names from the model names of real datasheets and the derived inverter type.</summary>
public sealed class SeriesNamesTests
{
    [Theory]
    [InlineData("SUN-SG05LP3-EU-SM2", "SUN-14K-SG05LP3-EU-SM2", "SUN-15K-SG05LP3-EU-SM2", "SUN-20K-SG05LP3-EU-SM2")]
    [InlineData("SUN-G06P3-EU-AM2", "SUN-4K-G06P3-EU-AM2", "SUN-10K-G06P3-EU-AM2", "SUN-15K-G06P3-EU-AM2")]
    [InlineData("TSM-NEG9R.28", "TSM-425-NEG9R.28", "TSM-450-NEG9R.28")]
    [InlineData("TSM-445-NEG9R.28", "TSM-445-NEG9R.28")]
    public void Series_is_the_common_part_of_the_model_names(string expected, params string[] models)
    {
        Assert.Equal(expected, SeriesNames.Suggest(models));
    }

    [Fact]
    public void Without_model_names_there_is_no_suggestion()
    {
        Assert.Null(SeriesNames.Suggest([null, " "]));
    }

    [Fact]
    public void Inverter_type_follows_phases_and_battery_input()
    {
        Inverter deye = Deye18K();
        deye.AcPhases = 3;
        Assert.Equal("Three-phase string inverter", SeriesNames.InverterType(deye));

        deye.BatteryVoltageRangeV = "40-60";
        Assert.Equal("Three-phase hybrid inverter", SeriesNames.InverterType(deye));

        deye.AcPhases = 0;
        Assert.StartsWith("Phases not stated", SeriesNames.InverterType(deye), StringComparison.Ordinal);
    }
}
