using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Worked examples with the datasheet values of the Deye SUN-18K-SG05LP3-EU-SM2 and the AIKO-A500-MAH60Db
/// (as in the shipped master data), Tmin = −20 °C, Tmax = 40 °C, safety margin 5 %.
/// </summary>
public sealed class StringCalculatorTests
{
    private const double Precision = 1e-3;

    internal static Wechselrichter Deye18K() => new()
    {
        Hersteller = "Deye",
        Model = "SUN-18K-SG05LP3-EU-SM2",
        MaxDcEingangsleistungW = 28800,
        MaxDcEingangsspannungV = 800,
        StartspannungV = 160,
        MpptSpannungsbereichV = "160-650",
        NennDcEingangsspannungV = 550,
        MaxBetriebsPvEingangsstromA = 36,
        MaxEingangsKurzschlussstromA = 54,
        AnzahlDerMpptTrackers = 2,
        MaxStringsProMppt = 2,
    };

    internal static PVModule AikoA500() => new()
    {
        Hersteller = "AIKO",
        Model = "AIKO-A500-MAH60Db",
        NominalleistungPmaxWp = 500,
        SpannungImMppUmppV = 37.9,
        StromImMppImppA = 13.21,
        LeerlaufspannungUocV = 45.02,
        KurzschlusstromIscA = 14.05,
        ModulwirkungsgradEtaMProzent = 22.6,
        TemperaturkoeffPmaxProzentProGradC = -0.26,
        TemperaturkoeffVocProzentProGradC = -0.22,
        TemperaturkoeffIscProzentProGradC = -0.05,
        BetriebstemperaturMinC = -40,
        BetriebstemperaturMaxC = 85,
    };

    internal static CalculationParameters Parameters(double marginPct = 5) => new()
    {
        MinTempC = -20,
        MaxTempC = 40,
        SicherheitsmargePct = marginPct,
    };

    internal static StringConfiguration Aiko(int mppt, int modules, int strings = 1) => new()
    {
        MpptIndex = mppt,
        SelectedModule = AikoA500(),
        ModuleProString = modules,
        ParalleleStrings = strings,
    };

    private static MpptResult CalculateSingle(int modules, int strings = 1)
    {
        StringCalculationResult result = StringCalculator.Calculate(
            Deye18K(), [Aiko(1, modules, strings), Aiko(2, 11)], Parameters());
        return result.Mppts[0];
    }

    [Fact]
    public void Safety_margin_reduces_maximum_and_raises_minimum_limits()
    {
        EffectiveLimits? limits = StringCalculator.ComputeLimits(Deye18K(), Parameters());

        Assert.NotNull(limits);
        Assert.Equal(760.0, limits.MaxDcVoltageV, Precision);
        Assert.Equal(168.0, limits.StartVoltageV, Precision);
        Assert.Equal(168.0, limits.MpptLowerBoundV, Precision);
        Assert.Equal(617.5, limits.MpptUpperBoundV, Precision);
        Assert.Equal(34.2, limits.MaxInputCurrentA, Precision);
        Assert.Equal(51.3, limits.MaxShortCircuitCurrentA, Precision);
        Assert.Equal(14400.0, limits.DcPowerPerMpptW, Precision);
        Assert.Equal(13680.0, limits.DcPowerPerMpptLimitW, Precision);
    }

    [Fact]
    public void Without_safety_margin_the_datasheet_limits_apply()
    {
        EffectiveLimits? limits = StringCalculator.ComputeLimits(Deye18K(), Parameters(marginPct: 0));

        Assert.NotNull(limits);
        Assert.Equal(800.0, limits.MaxDcVoltageV, Precision);
        Assert.Equal(160.0, limits.MpptLowerBoundV, Precision);
        Assert.Equal(650.0, limits.MpptUpperBoundV, Precision);
    }

    [Fact]
    public void Temperature_values_follow_the_linear_coefficients()
    {
        ModuleTemperatureValues t = StringCalculator.ComputeTemperatureValues(AikoA500(), Parameters());

        // VOC(Tmin) = 45.02 V · (1 + (−0.22 %/°C) · (−20 °C − 25 °C)) = 45.02 V · 1.099
        Assert.Equal(49.47698, t.VocAtTminV, Precision);
        Assert.Equal(45.02 * 0.967, t.VocAtTmaxV, Precision);
        Assert.Equal(37.9 * 1.099, t.VmppAtTminV, Precision);
        Assert.Equal(37.9 * 0.967, t.VmppAtTmaxV, Precision);
        Assert.Equal(558.5, t.PmaxAtTminW, Precision);
        Assert.Equal(480.5, t.PmaxAtTmaxW, Precision);
        Assert.Equal(14.05 * 1.0225, t.IscAtTminA, Precision);
        Assert.Equal(14.05 * 0.9925, t.IscAtTmaxA, Precision);
        Assert.Equal(558.5 / (37.9 * 1.099), t.ImppAtTminA, Precision);
        Assert.Equal(480.5 / (37.9 * 0.967), t.ImppAtTmaxA, Precision);
    }

    [Fact]
    public void Allowed_module_range_is_derived_from_voltage_limits()
    {
        MpptResult mppt = CalculateSingle(modules: 11);

        // Min: ceil(168 V / 36.649 V) = 5. Max: min(floor(760 V / 49.477 V) = 15, floor(617.5 V / 41.652 V) = 14) = 14.
        Assert.Equal(5, mppt.MinModulesPerString);
        Assert.Equal(14, mppt.MaxModulesPerString);
        Assert.True(mppt.HasValidModuleRange);
    }

