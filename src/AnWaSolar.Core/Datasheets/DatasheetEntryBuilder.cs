using System.Globalization;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

namespace AnWaSolar.Core.Datasheets;

/// <summary>
/// Builds a master data entry from the texts of the import dialog and checks them. Numbers are accepted with a decimal
/// point or a decimal comma. Beyond the checks of the master data loader, the import requires negative temperature
/// coefficients of Pmax and Voc: a lost minus sign would turn the cold voltage check around.
/// </summary>
public static class DatasheetEntryBuilder
{
    /// <summary>Builds a module; returns the problems, empty when <paramref name="module"/> is valid.</summary>
    public static IReadOnlyList<string> TryBuildModule(IReadOnlyDictionary<string, string?> values, out PVModule? module)
    {
        ArgumentNullException.ThrowIfNull(values);
        var errors = new List<string>();
        var reader = new Reader(values, DatasheetFields.Module, errors);
        var candidate = new PVModule
        {
            Manufacturer = reader.Text(DatasheetFields.Manufacturer),
            Model = reader.Text(DatasheetFields.Model),
            PmaxW = reader.Number(DatasheetFields.PmaxW),
            VmppV = reader.Number(DatasheetFields.VmppV),
            ImppA = reader.Number(DatasheetFields.ImppA),
            VocV = reader.Number(DatasheetFields.VocV),
            IscA = reader.Number(DatasheetFields.IscA),
            EfficiencyPct = reader.Number(DatasheetFields.EfficiencyPct),
            PmaxTempCoeffPctPerC = reader.Number(DatasheetFields.PmaxCoeff),
            VocTempCoeffPctPerC = reader.Number(DatasheetFields.VocCoeff),
            VmppTempCoeffPctPerC = reader.OptionalNumber(DatasheetFields.VmppCoeff),
            IscTempCoeffPctPerC = reader.Number(DatasheetFields.IscCoeff),
            MaxSeriesFuseA = reader.OptionalNumber(DatasheetFields.MaxSeriesFuseA),
            OperatingTempMinC = reader.Number(DatasheetFields.OperatingMinC),
            OperatingTempMaxC = reader.Number(DatasheetFields.OperatingMaxC),
            CableCrossSectionMm2 = reader.OptionalNumber(DatasheetFields.CableCrossSectionMm2),
            CableLengthPlusMm = reader.OptionalNumber(DatasheetFields.CableLengthPlusMm),
            CableLengthMinusMm = reader.OptionalNumber(DatasheetFields.CableLengthMinusMm),
        };

        if (errors.Count == 0)
        {
            if (candidate.VocTempCoeffPctPerC >= 0)
            {
                errors.Add("The temperature coefficient of Voc must be negative (for example -0.24 %/°C).");
            }

            if (candidate.PmaxTempCoeffPctPerC >= 0)
            {
                errors.Add("The temperature coefficient of Pmax must be negative (for example -0.30 %/°C).");
            }

            if (candidate.VmppTempCoeffPctPerC >= 0)
            {
                errors.Add("The temperature coefficient of Vmpp must be negative when it is given.");
            }

            if (candidate.MaxSeriesFuseA is <= 0)
            {
                errors.Add("The max. series fuse rating must be greater than 0 when it is given.");
            }

            if (candidate.CableCrossSectionMm2 is <= 0 || candidate.CableLengthPlusMm is <= 0 || candidate.CableLengthMinusMm is <= 0)
            {
                errors.Add("The cable cross-section and lengths must be greater than 0 when they are given.");
            }

            if (candidate.OperatingTempMinC >= candidate.OperatingTempMaxC)
            {
                errors.Add("The minimum operating temperature must be lower than the maximum.");
            }

            if (JsonDataStore.ValidateModule(candidate) is { } error)
            {
                errors.Add(Capitalize(error));
            }
        }

        module = errors.Count == 0 ? candidate : null;
        return errors;
    }

