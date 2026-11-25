using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Entries from the import dialog, with the values of the Trina TSM-445-NEG9R.28, the Deye SUN-18K-SG05LP3-EU-SM2 and
/// the Felicity FLA48314-EU battery.
/// </summary>
public sealed class DatasheetEntryBuilderTests
{
    internal static Dictionary<string, string?> Trina445() => new()
    {
        [DatasheetFields.Manufacturer] = "Trina",
        [DatasheetFields.Model] = "TSM-445-NEG9R.28",
        [DatasheetFields.PmaxW] = "445",
        [DatasheetFields.VmppV] = "44,3",
        [DatasheetFields.ImppA] = "10.05",
        [DatasheetFields.VocV] = "52.6",
        [DatasheetFields.IscA] = "10.71",
        [DatasheetFields.EfficiencyPct] = "22.3",
        [DatasheetFields.PmaxCoeff] = "-0.30",
        [DatasheetFields.VocCoeff] = "–0,24",
        [DatasheetFields.VmppCoeff] = "",
        [DatasheetFields.IscCoeff] = "0.04",
        [DatasheetFields.OperatingMinC] = "-40",
        [DatasheetFields.OperatingMaxC] = "85",
    };

    internal static Dictionary<string, string?> Deye18K() => new()
    {
        [DatasheetFields.Manufacturer] = "Deye",
        [DatasheetFields.Model] = "SUN-18K-SG05LP3-EU-SM2",
        [DatasheetFields.MaxDcPowerW] = "28800",
        [DatasheetFields.MaxDcVoltageV] = "800",
        [DatasheetFields.StartVoltageV] = "160",
        [DatasheetFields.MpptRangeV] = "160 – 650",
        [DatasheetFields.NominalDcVoltageV] = "550",
        [DatasheetFields.MaxInputCurrentA] = "36",
        [DatasheetFields.MaxShortCircuitCurrentA] = "54",
        [DatasheetFields.MpptCount] = "2",
        [DatasheetFields.MaxStringsPerMppt] = "2",
    };

    [Fact]
    public void Module_is_built_from_texts_with_decimal_commas_and_dashes()
    {
        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildModule(Trina445(), out PVModule? module);

        Assert.Empty(errors);
        Assert.NotNull(module);
        Assert.Equal(44.3, module.VmppV, 6);
        Assert.Equal(-0.24, module.VocTempCoeffPctPerC, 6);
        Assert.Null(module.VmppTempCoeffPctPerC);
        Assert.Equal(-40, module.OperatingTempMinC);
    }

    [Fact]
    public void Positive_voc_coefficient_is_rejected()
    {
        // A minus sign lost while reading the datasheet would make the cold string voltage too low.
        Dictionary<string, string?> values = Trina445();
        values[DatasheetFields.VocCoeff] = "0.24";

        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildModule(values, out PVModule? module);

        Assert.Null(module);
        Assert.Contains(errors, e => e.Contains("coefficient of Voc must be negative", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_and_invalid_values_are_listed()
    {
        Dictionary<string, string?> values = Trina445();
        values[DatasheetFields.IscA] = "";
        values[DatasheetFields.VocV] = "abc";

        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildModule(values, out _);

        Assert.Contains("Isc is missing.", errors);
        Assert.Contains("Voc \"abc\" is not a number.", errors);
    }

    [Fact]
    public void Inverter_is_built_and_range_is_normalised()
    {
        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildInverter(Deye18K(), out Inverter? inverter);

        Assert.Empty(errors);
        Assert.NotNull(inverter);
        Assert.Equal("160-650", inverter.MpptVoltageRangeV);
        Assert.Equal(2, inverter.MaxStringsPerMppt);
    }

    [Fact]
    public void Mppt_range_above_the_max_dc_voltage_is_rejected()
    {
        Dictionary<string, string?> values = Deye18K();
        values[DatasheetFields.MpptRangeV] = "160-950";

        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildInverter(values, out Inverter? inverter);

        Assert.Null(inverter);
        Assert.Contains(errors, e => e.Contains("MPPT voltage range must lie below", StringComparison.Ordinal));
    }

    internal static Dictionary<string, string?> Felicity() => new()
    {
        [DatasheetFields.Manufacturer] = "Felicity",
        [DatasheetFields.Model] = "FLA48314-EU",
        [DatasheetFields.NominalVoltageV] = "51.2",
        [DatasheetFields.OperatingRangeV] = "44.8-57.6",
        [DatasheetFields.NominalCapacityAh] = "",
        [DatasheetFields.NominalEnergyKWh] = "16",
        [DatasheetFields.DepthOfDischargePct] = "95",
        [DatasheetFields.MaxChargeA] = "160",
        [DatasheetFields.MaxDischargeA] = "160",
        [DatasheetFields.MaxInParallel] = "15",
    };

    [Fact]
    public void Battery_is_built_without_the_optional_capacity()
    {
        IReadOnlyList<string> errors = DatasheetEntryBuilder.TryBuildBattery(Felicity(), out Battery? battery);

        Assert.Empty(errors);
        Assert.Null(battery!.NominalCapacityAh);
        Assert.Equal(15, battery.MaxModulesInParallel);
        Assert.Equal(0, battery.MaxModulesInSeries);
        Assert.Equal(15.2, battery.UsableEnergyPerModuleKWh!.Value, 6);
    }

    [Fact]
    public void Battery_with_nominal_voltage_outside_its_range_is_rejected()
    {
        Dictionary<string, string?> values = Felicity();
        values[DatasheetFields.NominalVoltageV] = "60";

        Assert.Contains(DatasheetEntryBuilder.TryBuildBattery(values, out Battery? battery), e => e.Contains("nominal voltage must lie within", StringComparison.Ordinal));
        Assert.Null(battery);
    }

    [Fact]
    public void Inverter_battery_input_needs_range_and_currents()
    {
        Dictionary<string, string?> values = Deye18K();
        values[DatasheetFields.BatteryRangeV] = "40-60";
        values[DatasheetFields.MaxBatteryChargeA] = "330";

        Assert.Contains(DatasheetEntryBuilder.TryBuildInverter(values, out _), e => e.Contains("battery charge and discharge current", StringComparison.Ordinal));

        values[DatasheetFields.MaxBatteryDischargeA] = "330";
        values[DatasheetFields.BatteryInputs] = "2";
        Assert.Empty(DatasheetEntryBuilder.TryBuildInverter(values, out Inverter? inverter));
        Assert.True(inverter!.HasBatteryInput);
        Assert.Equal(330, inverter.MaxBatteryDischargeCurrentA);
    }

    [Fact]
    public void Optional_inverter_values_may_be_empty()
    {
        Dictionary<string, string?> values = Deye18K();
        values[DatasheetFields.NominalDcVoltageV] = null;
        values[DatasheetFields.MaxStringsPerMppt] = " ";

        Assert.Empty(DatasheetEntryBuilder.TryBuildInverter(values, out Inverter? inverter));
        Assert.Equal(0, inverter!.MaxStringsPerMppt);
    }
}
