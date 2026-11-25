namespace AnWaSolar.Core.Datasheets;

/// <summary>Kind of a datasheet.</summary>
public enum DatasheetKind
{
    Module,
    Inverter,
    Battery,
}

/// <summary>How the text of a field is read and checked.</summary>
public enum DatasheetValueType
{
    Text,
    Number,
    Integer,

    /// <summary>A voltage range such as "160-650".</summary>
    Range,
}

/// <summary>A value of a master data entry that the import dialog shows.</summary>
/// <param name="Key">Identifier, used by the parser and the entry builder.</param>
/// <param name="Label">Label in the dialog.</param>
/// <param name="Unit">Unit shown next to the value, or empty.</param>
/// <param name="Required">True when the entry cannot be saved without the value.</param>
/// <param name="Hint">Explanation shown with the field, or null.</param>
public sealed record DatasheetField(string Key, string Label, string Unit, DatasheetValueType Type, bool Required, string? Hint = null);

/// <summary>The fields of PV modules and inverters, in the order of the import dialog.</summary>
public static class DatasheetFields
{
    public const string Manufacturer = "Manufacturer";
    public const string Model = "Model";

    public const string PmaxW = "PmaxW";
    public const string VmppV = "VmppV";
    public const string ImppA = "ImppA";
    public const string VocV = "VocV";
    public const string IscA = "IscA";
    public const string EfficiencyPct = "EfficiencyPct";
    public const string PmaxCoeff = "PmaxCoeff";
    public const string VocCoeff = "VocCoeff";
    public const string VmppCoeff = "VmppCoeff";
    public const string IscCoeff = "IscCoeff";
    public const string OperatingMinC = "OperatingMinC";
    public const string MaxSeriesFuseA = "MaxSeriesFuseA";
    public const string OperatingMaxC = "OperatingMaxC";
    public const string CableCrossSectionMm2 = "CableCrossSectionMm2";
    public const string CableLengthPlusMm = "CableLengthPlusMm";
    public const string CableLengthMinusMm = "CableLengthMinusMm";

    public const string MaxDcPowerW = "MaxDcPowerW";
    public const string MaxDcVoltageV = "MaxDcVoltageV";
    public const string StartVoltageV = "StartVoltageV";
    public const string MpptRangeV = "MpptRangeV";
    public const string NominalDcVoltageV = "NominalDcVoltageV";
    public const string MaxInputCurrentA = "MaxInputCurrentA";
    public const string MaxShortCircuitCurrentA = "MaxShortCircuitCurrentA";
    public const string MpptCount = "MpptCount";
    public const string MaxStringsPerMppt = "MaxStringsPerMppt";
    public const string RatedAcPowerW = "RatedAcPowerW";
    public const string MaxAcApparentPowerVA = "MaxAcApparentPowerVA";
    public const string RatedAcCurrentA = "RatedAcCurrentA";
    public const string MaxAcCurrentA = "MaxAcCurrentA";
    public const string AcPhases = "AcPhases";
    public const string BatteryRangeV = "BatteryRangeV";
    public const string MaxBatteryChargeA = "MaxBatteryChargeA";
    public const string MaxBatteryDischargeA = "MaxBatteryDischargeA";
    public const string BatteryInputs = "BatteryInputs";

    public const string NominalVoltageV = "NominalVoltageV";
    public const string OperatingRangeV = "OperatingRangeV";
    public const string NominalCapacityAh = "NominalCapacityAh";
    public const string NominalEnergyKWh = "NominalEnergyKWh";
    public const string UsableEnergyKWh = "UsableEnergyKWh";
    public const string DepthOfDischargePct = "DepthOfDischargePct";
    public const string MaxChargeA = "MaxChargeA";
    public const string MaxDischargeA = "MaxDischargeA";
    public const string MaxInSeries = "MaxInSeries";
    public const string MaxInParallel = "MaxInParallel";