    /// <summary>Builds an inverter; returns the problems, empty when <paramref name="inverter"/> is valid.</summary>
    public static IReadOnlyList<string> TryBuildInverter(IReadOnlyDictionary<string, string?> values, out Inverter? inverter)
    {
        ArgumentNullException.ThrowIfNull(values);
        var errors = new List<string>();
        var reader = new Reader(values, DatasheetFields.Inverter, errors);
        var candidate = new Inverter
        {
            Manufacturer = reader.Text(DatasheetFields.Manufacturer),
            Model = reader.Text(DatasheetFields.Model),
            MaxDcInputPowerW = reader.Number(DatasheetFields.MaxDcPowerW),
            MaxDcInputVoltageV = reader.Number(DatasheetFields.MaxDcVoltageV),
            StartVoltageV = reader.Number(DatasheetFields.StartVoltageV),
            MpptVoltageRangeV = reader.Range(DatasheetFields.MpptRangeV),
            NominalDcInputVoltageV = reader.OptionalNumber(DatasheetFields.NominalDcVoltageV) ?? 0,
            MaxInputCurrentA = reader.Number(DatasheetFields.MaxInputCurrentA),
            MaxShortCircuitCurrentA = reader.Number(DatasheetFields.MaxShortCircuitCurrentA),
            MpptCount = reader.Integer(DatasheetFields.MpptCount),
            MaxStringsPerMppt = reader.OptionalInteger(DatasheetFields.MaxStringsPerMppt) ?? 0,
            RatedAcPowerW = reader.OptionalNumber(DatasheetFields.RatedAcPowerW) ?? 0,
            MaxAcApparentPowerVA = reader.OptionalNumber(DatasheetFields.MaxAcApparentPowerVA) ?? 0,
            RatedAcCurrentA = reader.OptionalNumber(DatasheetFields.RatedAcCurrentA) ?? 0,
            MaxAcCurrentA = reader.OptionalNumber(DatasheetFields.MaxAcCurrentA) ?? 0,
            AcPhases = reader.OptionalInteger(DatasheetFields.AcPhases) ?? 0,
            BatteryVoltageRangeV = reader.OptionalRange(DatasheetFields.BatteryRangeV),
            MaxBatteryChargeCurrentA = reader.OptionalNumber(DatasheetFields.MaxBatteryChargeA) ?? 0,
            MaxBatteryDischargeCurrentA = reader.OptionalNumber(DatasheetFields.MaxBatteryDischargeA) ?? 0,
            BatteryInputs = reader.OptionalInteger(DatasheetFields.BatteryInputs) ?? 0,
        };

        if (errors.Count == 0)
        {
            if (StringCalculator.TryParseVoltageRange(candidate.MpptVoltageRangeV, out double low, out double high)
                && (high > candidate.MaxDcInputVoltageV || low <= 0))
            {
                errors.Add("The MPPT voltage range must lie below the max. DC input voltage.");
            }

            if (candidate.HasBatteryInput && (candidate.MaxBatteryChargeCurrentA <= 0 || candidate.MaxBatteryDischargeCurrentA <= 0))
            {
                errors.Add("With a battery voltage range, the max. battery charge and discharge current are needed.");
            }
            else if (!candidate.HasBatteryInput && (candidate.MaxBatteryChargeCurrentA > 0 || candidate.MaxBatteryDischargeCurrentA > 0))
            {
                errors.Add("Battery currents need the battery voltage range.");
            }

            if (JsonDataStore.ValidateInverter(candidate) is { } error)
            {
                errors.Add(Capitalize(error));
            }
        }

        inverter = errors.Count == 0 ? candidate : null;
        return errors;
    }

