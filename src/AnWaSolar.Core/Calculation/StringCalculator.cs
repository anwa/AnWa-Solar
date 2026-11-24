using System.Globalization;

using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>
/// Checks PV string configurations against the limits of an inverter over the configured temperature range.
/// This is the only owner of the string calculation; the live results and the report both use its result.
/// </summary>
public static class StringCalculator
{
    // Tolerance for limit comparisons, so that values exactly at a limit are not reported as violations.
    private const double Tolerance = 1e-6;

    /// <summary>Calculates all MPPTs of <paramref name="inverter"/>.</summary>
    /// <param name="inverter">Selected inverter.</param>
    /// <param name="configurations">String configurations; the item at index i belongs to MPPT i + 1.</param>
    /// <param name="parameters">Temperature range and safety margin.</param>
    public static StringCalculationResult Calculate(
        Inverter inverter,
        IReadOnlyList<StringConfiguration> configurations,
        CalculationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentNullException.ThrowIfNull(parameters);

        EffectiveLimits? limits = ComputeLimits(inverter, parameters);
        if (limits is null)
        {
            return new StringCalculationResult { Inverter = inverter, Parameters = parameters };
        }

        int mpptCount = GetMpptCount(inverter);
        var mppts = new List<MpptResult>(mpptCount);
        for (int i = 0; i < mpptCount; i++)
        {
            StringConfiguration? configuration = i < configurations.Count ? configurations[i] : null;
            mppts.Add(CalculateMppt(i + 1, configuration, inverter, limits, parameters));
        }

        return new StringCalculationResult
        {
            Inverter = inverter,
            Parameters = parameters,
            Limits = limits,
            Mppts = mppts,
            Ac = AcCalculator.Calculate(inverter, mppts, parameters),
        };
    }

    /// <summary>Number of MPPT sections shown for an inverter (at least one).</summary>
    public static int GetMpptCount(Inverter inverter) => Math.Max(1, inverter.MpptCount);

    /// <summary>Applies the safety margin to the inverter limits; null when the MPPT range is invalid.</summary>
    public static EffectiveLimits? ComputeLimits(Inverter inverter, CalculationParameters parameters)
    {
        if (!TryParseVoltageRange(inverter.MpptVoltageRangeV, out double mpptMin, out double mpptMax))
        {
            return null;
        }

        double margin = parameters.SafetyMarginPct / 100.0;
        double dcPowerPerMppt = inverter.MaxDcInputPowerW / (double)GetMpptCount(inverter);

        return new EffectiveLimits(
            MpptMinV: mpptMin,
            MpptMaxV: mpptMax,
            MaxDcVoltageV: inverter.MaxDcInputVoltageV * (1 - margin),
            StartVoltageV: inverter.StartVoltageV * (1 + margin),
            MpptLowerBoundV: mpptMin * (1 + margin),
            MpptUpperBoundV: mpptMax * (1 - margin),
            MaxInputCurrentA: inverter.MaxInputCurrentA * (1 - margin),
            MaxShortCircuitCurrentA: inverter.MaxShortCircuitCurrentA * (1 - margin),
            DcPowerPerMpptW: dcPowerPerMppt,
            DcPowerPerMpptLimitW: dcPowerPerMppt * (1 - margin));
    }

    /// <summary>
    /// Applies a linear temperature coefficient: x(T) = x(25 °C) · (1 + α[%/°C] · (T − 25 °C) / 100).
    /// </summary>
    public static double ApplyTemperatureCoefficient(double valueAt25C, double coefficientPctPerC, double temperatureC)
        => valueAt25C * (1.0 + (coefficientPctPerC / 100.0 * (temperatureC - 25.0)));

