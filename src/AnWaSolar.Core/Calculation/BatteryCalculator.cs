using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Calculation;

/// <summary>Planned battery bank: the selected battery module and how many are connected in series and in parallel.</summary>
public sealed class BatterySettings
{
    /// <summary>True when a battery storage is planned.</summary>
    public bool Enabled { get; set; }

    public SelectedRef? Battery { get; set; }

    public int InSeries { get; set; } = 1;

    public int InParallel { get; set; } = 1;

    public BatterySettings Clone() => new()
    {
        Enabled = Enabled,
        Battery = Battery is null ? null : new SelectedRef { Manufacturer = Battery.Manufacturer, Model = Battery.Model },
        InSeries = InSeries,
        InParallel = InParallel,
    };
}

/// <summary>Kind of a battery compatibility check.</summary>
public enum BatteryCheckKind
{
    /// <summary>No battery is selected or the selected battery is not in the master data.</summary>
    BatteryMissing,

    /// <summary>The inverter has no battery voltage range or charge and discharge currents in the master data.</summary>
    NoBatteryInput,

    /// <summary>The battery voltage range of the module or the inverter cannot be read.</summary>
    InvalidRange,

    /// <summary>The nominal voltage of the bank lies outside the battery voltage range of the inverter.</summary>
    NominalVoltageOutside,

    /// <summary>The highest bank voltage exceeds the battery voltage range of the inverter.</summary>
    MaxVoltageAbove,

    /// <summary>The lowest bank voltage lies below the battery voltage range of the inverter; part of the capacity is not used.</summary>
    MinVoltageBelow,

    /// <summary>More modules in series than the datasheet allows.</summary>
    SeriesExceeded,

    /// <summary>More modules in parallel than the datasheet allows.</summary>
    ParallelExceeded,

    /// <summary>Several modules in series, but the datasheet states no limit.</summary>
    SeriesNotStated,

    /// <summary>Several modules in parallel, but the datasheet states no limit.</summary>
    ParallelNotStated,

    /// <summary>The bank allows a lower charge current than the inverter.</summary>
    ChargeLimitedByBattery,

    /// <summary>The bank allows a lower discharge current than the inverter.</summary>
    DischargeLimitedByBattery,

    /// <summary>The datasheet states neither a usable energy nor a depth of discharge; the nominal energy is shown.</summary>
    UsableEnergyMissing,
}

/// <summary>A battery check with its state, the value and the limit it was compared with.</summary>
/// <param name="State">Error for an incompatible bank, Warning for a restriction, Neutral for information.</param>
public sealed record BatteryCheck(BatteryCheckKind Kind, ResultState State, double Actual = 0, double Limit = 0);

/// <summary>Result of the battery compatibility check.</summary>
public sealed class BatteryCalculationResult
{
    public Battery? Battery { get; init; }

    public int InSeries { get; init; }

    public int InParallel { get; init; }

    public int ModuleCount => InSeries * InParallel;

    /// <summary>Battery voltage range of the inverter, or null when it is missing or invalid.</summary>
    public (double Min, double Max)? InverterRangeV { get; init; }

    public double? BankNominalV { get; init; }

    /// <summary>Operating voltage range of the bank, or null when it cannot be computed.</summary>
    public (double Min, double Max)? BankRangeV { get; init; }

    public double? BankChargeCurrentA { get; init; }

    public double? BankDischargeCurrentA { get; init; }

    /// <summary>Lower of the inverter and bank charge current; null when one of them is missing.</summary>
    public double? ChargeCurrentA { get; init; }

    public double? DischargeCurrentA { get; init; }

    /// <summary>Charge current times the nominal bank voltage.</summary>
    public double? ChargePowerW { get; init; }

    public double? DischargePowerW { get; init; }

    public double? NominalEnergyKWh { get; init; }

    public double? UsableEnergyKWh { get; init; }

    public IReadOnlyList<BatteryCheck> Checks { get; init; } = [];

    /// <summary>Error when a check fails, Warning for a restriction, otherwise OK.</summary>
    public ResultState State => Checks.Select(c => c.State).Where(s => s != ResultState.Neutral).DefaultIfEmpty(ResultState.Ok).Max();
}

/// <summary>
/// Checks a battery bank against the battery input of a hybrid inverter: voltage range, module counts, charge and
/// discharge current and the resulting power and energy. All values come from the datasheets; a missing value is
/// reported, not estimated. The safety margin of the PV calculation does not apply: the battery voltage is held in its
/// range by the battery management system.
/// </summary>
public static class BatteryCalculator
{
    private const double Tolerance = 1e-6;

