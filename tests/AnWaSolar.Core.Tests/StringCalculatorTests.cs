using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Worked examples with the datasheet values of the Deye SUN-18K-SG05LP3-EU-SM2 and the AIKO-A500-MAH60Db
/// (as in the shipped master data), Tmin = −20 °C, Tmax = 70 °C (cell temperatures), safety margin 5 %.
/// </summary>
public sealed class StringCalculatorTests
{
    private const double Precision = 1e-3;

    internal static Inverter Deye18K() => new()
    {
        Manufacturer = "Deye",
        Model = "SUN-18K-SG05LP3-EU-SM2",
        MaxDcInputPowerW = 28800,
        MaxDcInputVoltageV = 800,
        StartVoltageV = 160,
        MpptVoltageRangeV = "160-650",
        NominalDcInputVoltageV = 550,
        MaxInputCurrentA = 36,
        MaxShortCircuitCurrentA = 54,
        MpptCount = 2,
        MaxStringsPerMppt = 2,
        RatedAcPowerW = 18000,
        MaxAcApparentPowerVA = 19800,
        RatedAcCurrentA = 26.1,
        MaxAcCurrentA = 28.7,
        AcPhases = 3,
    };

    internal static PVModule AikoA500() => new()
    {
        Manufacturer = "AIKO",
        Model = "AIKO-A500-MAH60Db",
        PmaxW = 500,
        VmppV = 37.9,
        ImppA = 13.21,
        VocV = 45.02,
        IscA = 14.05,
        EfficiencyPct = 22.6,
        PmaxTempCoeffPctPerC = -0.26,
        VocTempCoeffPctPerC = -0.22,
        IscTempCoeffPctPerC = 0.05,
        OperatingTempMinC = -40,
        OperatingTempMaxC = 85,
    };

    internal static CalculationParameters Parameters(double marginPct = 5) => new()
    {
        MinTempC = -20,
        MaxTempC = 70,
        SafetyMarginPct = marginPct,
    };

    internal static StringConfiguration Aiko(int mppt, int modules, int strings = 1) => new()
    {
        MpptIndex = mppt,
        SelectedModule = AikoA500(),
        ModulesPerString = modules,
        ParallelStrings = strings,
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
        Assert.Equal(45.02 * 0.901, t.VocAtTmaxV, Precision);

        // No VMPP coefficient in the datasheet: the PMAX coefficient (−0.26 %/°C) is used.
        Assert.True(t.VmppCoefficientApproximated);
        Assert.Equal(-0.26, t.VmppCoefficientPctPerC);
        Assert.Equal(37.9 * 1.117, t.VmppAtTminV, Precision);
        Assert.Equal(37.9 * 0.883, t.VmppAtTmaxV, Precision);

        Assert.Equal(558.5, t.PmaxAtTminW, Precision);
        Assert.Equal(441.5, t.PmaxAtTmaxW, Precision);
        Assert.Equal(14.05 * 0.9775, t.IscAtTminA, Precision);
        Assert.Equal(14.05 * 1.0225, t.IscAtTmaxA, Precision);

        // With equal PMAX and VMPP coefficients IMPP stays at PMAX / VMPP at 25 °C.
        Assert.Equal(500 / 37.9, t.ImppAtTminA, Precision);
        Assert.Equal(500 / 37.9, t.ImppAtTmaxA, Precision);
    }

    [Fact]
    public void Datasheet_vmpp_coefficient_is_used_when_present()
    {
        PVModule module = AikoA500();
        module.VmppTempCoeffPctPerC = -0.30;

        ModuleTemperatureValues t = StringCalculator.ComputeTemperatureValues(module, Parameters());

        Assert.False(t.VmppCoefficientApproximated);
        Assert.Equal(37.9 * 1.135, t.VmppAtTminV, Precision);
        Assert.Equal(37.9 * 0.865, t.VmppAtTmaxV, Precision);
    }

    [Fact]
    public void Open_circuit_voltage_check_does_not_depend_on_the_vmpp_coefficient()
    {
        PVModule module = AikoA500();
        module.VmppTempCoeffPctPerC = -0.10;
        var configuration = new StringConfiguration { MpptIndex = 1, SelectedModule = module, ModulesPerString = 16 };

        MpptResult mppt = StringCalculator.Calculate(Deye18K(), [configuration], Parameters()).Mppts[0];

        Assert.Equal(16 * 49.47698, Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MaxDcVoltage).Actual, Precision);
    }

    [Fact]
    public void Allowed_module_range_is_derived_from_voltage_limits()
    {
        MpptResult mppt = CalculateSingle(modules: 11);

        // Min: ceil(168 V / 33.466 V) = 6. Max: min(floor(760 V / 49.477 V) = 15, floor(617.5 V / 42.334 V) = 14) = 14.
        Assert.Equal(6, mppt.MinModulesPerString);
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
        Assert.Equal(4 * 37.9 * 0.883, lower.Actual, Precision);
        Assert.Equal(6, lower.Recommendation);
        Assert.Equal(6, Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.StartVoltage).Recommendation);
    }

    [Fact]
    public void Too_many_parallel_strings_exceed_strings_and_input_current()
    {
        MpptResult mppt = CalculateSingle(modules: 11, strings: 3);

        LimitViolation strings = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.MaxStringsPerMppt);
        Assert.Equal(3, strings.Actual);
        Assert.Equal(2, strings.Limit);

        // IMPP per string: 500 W / 37.9 V = 13.193 A (equal at both temperatures).
        LimitViolation current = Assert.Single(mppt.Violations, v => v.Kind == ViolationKind.InputCurrent);
        Assert.Equal(3 * 500 / 37.9, current.Actual, Precision);
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
        Assert.Contains("MPPT 2: Please select a PV module.", CalculationTextFormatter.GetMessages(result));
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
        Inverter inverter = Deye18K();
        inverter.MpptVoltageRangeV = "650-160";

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