    /// <summary>Parses a voltage range such as "160-650" (hyphen, en dash or em dash, spaces allowed).</summary>
    public static bool TryParseVoltageRange(string? range, out double minV, out double maxV)
    {
        minV = maxV = 0;
        if (string.IsNullOrWhiteSpace(range))
        {
            return false;
        }

        string[] parts = range.Replace(" ", string.Empty, StringComparison.Ordinal).Split('-', '–', '—');
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out minV)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out maxV)
            && minV > 0
            && maxV > minV;
    }

    /// <summary>Module values at Tmin and Tmax.</summary>
    /// <remarks>
    /// Temperatures are module (cell) temperatures. VMPP uses the module's VMPP coefficient; without one, the PMAX
    /// coefficient is used as a conservative approximation (VMPP changes more strongly than VOC). IMPP is derived from
    /// PMAX / VMPP at the respective temperature.
    /// </remarks>
    public static ModuleTemperatureValues ComputeTemperatureValues(PVModule module, CalculationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(parameters);

        double tMin = parameters.MinTempC;
        double tMax = parameters.MaxTempC;

        bool vmppApproximated = module.VmppTempCoeffPctPerC is null;
        double vmppCoefficient = module.VmppTempCoeffPctPerC ?? module.PmaxTempCoeffPctPerC;
        double vmppAtTmin = ApplyTemperatureCoefficient(module.VmppV, vmppCoefficient, tMin);
        double vmppAtTmax = ApplyTemperatureCoefficient(module.VmppV, vmppCoefficient, tMax);
        double pmaxAtTmin = ApplyTemperatureCoefficient(module.PmaxW, module.PmaxTempCoeffPctPerC, tMin);
        double pmaxAtTmax = ApplyTemperatureCoefficient(module.PmaxW, module.PmaxTempCoeffPctPerC, tMax);

        return new ModuleTemperatureValues(
            VocAtTminV: ApplyTemperatureCoefficient(module.VocV, module.VocTempCoeffPctPerC, tMin),
            VocAtTmaxV: ApplyTemperatureCoefficient(module.VocV, module.VocTempCoeffPctPerC, tMax),
            VmppAtTminV: vmppAtTmin,
            VmppAtTmaxV: vmppAtTmax,
            PmaxAtTminW: pmaxAtTmin,
            PmaxAtTmaxW: pmaxAtTmax,
            IscAtTminA: ApplyTemperatureCoefficient(module.IscA, module.IscTempCoeffPctPerC, tMin),
            IscAtTmaxA: ApplyTemperatureCoefficient(module.IscA, module.IscTempCoeffPctPerC, tMax),
            ImppAtTminA: pmaxAtTmin / Math.Max(0.1, vmppAtTmin),
            ImppAtTmaxA: pmaxAtTmax / Math.Max(0.1, vmppAtTmax),
            VmppCoefficientPctPerC: vmppCoefficient,
            VmppCoefficientApproximated: vmppApproximated);
    }

    private static MpptResult CalculateMppt(
        int mpptIndex,
        StringConfiguration? configuration,
        Inverter inverter,
        EffectiveLimits limits,
        CalculationParameters parameters)
    {
        if (configuration is null)
        {
            return new MpptResult { MpptIndex = mpptIndex, Status = MpptStatus.MissingConfiguration };
        }

        if (!configuration.IsEnabled)
        {
            return new MpptResult { MpptIndex = mpptIndex, Status = MpptStatus.Disabled, Configuration = configuration };
        }

        if (configuration.SelectedModule is null)
        {
            return new MpptResult { MpptIndex = mpptIndex, Status = MpptStatus.NoModule, Configuration = configuration };
        }

        PVModule module = configuration.SelectedModule;
        int modules = Math.Max(1, configuration.ModulesPerString);
        int strings = Math.Max(1, configuration.ParallelStrings);
        ModuleTemperatureValues t = ComputeTemperatureValues(module, parameters);

        double stringIscMax = Math.Max(t.IscAtTminA, t.IscAtTmaxA);
        double stringImppMax = Math.Max(t.ImppAtTminA, t.ImppAtTmaxA);
        double stringPowerAtTmin = modules * t.PmaxAtTminW;
        double arrayPowerAtTmin = strings * stringPowerAtTmin;

        var violations = new List<LimitViolation>();

        // Safety-relevant: the open-circuit voltage is highest at the lowest temperature.
        double stringVocAtTmin = modules * t.VocAtTminV;
        if (stringVocAtTmin > limits.MaxDcVoltageV + Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.MaxDcVoltage, stringVocAtTmin, limits.MaxDcVoltageV,
                Math.Max(0, (int)Math.Floor(limits.MaxDcVoltageV / t.VocAtTminV))));
        }

        double stringVmppAtTmax = modules * t.VmppAtTmaxV;
        double stringVmppAtTmin = modules * t.VmppAtTminV;
        if (stringVmppAtTmax < limits.MpptLowerBoundV - Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.MpptLowerBound, stringVmppAtTmax, limits.MpptLowerBoundV,
                Math.Max(1, (int)Math.Ceiling(limits.MpptLowerBoundV / t.VmppAtTmaxV))));
        }

        if (stringVmppAtTmin > limits.MpptUpperBoundV + Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.MpptUpperBound, stringVmppAtTmin, limits.MpptUpperBoundV,
                Math.Max(0, (int)Math.Floor(limits.MpptUpperBoundV / t.VmppAtTminV))));
        }

        if (stringVmppAtTmax < limits.StartVoltageV - Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.StartVoltage, stringVmppAtTmax, limits.StartVoltageV,
                Math.Max(1, (int)Math.Ceiling(limits.StartVoltageV / t.VmppAtTmaxV))));
        }

        // Currents use the higher value of both temperatures.
        double arrayIscMax = strings * stringIscMax;
        if (arrayIscMax > limits.MaxShortCircuitCurrentA + Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.ShortCircuitCurrent, arrayIscMax, limits.MaxShortCircuitCurrentA,
                Math.Max(0, (int)Math.Floor(limits.MaxShortCircuitCurrentA / Math.Max(0.001, stringIscMax)))));
        }

        double arrayImppMax = strings * stringImppMax;
        if (arrayImppMax > limits.MaxInputCurrentA + Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.InputCurrent, arrayImppMax, limits.MaxInputCurrentA,
                Math.Max(0, (int)Math.Floor(limits.MaxInputCurrentA / Math.Max(0.001, stringImppMax)))));
        }

        if (inverter.MaxStringsPerMppt > 0 && strings > inverter.MaxStringsPerMppt)
        {
            violations.Add(new LimitViolation(
                ViolationKind.MaxStringsPerMppt, strings, inverter.MaxStringsPerMppt, inverter.MaxStringsPerMppt));
        }

        if (arrayPowerAtTmin > limits.DcPowerPerMpptLimitW + Tolerance)
        {
            violations.Add(new LimitViolation(
                ViolationKind.DcPower, arrayPowerAtTmin, limits.DcPowerPerMpptLimitW,
                Math.Max(0, (int)Math.Floor(limits.DcPowerPerMpptLimitW / Math.Max(1.0, stringPowerAtTmin)))));
        }

        int maxFromVoc = (int)Math.Floor(limits.MaxDcVoltageV / t.VocAtTminV);
        int minFromMppt = (int)Math.Ceiling(limits.MpptLowerBoundV / t.VmppAtTmaxV);
        int maxFromMppt = (int)Math.Floor(limits.MpptUpperBoundV / t.VmppAtTminV);
        int minFromStart = (int)Math.Ceiling(limits.StartVoltageV / t.VmppAtTmaxV);

        return new MpptResult
        {
            MpptIndex = mpptIndex,
            Status = MpptStatus.Calculated,
            Configuration = configuration,
            Module = module,
            ModulesPerString = modules,
            ParallelStrings = strings,
            Temperatures = t,
            StringVocMinV = modules * Math.Min(t.VocAtTminV, t.VocAtTmaxV),
            StringVocMaxV = modules * Math.Max(t.VocAtTminV, t.VocAtTmaxV),
            StringVmppMinV = modules * Math.Min(t.VmppAtTminV, t.VmppAtTmaxV),
            StringVmppMaxV = modules * Math.Max(t.VmppAtTminV, t.VmppAtTmaxV),
            StringIscMinA = Math.Min(t.IscAtTminA, t.IscAtTmaxA),
            StringIscMaxA = stringIscMax,
            StringImppMinA = Math.Min(t.ImppAtTminA, t.ImppAtTmaxA),
            StringImppMaxA = stringImppMax,
            ArrayIscMinA = strings * Math.Min(t.IscAtTminA, t.IscAtTmaxA),
            ArrayIscMaxA = arrayIscMax,
            ArrayImppMinA = strings * Math.Min(t.ImppAtTminA, t.ImppAtTmaxA),
            ArrayImppMaxA = arrayImppMax,
            ArrayPowerAtTminW = arrayPowerAtTmin,
            MinModulesPerString = Math.Max(1, Math.Max(minFromMppt, minFromStart)),
            MaxModulesPerString = Math.Min(maxFromVoc, maxFromMppt),
            Violations = violations,
        };
    }
}