    public static BatteryCalculationResult Calculate(Inverter inverter, Battery? battery, int inSeries, int inParallel)
    {
        ArgumentNullException.ThrowIfNull(inverter);
        int series = Math.Max(1, inSeries);
        int parallel = Math.Max(1, inParallel);
        var checks = new List<BatteryCheck>();

        (double Min, double Max)? inverterRange = null;
        if (!inverter.HasBatteryInput || inverter.MaxBatteryChargeCurrentA <= 0 || inverter.MaxBatteryDischargeCurrentA <= 0)
        {
            checks.Add(new BatteryCheck(BatteryCheckKind.NoBatteryInput, ResultState.Error));
        }
        else if (StringCalculator.TryParseVoltageRange(inverter.BatteryVoltageRangeV!, out double invMin, out double invMax))
        {
            inverterRange = (invMin, invMax);
        }
        else
        {
            checks.Add(new BatteryCheck(BatteryCheckKind.InvalidRange, ResultState.Error));
        }

        if (battery is null)
        {
            checks.Add(new BatteryCheck(BatteryCheckKind.BatteryMissing, ResultState.Warning));
            return new BatteryCalculationResult { InSeries = series, InParallel = parallel, InverterRangeV = inverterRange, Checks = checks };
        }

        double nominal = series * battery.NominalVoltageV;
        (double Min, double Max)? bankRange = null;
        if (StringCalculator.TryParseVoltageRange(battery.VoltageRangeV, out double modMin, out double modMax))
        {
            bankRange = (series * modMin, series * modMax);
        }
        else
        {
            checks.Add(new BatteryCheck(BatteryCheckKind.InvalidRange, ResultState.Error));
        }

        if (inverterRange is { } inv)
        {
            if (nominal < inv.Min - Tolerance || nominal > inv.Max + Tolerance)
            {
                checks.Add(new BatteryCheck(BatteryCheckKind.NominalVoltageOutside, ResultState.Error, nominal, nominal > inv.Max ? inv.Max : inv.Min));
            }

            if (bankRange is { } bank)
            {
                if (bank.Max > inv.Max + Tolerance)
                {
                    checks.Add(new BatteryCheck(BatteryCheckKind.MaxVoltageAbove, ResultState.Error, bank.Max, inv.Max));
                }

                if (bank.Min < inv.Min - Tolerance)
                {
                    checks.Add(new BatteryCheck(BatteryCheckKind.MinVoltageBelow, ResultState.Warning, bank.Min, inv.Min));
                }
            }
        }

        AddCountCheck(checks, series, battery.MaxModulesInSeries, BatteryCheckKind.SeriesExceeded, BatteryCheckKind.SeriesNotStated);
        AddCountCheck(checks, parallel, battery.MaxModulesInParallel, BatteryCheckKind.ParallelExceeded, BatteryCheckKind.ParallelNotStated);

        double bankCharge = parallel * battery.MaxChargeCurrentA;
        double bankDischarge = parallel * battery.MaxDischargeCurrentA;
        double? charge = null;
        double? discharge = null;
        if (inverter.MaxBatteryChargeCurrentA > 0)
        {
            charge = Math.Min(bankCharge, inverter.MaxBatteryChargeCurrentA);
            if (bankCharge < inverter.MaxBatteryChargeCurrentA - Tolerance)
            {
                checks.Add(new BatteryCheck(BatteryCheckKind.ChargeLimitedByBattery, ResultState.Neutral, bankCharge, inverter.MaxBatteryChargeCurrentA));
            }
        }

        if (inverter.MaxBatteryDischargeCurrentA > 0)
        {
            discharge = Math.Min(bankDischarge, inverter.MaxBatteryDischargeCurrentA);
            if (bankDischarge < inverter.MaxBatteryDischargeCurrentA - Tolerance)
            {
                checks.Add(new BatteryCheck(BatteryCheckKind.DischargeLimitedByBattery, ResultState.Neutral, bankDischarge, inverter.MaxBatteryDischargeCurrentA));
            }
        }

        if (battery.UsableEnergyPerModuleKWh is null)
        {
            checks.Add(new BatteryCheck(BatteryCheckKind.UsableEnergyMissing, ResultState.Neutral));
        }

        int modules = series * parallel;
        return new BatteryCalculationResult
        {
            Battery = battery,
            InSeries = series,
            InParallel = parallel,
            InverterRangeV = inverterRange,
            BankNominalV = nominal,
            BankRangeV = bankRange,
            BankChargeCurrentA = bankCharge,
            BankDischargeCurrentA = bankDischarge,
            ChargeCurrentA = charge,
            DischargeCurrentA = discharge,
            ChargePowerW = charge * nominal,
            DischargePowerW = discharge * nominal,
            NominalEnergyKWh = modules * battery.NominalEnergyKWh,
            UsableEnergyKWh = modules * battery.UsableEnergyPerModuleKWh,
            Checks = checks,
        };
    }