    public static IReadOnlyList<DatasheetField> Module { get; } =
    [
        new(Manufacturer, L.T("Manufacturer"), string.Empty, DatasheetValueType.Text, true),
        new(Model, L.T("Model"), string.Empty, DatasheetValueType.Text, true, L.T("The power class the values belong to, for example TSM-445-NEG9R.28.")),
        new(PmaxW, L.T("Pmax"), "W", DatasheetValueType.Number, true, L.T("Rated power at STC.")),
        new(VmppV, L.T("Vmpp"), "V", DatasheetValueType.Number, true, L.T("Voltage at the maximum power point, STC.")),
        new(ImppA, L.T("Impp"), "A", DatasheetValueType.Number, true, L.T("Current at the maximum power point, STC.")),
        new(VocV, L.T("Voc"), "V", DatasheetValueType.Number, true, L.T("Open-circuit voltage, STC.")),
        new(IscA, L.T("Isc"), "A", DatasheetValueType.Number, true, L.T("Short-circuit current, STC.")),
        new(EfficiencyPct, L.T("Module efficiency"), "%", DatasheetValueType.Number, true),
        new(PmaxCoeff, L.T("Temperature coefficient of Pmax"), "%/°C", DatasheetValueType.Number, true, L.T("Negative, for example -0.30.")),
        new(VocCoeff, L.T("Temperature coefficient of Voc"), "%/°C", DatasheetValueType.Number, true, L.T("Negative, for example -0.24. Decides the voltage when cold.")),
        new(VmppCoeff, L.T("Temperature coefficient of Vmpp"), "%/°C", DatasheetValueType.Number, false, L.T("Optional; most datasheets do not state it. Without it the Pmax coefficient is used.")),
        new(IscCoeff, L.T("Temperature coefficient of Isc"), "%/°C", DatasheetValueType.Number, true),
        new(MaxSeriesFuseA, L.T("Max. series fuse rating"), "A", DatasheetValueType.Number, false, L.T("Reverse current rating of the module; decides whether string fuses are needed.")),
        new(OperatingMinC, L.T("Operating temperature min"), "°C", DatasheetValueType.Number, true),
        new(OperatingMaxC, L.T("Operating temperature max"), "°C", DatasheetValueType.Number, true),
        new(CableCrossSectionMm2, L.T("Cable cross-section"), "mm²", DatasheetValueType.Number, false, L.T("Of the module leads, for example 4.")),
        new(CableLengthPlusMm, L.T("Cable length plus"), "mm", DatasheetValueType.Number, false, L.T("Portrait; for \"1100/1100 mm\" enter 1100.")),
        new(CableLengthMinusMm, L.T("Cable length minus"), "mm", DatasheetValueType.Number, false, L.T("Portrait.")),
    ];