    /// <summary>Builds a battery module; returns the problems, empty when <paramref name="battery"/> is valid.</summary>
    public static IReadOnlyList<string> TryBuildBattery(IReadOnlyDictionary<string, string?> values, out Battery? battery)
    {
        ArgumentNullException.ThrowIfNull(values);
        var errors = new List<string>();
        var reader = new Reader(values, DatasheetFields.Battery, errors);
        var candidate = new Battery
        {
            Manufacturer = reader.Text(DatasheetFields.Manufacturer),
            Model = reader.Text(DatasheetFields.Model),
            NominalVoltageV = reader.Number(DatasheetFields.NominalVoltageV),
            VoltageRangeV = reader.Range(DatasheetFields.OperatingRangeV),
            NominalCapacityAh = reader.OptionalNumber(DatasheetFields.NominalCapacityAh),
            NominalEnergyKWh = reader.Number(DatasheetFields.NominalEnergyKWh),
            UsableEnergyKWh = reader.OptionalNumber(DatasheetFields.UsableEnergyKWh),
            DepthOfDischargePct = reader.OptionalNumber(DatasheetFields.DepthOfDischargePct),
            MaxChargeCurrentA = reader.Number(DatasheetFields.MaxChargeA),
            MaxDischargeCurrentA = reader.Number(DatasheetFields.MaxDischargeA),
            MaxModulesInSeries = reader.OptionalInteger(DatasheetFields.MaxInSeries) ?? 0,
            MaxModulesInParallel = reader.OptionalInteger(DatasheetFields.MaxInParallel) ?? 0,
        };

        if (errors.Count == 0 && JsonDataStore.ValidateBattery(candidate) is { } error)
        {
            errors.Add(Capitalize(error));
        }

        battery = errors.Count == 0 ? candidate : null;
        return errors;
    }

    /// <summary>Reads a number typed by the user, with a decimal point or comma and an optional leading sign.</summary>
    public static bool TryParseNumber(string? text, out double value)
    {
        string normalized = (text ?? string.Empty).Trim().Replace('–', '-').Replace('−', '-').Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private sealed class Reader(IReadOnlyDictionary<string, string?> values, IReadOnlyList<DatasheetField> fields, List<string> errors)
    {
        public string Text(string key)
        {
            string text = Raw(key);
            if (text.Length == 0)
            {
                errors.Add($"{Label(key)} is missing.");
            }

            return text;
        }

        public double Number(string key) => OptionalNumber(key, required: true) ?? 0;

        public double? OptionalNumber(string key) => OptionalNumber(key, required: false);

        public int Integer(string key) => OptionalInteger(key, required: true) ?? 0;

        public int? OptionalInteger(string key) => OptionalInteger(key, required: false);

        public string? OptionalRange(string key) => Raw(key).Length == 0 ? null : Range(key);

        public string Range(string key)
        {
            string text = Raw(key).Replace(" ", string.Empty, StringComparison.Ordinal).Replace('–', '-').Replace('~', '-').Replace(',', '.');
            if (text.Length == 0)
            {
                errors.Add($"{Label(key)} is missing.");
            }
            else if (!StringCalculator.TryParseVoltageRange(text, out _, out _))
            {
                errors.Add($"{Label(key)} \"{Raw(key)}\" is not a range like 160-650.");
            }

            return text;
        }

        private double? OptionalNumber(string key, bool required)
        {
            string text = Raw(key);
            if (text.Length == 0)
            {
                if (required)
                {
                    errors.Add($"{Label(key)} is missing.");
                }

                return null;
            }

            if (!TryParseNumber(text, out double value))
            {
                errors.Add($"{Label(key)} \"{text}\" is not a number.");
                return null;
            }

            return value;
        }

        private int? OptionalInteger(string key, bool required)
        {
            double? value = OptionalNumber(key, required);
            if (value is null)
            {
                return null;
            }

            if (value != Math.Floor(value.Value) || value < 0)
            {
                errors.Add($"{Label(key)} must be a whole number.");
                return null;
            }

            return (int)value.Value;
        }

        private string Raw(string key) => values.TryGetValue(key, out string? text) ? (text ?? string.Empty).Trim() : string.Empty;

        private string Label(string key) => fields.FirstOrDefault(f => f.Key == key)?.Label ?? key;
    }
}