    private static void AddCountCheck(List<BatteryCheck> checks, int count, int max, BatteryCheckKind exceeded, BatteryCheckKind notStated)
    {
        if (max > 0 && count > max)
        {
            checks.Add(new BatteryCheck(exceeded, ResultState.Error, count, max));
        }
        else if (max == 0 && count > 1)
        {
            checks.Add(new BatteryCheck(notStated, ResultState.Warning, count, 0));
        }
    }
}

/// <summary>Texts of the battery checks, shared by the result card, the report and the plain text.</summary>
public static class BatteryTexts
{
    /// <summary>The values of the battery bank as lines for the report and the plain text.</summary>
    public static IReadOnlyList<string> Values(BatteryCalculationResult result, Inverter inverter)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(inverter);
        System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
        string Range((double Min, double Max)? r) => r is { } v ? string.Create(c, $"{v.Min:0.#}–{v.Max:0.#} V") : "not stated";
        var lines = new List<string>();
        if (result.Battery is not { } b)
        {
            lines.Add("No battery selected.");
            return lines;
        }

        lines.Add(string.Create(c, $"Battery: {b.Manufacturer} {b.Model}, {result.InSeries} in series × {result.InParallel} in parallel = {result.ModuleCount} modules"));
        lines.Add(string.Create(c, $"Bank voltage: {Range(result.BankRangeV)}, nominal {result.BankNominalV:0.#} V; battery voltage range of the inverter: {Range(result.InverterRangeV)}"));
        if (result.ChargeCurrentA is { } charge)
        {
            lines.Add(string.Create(c, $"Charge current: {charge:0.#} A = {result.ChargePowerW / 1000:0.0} kW (bank {result.BankChargeCurrentA:0.#} A, inverter {inverter.MaxBatteryChargeCurrentA:0.#} A)"));
        }

        if (result.DischargeCurrentA is { } discharge)
        {
            lines.Add(string.Create(c, $"Discharge current: {discharge:0.#} A = {result.DischargePowerW / 1000:0.0} kW (bank {result.BankDischargeCurrentA:0.#} A, inverter {inverter.MaxBatteryDischargeCurrentA:0.#} A)"));
        }

        string usable = result.UsableEnergyKWh is { } u
            ? string.Create(c, $", usable {u:0.##} kWh") + (b.UsableEnergyKWh is null ? string.Create(c, $" (depth of discharge {b.DepthOfDischargePct:0.#} %)") : string.Empty)
            : string.Empty;
        lines.Add(string.Create(c, $"Energy: nominal {result.NominalEnergyKWh:0.##} kWh{usable}"));
        return lines;
    }

    public static string Format(BatteryCheck check, BatteryCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(result);
        System.Globalization.CultureInfo c = System.Globalization.CultureInfo.CurrentCulture;
        string V(double v) => v.ToString("0.#", c) + " V";
        string A(double v) => v.ToString("0.#", c) + " A";
        return check.Kind switch
        {
            BatteryCheckKind.BatteryMissing => "Select a battery to check it against the inverter.",
            BatteryCheckKind.NoBatteryInput => "The inverter has no battery input in the master data (battery voltage range, max. charge and discharge current). A battery can only be connected to a hybrid inverter.",
            BatteryCheckKind.InvalidRange => "A battery voltage range cannot be read (expected format: \"40-60\").",
            BatteryCheckKind.NominalVoltageOutside => $"The nominal bank voltage {V(check.Actual)} lies outside the battery voltage range of the inverter.",
            BatteryCheckKind.MaxVoltageAbove => $"The highest bank voltage {V(check.Actual)} exceeds the max. battery voltage of the inverter ({V(check.Limit)}).",
            BatteryCheckKind.MinVoltageBelow => $"The lowest bank voltage {V(check.Actual)} lies below the min. battery voltage of the inverter ({V(check.Limit)}); the inverter stops discharging earlier, so part of the capacity is not used.",
            BatteryCheckKind.SeriesExceeded => $"{check.Actual:0} modules in series exceed the max. of {check.Limit:0} from the battery datasheet.",
            BatteryCheckKind.ParallelExceeded => $"{check.Actual:0} modules in parallel exceed the max. of {check.Limit:0} from the battery datasheet.",
            BatteryCheckKind.SeriesNotStated => "The battery datasheet states no max. number of modules in series; check that the modules may be connected in series.",
            BatteryCheckKind.ParallelNotStated => "The battery datasheet states no max. number of modules in parallel; check that the modules may be connected in parallel.",
            BatteryCheckKind.ChargeLimitedByBattery => $"The bank allows {A(check.Actual)} charge current; the inverter could charge with {A(check.Limit)}.",
            BatteryCheckKind.DischargeLimitedByBattery => $"The bank allows {A(check.Actual)} discharge current; the inverter could discharge with {A(check.Limit)}.",
            BatteryCheckKind.UsableEnergyMissing => "The battery datasheet states neither a usable energy nor a depth of discharge; only the nominal energy is shown.",
            _ => check.Kind.ToString(),
        };
    }
}