    public static IReadOnlyList<DatasheetField> Inverter { get; } =
    [
        new(Manufacturer, L.T("Manufacturer"), string.Empty, DatasheetValueType.Text, true),
        new(Model, L.T("Model"), string.Empty, DatasheetValueType.Text, true),
        new(MaxDcPowerW, L.T("Max. DC input power"), "W", DatasheetValueType.Number, true, L.T("Max. DC input power of the inverter, not the max. PV access power.")),
        new(MaxDcVoltageV, L.T("Max. DC input voltage"), "V", DatasheetValueType.Number, true),
        new(StartVoltageV, L.T("Start voltage"), "V", DatasheetValueType.Number, true),
        new(MpptRangeV, L.T("MPPT voltage range"), "V", DatasheetValueType.Range, true, L.T("Lower and upper bound, for example 160-650.")),
        new(NominalDcVoltageV, L.T("Nominal DC input voltage"), "V", DatasheetValueType.Number, false),
        new(MaxInputCurrentA, L.T("Max. input current per MPPT"), "A", DatasheetValueType.Number, true, L.T("Per MPPT: for \"36+36\" enter 36.")),
        new(MaxShortCircuitCurrentA, L.T("Max. short-circuit current per MPPT"), "A", DatasheetValueType.Number, true, L.T("Per MPPT: for \"54+54\" enter 54.")),
        new(MpptCount, L.T("MPPT count"), string.Empty, DatasheetValueType.Integer, true),
        new(MaxStringsPerMppt, L.T("Max. strings per MPPT"), string.Empty, DatasheetValueType.Integer, false, L.T("Leave empty when the datasheet does not state it.")),
        new(RatedAcPowerW, L.T("Rated AC output power"), "W", DatasheetValueType.Number, false, L.T("Rated active power on the AC side.")),
        new(MaxAcApparentPowerVA, L.T("Max. AC apparent power"), "VA", DatasheetValueType.Number, false),
        new(RatedAcCurrentA, L.T("Rated AC output current"), "A", DatasheetValueType.Number, false, L.T("Per phase at 230/400 V: for \"27.3/26.1\" (220/380 V / 230/400 V) enter 26.1.")),
        new(MaxAcCurrentA, L.T("Max. AC output current"), "A", DatasheetValueType.Number, false, L.T("Per phase at 230/400 V.")),
        new(AcPhases, L.T("AC phases"), string.Empty, DatasheetValueType.Integer, false, L.T("1 or 3.")),
        new(BatteryRangeV, L.T("Battery voltage range"), "V", DatasheetValueType.Range, false, L.T("Hybrid inverters only, for example 40-60. Leave empty without battery input.")),
        new(MaxBatteryChargeA, L.T("Max. battery charge current"), "A", DatasheetValueType.Number, false),
        new(MaxBatteryDischargeA, L.T("Max. battery discharge current"), "A", DatasheetValueType.Number, false),
        new(BatteryInputs, L.T("Battery inputs"), string.Empty, DatasheetValueType.Integer, false),
    ];

    public static IReadOnlyList<DatasheetField> Battery { get; } =
    [
        new(Manufacturer, L.T("Manufacturer"), string.Empty, DatasheetValueType.Text, true),
        new(Model, L.T("Model"), string.Empty, DatasheetValueType.Text, true, L.T("One battery module, for example the module of a high-voltage tower.")),
        new(NominalVoltageV, L.T("Nominal voltage"), "V", DatasheetValueType.Number, true, L.T("Of one module.")),
        new(OperatingRangeV, L.T("Operating voltage range"), "V", DatasheetValueType.Range, true, L.T("Of one module, for example 44.8-57.6.")),
        new(NominalCapacityAh, L.T("Nominal capacity"), L.T("Ah"), DatasheetValueType.Number, false, L.T("Leave empty when the datasheet does not state it.")),
        new(NominalEnergyKWh, L.T("Nominal energy"), "kWh", DatasheetValueType.Number, true, L.T("Of one module.")),
        new(UsableEnergyKWh, L.T("Usable energy"), "kWh", DatasheetValueType.Number, false, L.T("Of one module; leave empty when the datasheet does not state it.")),
        new(DepthOfDischargePct, L.T("Depth of discharge"), "%", DatasheetValueType.Number, false, L.T("Recommended DoD, for example 90.")),
        new(MaxChargeA, L.T("Max. charge current"), "A", DatasheetValueType.Number, true, L.T("Max. continuous charge current of one module.")),
        new(MaxDischargeA, L.T("Max. discharge current"), "A", DatasheetValueType.Number, true, L.T("Max. continuous discharge current of one module.")),
        new(MaxInSeries, L.T("Max. modules in series"), string.Empty, DatasheetValueType.Integer, false, L.T("Leave empty when the datasheet does not state it.")),
        new(MaxInParallel, L.T("Max. modules in parallel"), string.Empty, DatasheetValueType.Integer, false, L.T("Leave empty when the datasheet does not state it.")),
    ];

    public static IReadOnlyList<DatasheetField> For(DatasheetKind kind) => kind switch
    {
        DatasheetKind.Inverter => Inverter,
        DatasheetKind.Battery => Battery,
        _ => Module,
    };
}