    [Fact]
    public void Valid_configuration_has_no_violations()
    {
        StringCalculationResult result = StringCalculator.Calculate(
            Deye18K(), [Aiko(1, 11), Aiko(2, 10)], Parameters());

        Assert.True(result.IsOk);
        MpptResult mppt = result.Mppts[0];
        Assert.Equal(MpptStatus.Calculated, mppt.Status);
        Assert.Empty(mppt.Violations);
        Assert.Equal(11 * 49.47698, mppt.StringVocMaxV, Precision);
        Assert.Equal(11 * 558.5, mppt.ArrayPowerAtTminW, Precision);
    }

    [Fact]
    public void Too_many_modules_exceed_the_maximum_dc_voltage()
    {
        MpptResult mppt = CalculateSingle(modules: 16);

        LimitViolation voltage = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MaxDcVoltage);
        Assert.Equal(16 * 49.47698, voltage.Actual, Precision);
        Assert.Equal(760.0, voltage.Limit, Precision);
        Assert.Equal(15, voltage.Recommendation);

        LimitViolation mpptUpper = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MpptUpperBound);
        Assert.Equal(14, mpptUpper.Recommendation);
    }

    [Fact]
    public void Fifteen_modules_stay_below_the_dc_voltage_but_exceed_the_mppt_range()
    {
        MpptResult mppt = CalculateSingle(modules: 15);

        Assert.DoesNotContain(mppt.Violations, v => v.Kind == ViolationKind.MaxDcVoltage);
        Assert.Contains(mppt.Violations, v => v.Kind == ViolationKind.MpptUpperBound);
    }

    [Fact]
    public void Too_few_modules_miss_the_mppt_lower_bound_and_the_start_voltage()
    {
        MpptResult mppt = CalculateSingle(modules: 4);

        LimitViolation lower = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MpptLowerBound);
        Assert.Equal(4 * 37.9 * 0.967, lower.Actual, Precision);
        Assert.Equal(5, lower.Recommendation);
        Assert.Equal(5, Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.StartVoltage).Recommendation);
    }

    [Fact]
    public void Too_many_parallel_strings_exceed_strings_and_input_current()
    {
        MpptResult mppt = CalculateSingle(modules: 11, strings: 3);

        LimitViolation strings = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MaxStringsPerMppt);
        Assert.Equal(3, strings.Actual);
        Assert.Equal(2, strings.Limit);

        // The higher IMPP of both temperatures is used: 558.5 W / 41.652 V = 13.409 A per string.
        LimitViolation current = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.InputCurrent);
        Assert.Equal(3 * 558.5 / (37.9 * 1.099), current.Actual, Precision);
        Assert.Equal(2, current.Recommendation);

        Assert.DoesNotContain(mppt.Violations, v => v.Kind == ViolationKind.ShortCircuitCurrent);
    }

    [Fact]
    public void Too_much_power_per_mppt_is_reported()
    {
        MpptResult mppt = CalculateSingle(modules: 14, strings: 2);

        LimitViolation power = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.DcPower);
        Assert.Equal(2 * 14 * 558.5, power.Actual, Precision);
        Assert.Equal(13680.0, power.Limit, Precision);
        Assert.Equal(1, power.Recommendation);
    }

    [Fact]
    public void Disabled_mppt_is_skipped_and_does_not_affect_the_result()
    {
        StringConfiguration disabled = Aiko(2, 30, 5);
        disabled.IsEnabled = false;

        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), disabled], Parameters());

        Assert.Equal(MpptStatus.Disabled, result.Mppts[1].Status);
        Assert.Empty(result.Mppts[1].Violations);
        Assert.True(result.IsOk);
    }

    [Fact]
    public void Missing_module_makes_the_result_not_ok()
    {
        var empty = new StringConfiguration { MpptIndex = 2 };

        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), empty], Parameters());

        Assert.Equal(MpptStatus.NoModule, result.Mppts[1].Status);
        Assert.False(result.IsOk);
        Assert.Contains("MPPT 2: Bitte PV-Modul auswählen.", CalculationTextFormatter.GetMessages(result));
    }

    [Fact]
    public void Every_mppt_of_the_inverter_gets_a_result()
    {
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11)], Parameters());

        Assert.Equal(2, result.Mppts.Count);
        Assert.Equal(MpptStatus.MissingConfiguration, result.Mppts[1].Status);
    }

    [Fact]
    public void Invalid_mppt_range_stops_the_calculation()
    {
        Wechselrichter inverter = Deye18K();
        inverter.MpptSpannungsbereichV = "650-160";

        StringCalculationResult result = StringCalculator.Calculate(inverter, [Aiko(1, 11)], Parameters());

        Assert.False(result.HasValidMpptRange);
        Assert.False(result.IsOk);
        Assert.Empty(result.Mppts);
    }

    [Theory]
    [InlineData("160-650", true, 160, 650)]
    [InlineData(" 120 – 1000 ", true, 120, 1000)]
    [InlineData("200—800", true, 200, 800)]
    [InlineData("650-160", false, 0, 0)]
    [InlineData("0-500", false, 0, 0)]
    [InlineData("160", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void Voltage_range_is_parsed(string? text, bool valid, double min, double max)
    {
        bool parsed = StringCalculator.TryParseVoltageRange(text, out double minV, out double maxV);

        Assert.Equal(valid, parsed);
        if (valid)
        {
            Assert.Equal(min, minV);
            Assert.Equal(max, maxV);
        }
    }
}
