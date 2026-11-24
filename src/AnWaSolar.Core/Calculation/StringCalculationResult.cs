using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>
/// Inverter limits after applying the safety margin. Maximum values are reduced, minimum values are raised.
/// </summary>
/// <param name="MpptMinV">Lower bound of the MPPT voltage range from the datasheet.</param>
/// <param name="MpptMaxV">Upper bound of the MPPT voltage range from the datasheet.</param>
/// <param name="MaxDcVoltageV">Maximum DC input voltage, reduced by the margin.</param>
/// <param name="StartVoltageV">Start voltage, raised by the margin.</param>
/// <param name="MpptLowerBoundV">MPPT lower bound, raised by the margin.</param>
/// <param name="MpptUpperBoundV">MPPT upper bound, reduced by the margin.</param>
/// <param name="MaxInputCurrentA">Maximum operating input current per MPPT, reduced by the margin.</param>
/// <param name="MaxShortCircuitCurrentA">Maximum input short-circuit current per MPPT, reduced by the margin.</param>
/// <param name="DcPowerPerMpptW">Maximum DC input power divided evenly over the MPPTs.</param>
/// <param name="DcPowerPerMpptLimitW">DC power per MPPT, reduced by the margin.</param>
public sealed record EffectiveLimits(
    double MpptMinV,
    double MpptMaxV,
    double MaxDcVoltageV,
    double StartVoltageV,
    double MpptLowerBoundV,
    double MpptUpperBoundV,
    double MaxInputCurrentA,
    double MaxShortCircuitCurrentA,
    double DcPowerPerMpptW,
    double DcPowerPerMpptLimitW);

/// <summary>Module values at the minimum and maximum cell temperature.</summary>
/// <param name="VmppCoefficientPctPerC">VMPP temperature coefficient used for the calculation.</param>
/// <param name="VmppCoefficientApproximated">True when the module has no VMPP coefficient and the PMAX coefficient was used.</param>
public sealed record ModuleTemperatureValues(
    double VocAtTminV,
    double VocAtTmaxV,
    double VmppAtTminV,
    double VmppAtTmaxV,
    double PmaxAtTminW,
    double PmaxAtTmaxW,
    double IscAtTminA,
    double IscAtTmaxA,
    double ImppAtTminA,
    double ImppAtTmaxA,
    double VmppCoefficientPctPerC,
    bool VmppCoefficientApproximated);

/// <summary>Kind of a violated inverter limit.</summary>
public enum ViolationKind
{
    /// <summary>String VOC at Tmin exceeds the maximum DC input voltage (safety-relevant).</summary>
    MaxDcVoltage,

    /// <summary>String VMPP at Tmax is below the MPPT lower bound.</summary>
    MpptLowerBound,

    /// <summary>String VMPP at Tmin is above the MPPT upper bound.</summary>
    MpptUpperBound,

    /// <summary>String VMPP at Tmax is below the start voltage.</summary>
    StartVoltage,

    /// <summary>Total short-circuit current exceeds the inverter limit.</summary>
    ShortCircuitCurrent,

    /// <summary>Total MPP current exceeds the maximum operating input current.</summary>
    InputCurrent,

    /// <summary>More parallel strings than the inverter allows per MPPT.</summary>
    MaxStringsPerMppt,

    /// <summary>DC power at Tmin exceeds the DC power per MPPT.</summary>
    DcPower,
}

/// <summary>
/// A violated limit with the computed value, the effective limit and the recommended module or string count.
/// </summary>
/// <param name="Kind">Violated limit.</param>
/// <param name="Actual">Computed value of the configuration.</param>
/// <param name="Limit">Effective limit (with safety margin).</param>
/// <param name="Recommendation">
/// Recommended count: maximum or minimum modules per string for voltage limits, maximum parallel strings for current and
/// power limits, the inverter's maximum for <see cref="ViolationKind.MaxStringsPerMppt"/>.
/// </param>
public sealed record LimitViolation(ViolationKind Kind, double Actual, double Limit, int Recommendation);

/// <summary>State of one MPPT in a calculation.</summary>
public enum MpptStatus
{
    /// <summary>No string configuration exists for this MPPT.</summary>
    MissingConfiguration,

    /// <summary>The string is disabled and skipped.</summary>
    Disabled,

    /// <summary>No PV module is selected.</summary>
    NoModule,

    /// <summary>The string was calculated.</summary>
    Calculated,
}

/// <summary>Calculation result of one MPPT.</summary>
public sealed class MpptResult
{
    /// <summary>MPPT number, starting at 1.</summary>
    public required int MpptIndex { get; init; }

    public required MpptStatus Status { get; init; }

    public StringConfiguration? Configuration { get; init; }

    public PVModule? Module { get; init; }

    public int ModulesPerString { get; init; }

    public int ParallelStrings { get; init; }

    public ModuleTemperatureValues? Temperatures { get; init; }

    public double StringVocMinV { get; init; }

    public double StringVocMaxV { get; init; }

    public double StringVmppMinV { get; init; }

    public double StringVmppMaxV { get; init; }

    public double StringIscMinA { get; init; }

    public double StringIscMaxA { get; init; }

    public double StringImppMinA { get; init; }

    public double StringImppMaxA { get; init; }

    public double ArrayIscMinA { get; init; }

    public double ArrayIscMaxA { get; init; }

    public double ArrayImppMinA { get; init; }

    public double ArrayImppMaxA { get; init; }

    /// <summary>Estimated DC power of all strings of this MPPT at Tmin (highest power).</summary>
    public double ArrayPowerAtTminW { get; init; }

    /// <summary>Lowest allowed module count per string (MPPT lower bound and start voltage).</summary>
    public int MinModulesPerString { get; init; }

    /// <summary>Highest allowed module count per string (maximum DC voltage and MPPT upper bound).</summary>
    public int MaxModulesPerString { get; init; }

    public bool HasValidModuleRange => MinModulesPerString <= MaxModulesPerString;

    public IReadOnlyList<LimitViolation> Violations { get; init; } = [];

    /// <summary>True when this MPPT does not prevent an overall OK result.</summary>
    public bool IsOk => Status switch
    {
        MpptStatus.NoModule => false,
        MpptStatus.Calculated => Violations.Count == 0,
        _ => true,
    };
}

/// <summary>Calculation result of all MPPTs of an inverter.</summary>
public sealed class StringCalculationResult
{
    public required Wechselrichter Inverter { get; init; }

    public required CalculationParameters Parameters { get; init; }

    /// <summary>False when the MPPT voltage range of the inverter cannot be parsed; then nothing else is calculated.</summary>
    public bool HasValidMpptRange => Limits is not null;

    public EffectiveLimits? Limits { get; init; }

    public IReadOnlyList<MpptResult> Mppts { get; init; } = [];

    public bool IsOk => HasValidMpptRange && Mppts.All(m => m.IsOk);
}
