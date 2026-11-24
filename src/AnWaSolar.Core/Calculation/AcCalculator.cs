using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>Kind of a check of the AC side and the grid connection.</summary>
public enum AcCheckKind
{
    /// <summary>The inverter has no rated AC power, phase count or AC current in the master data.</summary>
    AcValuesMissing,

    /// <summary>PV peak power to rated AC power is below the recommended range.</summary>
    DcAcRatioLow,

    /// <summary>PV peak power to rated AC power is above the recommended range (clipping).</summary>
    DcAcRatioHigh,

    /// <summary>A single-phase inverter exceeds the unbalanced load limit.</summary>
    UnbalancedLoad,

    /// <summary>The apparent power exceeds the limit of the simplified grid registration.</summary>
    RegistrationLimit,

    /// <summary>The feed-in limit caps the power fed into the grid.</summary>
    FeedInLimit,
}

/// <summary>A check of the AC side with its state, the computed value and the limit it was compared with.</summary>
/// <param name="State">Error for a violated grid rule, Warning for a recommendation, Neutral for information.</param>
public sealed record AcCheck(AcCheckKind Kind, ResultState State, double Actual, double Limit);

/// <summary>Result of the AC side and grid connection checks.</summary>
public sealed class AcCalculationResult
{
    /// <summary>PV peak power (STC) of all calculated MPPTs.</summary>
    public required double PvPeakPowerW { get; init; }

    /// <summary>Rated AC power from the datasheet, or null when it is not stated.</summary>
    public double? RatedAcPowerW { get; init; }

    /// <summary>Max. AC apparent power from the datasheet, otherwise the rated AC power; null when neither is stated.</summary>
    public double? ApparentPowerVA { get; init; }

    /// <summary>PV peak power divided by the rated AC power; null without rated AC power or PV power.</summary>
    public double? DcAcRatio { get; init; }

    /// <summary>AC phases (1 or 3), or null when not stated.</summary>
    public int? Phases { get; init; }

    public double? RatedAcCurrentA { get; init; }

    public double? MaxAcCurrentA { get; init; }

    /// <summary>Highest feed-in power with the feed-in limit, or null without a limit.</summary>
    public double? FeedInCapW { get; init; }

    /// <summary>Names of the AC datasheet values that are missing.</summary>
    public IReadOnlyList<string> MissingValues { get; init; } = [];

    public IReadOnlyList<AcCheck> Checks { get; init; } = [];

    public bool HasErrors => Checks.Any(c => c.State == ResultState.Error);
}

/// <summary>
/// Checks the AC side of the inverter: DC/AC ratio, unbalanced load of single-phase inverters, the limit of the
/// simplified grid registration and the feed-in limit. The AC values come from the datasheet only; a missing value is
/// reported, not derived. The limits are the user's parameters.
/// </summary>
public static class AcCalculator
{
    private const double Tolerance = 1e-6;

    public static AcCalculationResult Calculate(Inverter inverter, IReadOnlyList<MpptResult> mppts, CalculationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        ArgumentNullException.ThrowIfNull(mppts);
        ArgumentNullException.ThrowIfNull(parameters);

        double pvPeak = mppts
            .Where(m => m.Status == MpptStatus.Calculated && m.Module is not null)
            .Sum(m => m.ModulesPerString * m.ParallelStrings * m.Module!.PmaxW);
        double? ratedAc = inverter.RatedAcPowerW > 0 ? inverter.RatedAcPowerW : null;
        double? apparent = inverter.MaxAcApparentPowerVA > 0 ? inverter.MaxAcApparentPowerVA : ratedAc;
        int? phases = inverter.AcPhases > 0 ? inverter.AcPhases : null;

        var missing = new List<string>();
        if (ratedAc is null)
        {
            missing.Add("rated AC power");
        }

        if (phases is null)
        {
            missing.Add("AC phases");
        }

        if (inverter.MaxAcCurrentA <= 0)
        {
            missing.Add("max. AC current");
        }

        var checks = new List<AcCheck>();
        if (missing.Count > 0)
        {
            checks.Add(new AcCheck(AcCheckKind.AcValuesMissing, ResultState.Warning, 0, 0));
        }

        double? ratio = ratedAc is { } ac && pvPeak > 0 ? pvPeak / ac : null;
        if (ratio is { } r)
        {
            if (r < parameters.DcAcRatioMin - Tolerance)
            {
                checks.Add(new AcCheck(AcCheckKind.DcAcRatioLow, ResultState.Warning, r, parameters.DcAcRatioMin));
            }
            else if (r > parameters.DcAcRatioMax + Tolerance)
            {
                checks.Add(new AcCheck(AcCheckKind.DcAcRatioHigh, ResultState.Warning, r, parameters.DcAcRatioMax));
            }
        }

        if (phases == 1 && apparent is { } single && single > parameters.UnbalancedLoadLimitVA + Tolerance)
        {
            checks.Add(new AcCheck(AcCheckKind.UnbalancedLoad, ResultState.Error, single, parameters.UnbalancedLoadLimitVA));
        }

        if (apparent is { } s && s > parameters.SimplifiedRegistrationLimitVA + Tolerance)
        {
            checks.Add(new AcCheck(AcCheckKind.RegistrationLimit, ResultState.Warning, s, parameters.SimplifiedRegistrationLimitVA));
        }

        double? feedInCap = null;
        if (parameters.FeedInLimitPct < 100 - Tolerance && pvPeak > 0)
        {
            feedInCap = pvPeak * parameters.FeedInLimitPct / 100;
            checks.Add(new AcCheck(AcCheckKind.FeedInLimit, ResultState.Neutral, feedInCap.Value, ratedAc ?? 0));
        }

        return new AcCalculationResult
        {
            PvPeakPowerW = pvPeak,
            RatedAcPowerW = ratedAc,
            ApparentPowerVA = apparent,
            DcAcRatio = ratio,
            Phases = phases,
            RatedAcCurrentA = inverter.RatedAcCurrentA > 0 ? inverter.RatedAcCurrentA : null,
            MaxAcCurrentA = inverter.MaxAcCurrentA > 0 ? inverter.MaxAcCurrentA : null,
            FeedInCapW = feedInCap,
            MissingValues = missing,
            Checks = checks,
        };
    }
}
