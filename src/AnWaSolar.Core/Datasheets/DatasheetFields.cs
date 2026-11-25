namespace AnWaSolar.Core.Datasheets;

/// <summary>Kind of a datasheet.</summary>
public enum DatasheetKind
{
    Module,
    Inverter,
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

    public static IReadOnlyList<DatasheetField> Module { get; } =
    [
        new(Manufacturer, "Manufacturer", string.Empty, DatasheetValueType.Text, true),
        new(Model, "Model", string.Empty, DatasheetValueType.Text, true, "The power class the values belong to, for example TSM-445-NEG9R.28."),
        new(PmaxW, "Pmax", "W", DatasheetValueType.Number, true, "Rated power at STC."),
        new(VmppV, "Vmpp", "V", DatasheetValueType.Number, true, "Voltage at the maximum power point, STC."),
        new(ImppA, "Impp", "A", DatasheetValueType.Number, true, "Current at the maximum power point, STC."),
        new(VocV, "Voc", "V", DatasheetValueType.Number, true, "Open-circuit voltage, STC."),
        new(IscA, "Isc", "A", DatasheetValueType.Number, true, "Short-circuit current, STC."),
        new(EfficiencyPct, "Module efficiency", "%", DatasheetValueType.Number, true),
        new(PmaxCoeff, "Temperature coefficient of Pmax", "%/°C", DatasheetValueType.Number, true, "Negative, for example -0.30."),
        new(VocCoeff, "Temperature coefficient of Voc", "%/°C", DatasheetValueType.Number, true, "Negative, for example -0.24. Decides the voltage when cold."),
        new(VmppCoeff, "Temperature coefficient of Vmpp", "%/°C", DatasheetValueType.Number, false, "Optional; most datasheets do not state it. Without it the Pmax coefficient is used."),
        new(IscCoeff, "Temperature coefficient of Isc", "%/°C", DatasheetValueType.Number, true),
        new(MaxSeriesFuseA, "Max. series fuse rating", "A", DatasheetValueType.Number, false, "Reverse current rating of the module; decides whether string fuses are needed."),
        new(OperatingMinC, "Operating temperature min", "°C", DatasheetValueType.Number, true),
        new(OperatingMaxC, "Operating temperature max", "°C", DatasheetValueType.Number, true),
    ];

    public static IReadOnlyList<DatasheetField> Inverter { get; } =
    [
        new(Manufacturer, "Manufacturer", string.Empty, DatasheetValueType.Text, true),
        new(Model, "Model", string.Empty, DatasheetValueType.Text, true),
        new(MaxDcPowerW, "Max. DC input power", "W", DatasheetValueType.Number, true, "Max. DC input power of the inverter, not the max. PV access power."),
        new(MaxDcVoltageV, "Max. DC input voltage", "V", DatasheetValueType.Number, true),
        new(StartVoltageV, "Start voltage", "V", DatasheetValueType.Number, true),
        new(MpptRangeV, "MPPT voltage range", "V", DatasheetValueType.Range, true, "Lower and upper bound, for example 160-650."),
        new(NominalDcVoltageV, "Nominal DC input voltage", "V", DatasheetValueType.Number, false),
        new(MaxInputCurrentA, "Max. input current per MPPT", "A", DatasheetValueType.Number, true, "Per MPPT: for \"36+36\" enter 36."),
        new(MaxShortCircuitCurrentA, "Max. short-circuit current per MPPT", "A", DatasheetValueType.Number, true, "Per MPPT: for \"54+54\" enter 54."),
        new(MpptCount, "MPPT count", string.Empty, DatasheetValueType.Integer, true),
        new(MaxStringsPerMppt, "Max. strings per MPPT", string.Empty, DatasheetValueType.Integer, false, "Leave empty when the datasheet does not state it."),
        new(RatedAcPowerW, "Rated AC output power", "W", DatasheetValueType.Number, false, "Rated active power on the AC side."),
        new(MaxAcApparentPowerVA, "Max. AC apparent power", "VA", DatasheetValueType.Number, false),
        new(RatedAcCurrentA, "Rated AC output current", "A", DatasheetValueType.Number, false, "Per phase at 230/400 V: for \"27.3/26.1\" (220/380 V / 230/400 V) enter 26.1."),
        new(MaxAcCurrentA, "Max. AC output current", "A", DatasheetValueType.Number, false, "Per phase at 230/400 V."),
        new(AcPhases, "AC phases", string.Empty, DatasheetValueType.Integer, false, "1 or 3."),
    ];

    public static IReadOnlyList<DatasheetField> For(DatasheetKind kind) => kind == DatasheetKind.Module ? Module : Inverter;
}
