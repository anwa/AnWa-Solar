using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AnWaSolar.Core.Datasheets;

/// <summary>A value found in a datasheet.</summary>
/// <param name="Text">The value as it is entered into the dialog (invariant culture, for example "44.3" or "160-650").</param>
/// <param name="Source">The datasheet line the value was read from.</param>
public sealed record DatasheetValue(string Text, string Source);

/// <summary>Values found in a datasheet, per model or power class (column) of the datasheet.</summary>
public sealed class DatasheetParseResult
{
    internal DatasheetParseResult(DatasheetKind kind, string? manufacturer, IReadOnlyList<string> variants,
        IReadOnlyDictionary<string, DatasheetValue?[]> values)
    {
        Kind = kind;
        Manufacturer = manufacturer;
        Variants = variants;
        Values = values;
    }

    public DatasheetKind Kind { get; }

    /// <summary>Manufacturer recognised in the text, or null.</summary>
    public string? Manufacturer { get; }

    /// <summary>Model or power class names of the datasheet columns; one entry (possibly empty) for a single column.</summary>
    public IReadOnlyList<string> Variants { get; }

    /// <summary>Values per field key, one per variant; null where nothing was found.</summary>
    public IReadOnlyDictionary<string, DatasheetValue?[]> Values { get; }

    /// <summary>The value of a field for a variant, or null.</summary>
    public DatasheetValue? Get(string key, int variant) =>
        Values.TryGetValue(key, out DatasheetValue?[]? values) && variant >= 0 && variant < values.Length ? values[variant] : null;
}

/// <summary>
/// Finds PV module and inverter values in the words of a datasheet. Datasheets differ in layout and language
/// (German and English labels are recognised), so the result is a proposal: the user checks every value against the
/// datasheet before it is saved. A value that is not found stays empty; nothing is estimated.
/// </summary>
public static partial class DatasheetParser
{
    /// <summary>Manufacturers recognised in addition to the ones of the master data.</summary>
    public static IReadOnlyList<string> KnownManufacturers { get; } =
    [
        "AIKO", "Canadian Solar", "DAS Solar", "Deye", "Felicity", "Fronius", "GoodWe", "Growatt", "Hanwha", "Huawei",
        "JA Solar", "Jinko", "Kostal", "LONGi", "Meyer Burger", "Q CELLS", "REC", "Risen", "SMA", "SolarEdge",
        "Sungrow", "Tongwei", "Trina", "Victron", "Viessmann",
    ];

    // Label patterns on the line text in lower case without spaces. Several patterns of a field are tried in order.
    private static readonly Dictionary<string, Regex[]> s_moduleLabels = new()
    {
        [DatasheetFields.PmaxW] = [Re(@"(?<![a-z])p-?max|nennleistung|nominalleistung|maximumpower(?!voltage|current|point)|ratedpower|peakpower")],
        [DatasheetFields.VmppV] = [Re(@"spannungimmpp|(?<![a-z])[uv]-?mpp?(?![a-z])|maximumpowervoltage|optimumoperatingvoltage|mpp-?spannung")],
        [DatasheetFields.ImppA] = [Re(@"stromimmpp|(?<![a-z])i-?mpp?(?![a-z])|maximumpowercurrent|optimumoperatingcurrent|mpp-?strom")],
        [DatasheetFields.VocV] = [Re(@"leerlaufspannung|open-?circuitvoltage|(?<![a-z])[uv]-?oc(?![a-z])")],
        [DatasheetFields.IscA] = [Re(@"kurzschlus+strom|short-?circuitcurrent|(?<![a-z])i-?sc(?![a-z])")],
        [DatasheetFields.EfficiencyPct] = [Re(@"modul(e)?wirkungsgrad|moduleefficiency|wirkungsgrad|efficiency")],
    };

    private static readonly Dictionary<string, Regex[]> s_coefficientLabels = new()
    {
        [DatasheetFields.PmaxCoeff] = [Re(@"(koeff|coeff).*?p-?max|p-?max.*?(koeff|coeff)")],
        [DatasheetFields.VocCoeff] = [Re(@"(koeff|coeff).*?([uv]-?oc(?![a-z])|leerlauf|open-?circuit)|([uv]-?oc).*?(koeff|coeff)")],
        [DatasheetFields.VmppCoeff] = [Re(@"(koeff|coeff).*?([uv]-?mpp?(?![a-z]))")],
        [DatasheetFields.IscCoeff] = [Re(@"(koeff|coeff).*?(i-?sc(?![a-z])|kurzschlus|short-?circuit)|(i-?sc).*?(koeff|coeff)")],
    };

    private static readonly Regex[] s_seriesFuse = [Re(@"maximaleabsicherung|max(imum)?\.?seriesfuse|seriesfuserating|max\.?rückstrom|reversecurrent|max\.?sicherung")];

    private static readonly Regex s_cableLabel = Re(@"kabel|cable");

    private static readonly Regex s_portrait = Re(@"hochformat|portrait");

    private static readonly Regex[] s_operatingTemperature = [Re(@"(?<!nenn)betriebstemperatur|operatingtemperature|operationaltemperature|operatingambienttemperature")];

    private static readonly Dictionary<string, Regex[]> s_inverterLabels = new()
    {
        [DatasheetFields.MaxDcPowerW] = [Re(@"max\.?dc-?eingangsleistung|max\.?dcinputpower|max(imum)?\.?dcpower"), Re(@"max\.?pv-?eingangsleistung|max\.?pvinputpower")],
        [DatasheetFields.MaxDcVoltageV] = [Re(@"max\.?dc-?eingangsspannung|max(imum)?\.?(dc|pv)inputvoltage|max\.?pv-?eingangsspannung|maximuminputvoltage")],
        [DatasheetFields.StartVoltageV] = [Re(@"startspannung|start-?upvoltage|startvoltage|einschaltspannung")],
        [DatasheetFields.MpptRangeV] = [Re(@"mppt?-?spannungsbereich|mppt?-?voltagerange?|mppt?operatingvoltagerange|mpp-?bereich")],
        [DatasheetFields.NominalDcVoltageV] = [Re(@"nenn-?dc-?eingangsspannung|(rated|nominal)(dc)?inputvoltage|nenneingangsspannung")],
        [DatasheetFields.MaxInputCurrentA] = [Re(@"max\.?betriebs-?pv-?eingangsstrom|max\.?operatingpvinputcurrent|max(imum)?\.?(pv|dc)?inputcurrent(?!.*short)|max\.?(pv-?)?eingangsstrom")],
        [DatasheetFields.RatedAcPowerW] = [Re(@"nenn-?ac-?(eingangs-?/)?ausgangs-?wirkleistung|ratedac(input/)?output(active)?power|ratedacpower|ac-?nennleistung|nenn-?ausgangsleistung|(rated|nominal)(ac)?outputpower|nominalacpower")],
        [DatasheetFields.MaxAcApparentPowerVA] = [Re(@"scheinleistung|apparentpower")],
        [DatasheetFields.RatedAcCurrentA] = [Re(@"ac-?(eingangs-?/)?ausgangsnennstrom|nenn-?ac-?strom|ratedac(input/)?outputcurrent|(rated|nominal)outputcurrent")],
        [DatasheetFields.MaxAcCurrentA] = [Re(@"max\.?ac-?(eingangs-?/)?ausgangs+strom|max(imum)?\.?ac(input/)?outputcurrent|max(imum)?\.?outputcurrent")],
        [DatasheetFields.MaxShortCircuitCurrentA] = [Re(@"eingangs-?kurzschlus+strom|inputshort-?circuitcurrent|max(imum)?\.?short-?circuitcurrent|max\.?kurzschlus+strom|iscpv")],
        [DatasheetFields.BatteryRangeV] = [Re(@"batteriespannungsbereich|batteryvoltagerange|batteryvoltage(?!.*nominal)")],
        [DatasheetFields.MaxBatteryChargeA] = [Re(@"max\.?(batterie-?)?ladestrom|max(imum)?\.?(battery)?(?<!dis)charg(e|ing)current")],
        [DatasheetFields.MaxBatteryDischargeA] = [Re(@"max\.?(batterie-?)?entladestrom|max(imum)?\.?(battery)?discharg(e|ing)current")],
    };

    private static readonly Regex[] s_batteryInputs = [Re(@"anzahlderbatterieeingänge|(no\.?of|numberof)batter(y|ies)inputs?|batteryinputs")];

    // Battery modules: values of one module, per model column.
    private static readonly Dictionary<string, Regex[]> s_batteryLabels = new()
    {
        [DatasheetFields.NominalVoltageV] = [Re(@"nennspannung|nominalvoltage|ratedvoltage")],
        [DatasheetFields.OperatingRangeV] = [Re(@"betriebsspannung|spannungsbereich|operatingvoltage|voltagerange|workingvoltage")],
        [DatasheetFields.NominalCapacityAh] = [Re(@"nennkapazität|nominalcapacity|ratedcapacity|kapazität|capacity(?!.*kwh)")],
        [DatasheetFields.NominalEnergyKWh] = [Re(@"nennenergie|nominalenergy|ratedenergy|energie\(kwh\)|energy\(kwh\)|nennenergieinhalt|gesamtenergie|totalenergy")],
        [DatasheetFields.UsableEnergyKWh] = [Re(@"nutzbareenergie|nutzbarekapazität|nutzbarerenergieinhalt|usableenergy|usablecapacity")],
        [DatasheetFields.DepthOfDischargePct] = [Re(@"entladetiefe|depthofdischarge|(?<![a-z])dod(?![a-z])")],
        [DatasheetFields.MaxChargeA] = [Re(@"max\.?(dauer-?)?ladestrom|max(imum)?\.?(continuous)?(?<!dis)charg(e|ing)current"), Re(@"(?<!ent)ladestrom|(?<!dis)charg(e|ing)current")],
        [DatasheetFields.MaxDischargeA] = [Re(@"max\.?(dauer-?)?entladestrom|max(imum)?\.?(continuous)?discharg(e|ing)current"), Re(@"entladestrom|discharg(e|ing)current")],
        [DatasheetFields.MaxInSeries] = [Re(@"inreihe|inserie|seriesconnection|reihenschaltung|max\.?series")],
        [DatasheetFields.MaxInParallel] = [Re(@"parallel|skalierbarkeit|scalability")],
    };

    private static readonly Regex[] s_batteryWords = [Re(@"lifepo|zyklen|cycles?(?![a-z])|kwh|entladetiefe|depthofdischarge|(?<![a-z])dod(?![a-z])|zellchemie|cellchemistry|batteriemodul|batterymodule")];

    // A neighbouring line with a peak value ("Spitzenwert 280 A (10 Sek.)") does not hold the continuous value of a label.
    private static readonly Regex s_peakValue = Re(@"spitzenwert|peak");

    private static readonly Regex s_threePhase = Re(@"dreiphas|three-?phase|3l\+n\+pe|3/n/pe|3w\+n\+pe");

    private static readonly Regex s_singlePhase = Re(@"einphas|single-?phase|1/n/pe|(?<![0-9a-z])l\+n\+pe");

    private static readonly Regex[] s_mpptCount = [Re(@"anzahlder(mpp|mpp-?tracker)|no\.?ofmppt?|numberofmppt?|mpp-?trackers?/|mppt?anzahl")];

    private static readonly Regex[] s_stringsPerMppt = [Re(@"strings?(je|pro|per)mppt?|anzahlderstrings|no\.?ofstrings|numberofstrings")];

    private static readonly Regex[] s_moduleWords = [Re(@"modul|module|zelle|cell|leerlauf|open-?circuit|(?<![a-z])[uv]-?oc(?![a-z])|wp\)")];

    private static readonly Regex[] s_inverterWords = [Re(@"wechselrichter|inverter|mppt|ac-?ausgang|acoutput|netz|grid|batter")];

    /// <summary>Kind of the datasheet from its words; module when it cannot be told.</summary>
    public static DatasheetKind DetectKind(IReadOnlyList<DatasheetWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        string text = string.Concat(words.Select(w => w.Text)).ToLowerInvariant();
        int module = s_moduleWords.Sum(r => r.Matches(text).Count);
        int inverter = s_inverterWords.Sum(r => r.Matches(text).Count);
        int battery = s_batteryWords.Sum(r => r.Matches(text).Count);
        if (battery > inverter && battery > module)
        {
            return DatasheetKind.Battery;
        }

        return inverter > module ? DatasheetKind.Inverter : DatasheetKind.Module;
    }

    /// <summary>Reads the values of the given kind.</summary>
    /// <param name="manufacturers">Manufacturer names of the master data; recognised in their spelling.</param>
    public static DatasheetParseResult Parse(IReadOnlyList<DatasheetWord> words, DatasheetKind kind, IEnumerable<string>? manufacturers = null)
    {
        ArgumentNullException.ThrowIfNull(words);
        List<Line> lines = BuildLines(words);
        var values = new Dictionary<string, DatasheetValue?[]>();
        Columns columns;

        if (kind == DatasheetKind.Module)
        {
            // The Pmax row with the most values gives the power class columns.
            Candidate? pmax = FindCandidates(lines, s_moduleLabels[DatasheetFields.PmaxW])
                .OrderByDescending(c => c.Tokens.Count(t => TryNumber(t.Text, out _)))
                .FirstOrDefault();
            columns = ModuleColumns(lines, pmax, " W");
            foreach ((string key, Regex[] patterns) in s_moduleLabels)
            {
                values[key] = ReadColumns(lines, patterns, columns, NumberOf);
            }

            foreach ((string key, Regex[] patterns) in s_coefficientLabels)
            {
                values[key] = Shared(columns, FirstValue(lines, patterns, NumberOf, sameLineOnly: true));
            }

            values[DatasheetFields.MaxSeriesFuseA] = Shared(columns, FirstValue(lines, s_seriesFuse, NumberOf, sameLineOnly: true));

            (DatasheetValue? area, DatasheetValue? plus, DatasheetValue? minus) = ReadModuleCable(lines);
            values[DatasheetFields.CableCrossSectionMm2] = Shared(columns, area);
            values[DatasheetFields.CableLengthPlusMm] = Shared(columns, plus);
            values[DatasheetFields.CableLengthMinusMm] = Shared(columns, minus);

            DatasheetValue?[] temperature = ReadOperatingTemperature(lines, columns);
            values[DatasheetFields.OperatingMinC] = [.. temperature.Select(v => v is null ? null : v with { Text = v.Text.Split('|')[0] })];
            values[DatasheetFields.OperatingMaxC] = [.. temperature.Select(v => v is null ? null : v with { Text = v.Text.Split('|')[1] })];
        }
        else if (kind == DatasheetKind.Battery)
        {
            // The energy or capacity row with the most values gives the model columns; the model names of battery
            // datasheets are often graphics, so a column may stay without name.
            Candidate? energy = FindCandidates(lines, s_batteryLabels[DatasheetFields.NominalEnergyKWh])
                .Concat(FindCandidates(lines, s_batteryLabels[DatasheetFields.NominalCapacityAh]))
                .OrderByDescending(c => c.Tokens.Count(t => TryNumber(t.Text, out _)))
                .FirstOrDefault();
            columns = ModuleColumns(lines, energy, fallbackUnit: null);
            foreach ((string key, Regex[] patterns) in s_batteryLabels)
            {
                if (key == DatasheetFields.OperatingRangeV)
                {
                    values[key] = Shared(columns, FirstRange(lines, patterns));
                    continue;
                }

                Func<string, string?> read = key switch
                {
                    DatasheetFields.MaxInSeries or DatasheetFields.MaxInParallel => IntegerOf,
                    DatasheetFields.NominalCapacityAh => t => UnitOf(t, "Ah"),
                    DatasheetFields.NominalEnergyKWh or DatasheetFields.UsableEnergyKWh => t => UnitOf(t, "kWh"),
                    _ => NumberOf,
                };
                values[key] = ReadColumns(lines, patterns, columns, read);
            }
        }
        else
        {
            columns = InverterColumns(lines);
            foreach ((string key, Regex[] patterns) in s_inverterLabels)
            {
                Func<string, string?> read = key switch
                {
                    DatasheetFields.MpptRangeV or DatasheetFields.BatteryRangeV => RangeOf,
                    DatasheetFields.MaxInputCurrentA or DatasheetFields.MaxShortCircuitCurrentA => PerMpptOf,
                    DatasheetFields.RatedAcCurrentA or DatasheetFields.MaxAcCurrentA => LastOf,
                    _ => NumberOf,
                };
                values[key] = ReadColumns(lines, patterns, columns, read);
            }

            values[DatasheetFields.MpptCount] = ReadColumns(lines, s_mpptCount, columns, t => MpptAndStrings(t)?.Mppts);
            DatasheetValue?[] strings = ReadColumns(lines, s_mpptCount, columns, t => MpptAndStrings(t)?.Strings);
            if (strings.All(v => v is null))
            {
                strings = ReadColumns(lines, s_stringsPerMppt, columns, IntegerOf);
            }

            values[DatasheetFields.MaxStringsPerMppt] = strings;
            values[DatasheetFields.BatteryInputs] = ReadColumns(lines, s_batteryInputs, columns, IntegerOf);

            string text = string.Concat(words.Select(w => Normalize(w.Text))).ToLowerInvariant();
            string? phases = s_threePhase.IsMatch(text) ? "3" : s_singlePhase.IsMatch(text) ? "1" : null;
            values[DatasheetFields.AcPhases] = Shared(columns, phases is null ? null : new DatasheetValue(phases, phases == "3" ? L.T("Three-phase grid connection in the datasheet") : L.T("Single-phase grid connection in the datasheet")));
        }

        string? manufacturer = FindManufacturer(words, manufacturers);
        values[DatasheetFields.Manufacturer] = Shared(columns, manufacturer is null ? null : new DatasheetValue(manufacturer, L.T("Manufacturer name in the datasheet")));
        values[DatasheetFields.Model] = [.. columns.Names.Select(n => string.IsNullOrWhiteSpace(n) ? null : new DatasheetValue(n, L.T("Column heading in the datasheet")))];
        return new DatasheetParseResult(kind, manufacturer, columns.Names, values);
    }

    #region Lines

    private sealed class Line
    {
        public required int Page { get; init; }

        /// <summary>Baseline of the line (average bottom of its words).</summary>
        public required double CenterY { get; init; }

        public required double Height { get; init; }

        public required List<DatasheetWord> Words { get; init; }

        /// <summary>Lower case text without spaces, for label matching.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Index in <see cref="Key"/> where each word starts.</summary>
        public List<int> Offsets { get; } = [];

        public string Text => string.Join(" ", Words.Select(w => w.Text));
    }

    private static List<Line> BuildLines(IReadOnlyList<DatasheetWord> words)
    {
        var lines = new List<Line>();
        foreach (IGrouping<int, DatasheetWord> page in words.GroupBy(w => w.Page).OrderBy(g => g.Key))
        {
            var current = new List<DatasheetWord>();
            // Words of a row share the baseline (bottom); the boxes of some fonts are much taller than the glyphs, so
            // neither the centre nor the box height is reliable.
            foreach (DatasheetWord word in page.OrderByDescending(w => w.Bottom))
            {
                if (current.Count > 0)
                {
                    double baseline = current.Average(w => w.Bottom);
                    double height = current.Min(w => w.Height);
                    if (Math.Abs(word.Bottom - baseline) > 0.45 * Math.Min(height, word.Height))
                    {
                        lines.Add(NewLine(page.Key, current));
                        current = [];
                    }
                }

                current.Add(word);
            }

            if (current.Count > 0)
            {
                lines.Add(NewLine(page.Key, current));
            }
        }

        return lines;
    }

    private static Line NewLine(int page, List<DatasheetWord> words)
    {
        var line = new Line
        {
            Page = page,
            CenterY = words.Average(w => w.Bottom),
            Height = words.Min(w => w.Height),
            Words = [.. words.OrderBy(w => w.Left)],
        };
        var key = new StringBuilder();
        foreach (DatasheetWord word in line.Words)
        {
            line.Offsets.Add(key.Length);
            key.Append(Normalize(word.Text).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant());
        }

        line.Key = key.ToString();
        return line;
    }

    private static string Normalize(string text) =>
        text.Replace('–', '-').Replace('—', '-').Replace('−', '-').Replace('～', '~').Replace('℃', 'C').Replace('º', '°');

    #endregion

    #region Candidates

    /// <summary>The words that may hold the values of a label: right of the label, on its line or a neighbouring one.</summary>
    private sealed record Candidate(Line Label, Line ValueLine, List<DatasheetWord> Tokens, int Order)
    {
        public string Source => ReferenceEquals(Label, ValueLine) ? Label.Text : $"{Label.Text} / {ValueLine.Text}";
    }

    private static IEnumerable<Candidate> FindCandidates(List<Line> lines, Regex[] patterns)
    {
        int order = 0;
        foreach (Regex pattern in patterns)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                Match match = pattern.Match(line.Key);
                if (!match.Success)
                {
                    continue;
                }

                int labelEnd = WordIndexAt(line, match.Index + match.Length - 1);
                if (!pattern.ToString().Contains("coeff", StringComparison.Ordinal) && BelongsToCoefficient(line, WordIndexAt(line, match.Index)))
                {
                    continue;
                }

                double labelRight = line.Words[labelEnd].Right;
                List<DatasheetWord> same = ValueTokens(MergeFragments(line.Words.Skip(labelEnd + 1)));
                if (same.Count > 0)
                {
                    yield return new Candidate(line, line, same, order++);
                    continue;
                }

                // Values next to a label that is vertically centred on two lines are on a neighbouring line.
                foreach (Line neighbour in lines
                    .Where(l => l.Page == line.Page && !ReferenceEquals(l, line)
                        && Math.Abs(l.CenterY - line.CenterY) <= 1.6 * Math.Max(l.Height, line.Height))
                    .OrderBy(l => Math.Abs(l.CenterY - line.CenterY)))
                {
                    List<DatasheetWord> tokens = ValueTokens(MergeFragments(neighbour.Words.Where(w => w.Left >= labelRight - 1)));
                    if (tokens.Count > 0 && !HasOwnLabel(neighbour, labelRight) && !s_peakValue.IsMatch(neighbour.Key))
                    {
                        yield return new Candidate(line, neighbour, tokens, order++);
                        break;
                    }
                }
            }
        }
    }

    private static bool IsCoefficient(string key) => key.Contains("koeff", StringComparison.Ordinal) || key.Contains("coeff", StringComparison.Ordinal);

    // A label is part of a temperature coefficient ("Temperaturkoeffizient von VOC") when a coefficient word precedes
    // it without a value in between; "… –0,24 %/K Maximale Absicherung 20 A" is a separate label on the same line.
    private static bool BelongsToCoefficient(Line line, int labelStart)
    {
        for (int i = labelStart; i >= 0; i--)
        {
            string word = Normalize(line.Words[i].Text).ToLowerInvariant();
            if (IsCoefficient(word))
            {
                return true;
            }

            if (i < labelStart && ValueTokens([line.Words[i]]).Count > 0)
            {
                return false;
            }
        }

        return false;
    }

    // A neighbouring line with its own label (text left of the values) belongs to another row.
    private static bool HasOwnLabel(Line line, double labelRight) =>
        line.Words.Any(w => w.Right < labelRight && w.Text.Any(char.IsLetter) && w.Text.Length > 2);

    private static int WordIndexAt(Line line, int charIndex)
    {
        int index = 0;
        for (int i = 0; i < line.Offsets.Count; i++)
        {
            if (line.Offsets[i] <= charIndex)
            {
                index = i;
            }
        }

        return index;
    }

    // Joins number fragments that the PDF split into words, for example "27" ".3/26.1" into "27.3/26.1".
    private static List<DatasheetWord> MergeFragments(IEnumerable<DatasheetWord> words)
    {
        var result = new List<DatasheetWord>();
        foreach (DatasheetWord word in words)
        {
            if (result.Count > 0 && word.Text.Length > 1 && word.Text[0] is '.' or ',' && char.IsDigit(word.Text[1])
                && result[^1] is { } previous && char.IsDigit(previous.Text[^1]) && word.Left - previous.Right < previous.Height)
            {
                result[^1] = previous with { Text = previous.Text + word.Text, Right = word.Right };
            }
            else
            {
                result.Add(word);
            }
        }

        return result;
    }

    private static List<DatasheetWord> ValueTokens(IEnumerable<DatasheetWord> words) =>
        [.. words.Where(w => Normalize(w.Text) is { Length: > 0 } t && (char.IsDigit(t[0]) || (t.Length > 1 && t[0] is '-' or '+' or '<' or '>' or '≤' && char.IsDigit(t[1]))))];

    #endregion

    #region Columns

    private sealed record Columns(IReadOnlyList<string> Names, IReadOnlyList<double> Centers)
    {
        public static Columns Single { get; } = new([string.Empty], []);

        public int Count => Names.Count;

        public double Tolerance => Centers.Count < 2 ? 0 : 0.5 * Centers.Zip(Centers.Skip(1), (a, b) => Math.Abs(b - a)).Min();
    }

    // Columns at the values of a row (Pmax of a module, energy of a battery); headings are model-like words above it.
    // Without heading a column is named after its value and the fallback unit, or stays empty without one.
    private static Columns ModuleColumns(List<Line> lines, Candidate? pmax, string? fallbackUnit)
    {
        if (pmax is null)
        {
            return Columns.Single;
        }

        List<DatasheetWord> numbers = [.. pmax.Tokens.Where(t => TryNumber(t.Text, out _))];
        if (numbers.Count < 2)
        {
            return Columns.Single;
        }

        double[] centers = [.. numbers.Select(n => n.CenterX)];
        double tolerance = new Columns([], centers).Tolerance;

        // Column headings: words with letters and digits above the Pmax row, aligned with the columns.
        var names = new string[centers.Length];
        IEnumerable<Line> above = lines
            .Where(l => l.Page == pmax.ValueLine.Page && l.CenterY > pmax.ValueLine.CenterY && l.CenterY - pmax.ValueLine.CenterY < 8 * pmax.ValueLine.Height)
            .OrderBy(l => l.CenterY);
        foreach (Line line in above.Reverse())
        {
            for (int c = 0; c < centers.Length; c++)
            {
                DatasheetWord? word = line.Words
                    .Where(w => Math.Abs(w.CenterX - centers[c]) <= tolerance && IsModelLike(w.Text))
                    .OrderBy(w => Math.Abs(w.CenterX - centers[c]))
                    .FirstOrDefault();
                if (word is not null)
                {
                    names[c] = Join(names[c], word.Text);
                }
            }
        }

        return new Columns([.. names.Select((n, i) => string.IsNullOrEmpty(n) ? (fallbackUnit is null ? string.Empty : numbers[i].Text + fallbackUnit) : n)], centers);
    }

    private static Columns InverterColumns(List<Line> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            List<DatasheetWord> models = [.. lines[i].Words.Where(w => IsModelLike(w.Text) && w.Text.Length >= 6)];
            if (models.Count < 2)
            {
                continue;
            }

            double[] centers = [.. models.Select(m => m.CenterX)];
            string[] names = [.. models.Select(m => m.Text)];
            double tolerance = new Columns([], centers).Tolerance;

            // A heading continued on a following line, for example "SUN-18K-SG05LP3" and "-EU-SM2".
            foreach (Line next in lines.Skip(i + 1).Take(3).Where(l => l.Page == lines[i].Page))
            {
                for (int c = 0; c < centers.Length; c++)
                {
                    DatasheetWord? part = next.Words.FirstOrDefault(w => Math.Abs(w.CenterX - centers[c]) <= tolerance && w.Text.StartsWith('-'));
                    if (part is not null)
                    {
                        names[c] += part.Text;
                    }
                }
            }

            return new Columns(names, centers);
        }

        return Columns.Single;
    }

    private static bool IsModelLike(string text) =>
        text.Any(char.IsLetter) && text.Any(char.IsDigit) && ModelPattern().IsMatch(text) && !UnitPattern().IsMatch(text);

    private static string Join(string? first, string second) =>
        string.IsNullOrEmpty(first) ? second : second.StartsWith('-') || first.EndsWith('-') ? first + second : first + "-" + second;

    #endregion

    #region Values

    private static DatasheetValue?[] ReadColumns(List<Line> lines, Regex[] patterns, Columns columns, Func<string, string?> read)
    {
        DatasheetValue?[]? shared = null;
        foreach (Candidate candidate in FindCandidates(lines, patterns))
        {
            var perColumn = new DatasheetValue?[columns.Count];
            int found = 0;
            if (columns.Count > 1)
            {
                for (int c = 0; c < columns.Count; c++)
                {
                    DatasheetWord? token = candidate.Tokens
                        .Where(t => Math.Abs(t.CenterX - columns.Centers[c]) <= columns.Tolerance)
                        .OrderBy(t => Math.Abs(t.CenterX - columns.Centers[c]))
                        .FirstOrDefault();
                    if (token is not null && read(token.Text) is { } text)
                    {
                        perColumn[c] = new DatasheetValue(text, candidate.Source);
                        found++;
                    }
                }

                // Values per column are preferred; a single value spans all columns (merged table cell).
                if (found >= Math.Min(2, columns.Count))
                {
                    return perColumn;
                }
            }

            if (shared is null && candidate.Tokens.Count >= 1 && (columns.Count == 1 || candidate.Tokens.Count == 1)
                && read(candidate.Tokens[0].Text) is { } single)
            {
                shared = Shared(columns, new DatasheetValue(single, candidate.Source));
                if (columns.Count == 1)
                {
                    return shared;
                }
            }
        }

        return shared ?? new DatasheetValue?[columns.Count];
    }

    private static DatasheetValue? FirstValue(List<Line> lines, Regex[] patterns, Func<string, string?> read, bool sameLineOnly)
    {
        foreach (Candidate candidate in FindCandidates(lines, patterns))
        {
            if (sameLineOnly && !ReferenceEquals(candidate.Label, candidate.ValueLine))
            {
                continue;
            }

            // A coefficient stated in mV/°C or mA/°C is not a percentage; it stays empty.
            int index = candidate.ValueLine.Words.IndexOf(candidate.Tokens[0]);
            string after = index + 1 < candidate.ValueLine.Words.Count ? candidate.ValueLine.Words[index + 1].Text : string.Empty;
            if (candidate.Tokens[0].Text.Contains("mV", StringComparison.OrdinalIgnoreCase) || candidate.Tokens[0].Text.Contains("mA", StringComparison.OrdinalIgnoreCase)
                || after.StartsWith("mV", StringComparison.OrdinalIgnoreCase) || after.StartsWith("mA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (read(candidate.Tokens[0].Text) is { } text)
            {
                return new DatasheetValue(text, candidate.Source);
            }
        }

        return null;
    }

    // Module leads: "Kabel Photovoltaikkabel: 4,0 mm2" and on the same or the next lines "Hochformat: 1100/1100 mm" or
    // "(+) 1200 mm (-) 1200 mm"; the portrait lengths are preferred over the landscape ones.
    private static (DatasheetValue? Area, DatasheetValue? Plus, DatasheetValue? Minus) ReadModuleCable(List<Line> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string text = Normalize(lines[i].Text);
            Match area = CableAreaPattern().Match(text);
            if (!s_cableLabel.IsMatch(lines[i].Key) || !area.Success || !TryNumber(area.Groups[1].Value, out double mm2))
            {
                continue;
            }

            DatasheetValue areaValue = new(Format(mm2), lines[i].Text);
            List<Line> near = [.. lines.Skip(i).Take(4).Where(l => l.Page == lines[i].Page)];
            Line? lengthLine = near.FirstOrDefault(l => s_portrait.IsMatch(l.Key) && CableLengthPattern().IsMatch(Normalize(l.Text)))
                ?? near.FirstOrDefault(l => CableLengthPattern().IsMatch(Normalize(l.Text)) || SignedLengthPattern().IsMatch(Normalize(l.Text)));
            if (lengthLine is null)
            {
                return (areaValue, null, null);
            }

            string lengthText = Normalize(lengthLine.Text);
            Match signed = SignedLengthPattern().Match(lengthText);
            Match length = signed.Success ? signed : CableLengthPattern().Match(s_portrait.IsMatch(lengthLine.Key) ? lengthText[Math.Max(0, PortraitIndex(lengthText))..] : lengthText);
            if (length.Success && TryNumber(length.Groups[1].Value, out double plus) && TryNumber(length.Groups[2].Value, out double minus))
            {
                return (areaValue, new DatasheetValue(Format(plus), lengthLine.Text), new DatasheetValue(Format(minus), lengthLine.Text));
            }

            return (areaValue, null, null);
        }

        return (null, null, null);
    }

    private static int PortraitIndex(string text)
    {
        Match match = PortraitWordPattern().Match(text);
        return match.Success ? match.Index : 0;
    }

    // A range whose numbers may be separate words: "44,8 V ~ 57,6 V" or "44.8-57.6V".
    private static DatasheetValue? FirstRange(List<Line> lines, Regex[] patterns)
    {
        foreach (Candidate candidate in FindCandidates(lines, patterns))
        {
            int start = candidate.ValueLine.Words.IndexOf(candidate.Tokens[0]);
            string text = Normalize(string.Join(string.Empty, candidate.ValueLine.Words.Skip(start).Take(6).Select(w => w.Text)));
            Match match = SpacedRangePattern().Match(text);
            if (match.Success && TryNumber(match.Groups[1].Value, out double low) && TryNumber(match.Groups[2].Value, out double high) && low < high)
            {
                return new DatasheetValue($"{Format(low)}-{Format(high)}", candidate.Source);
            }
        }

        return null;
    }

    private static DatasheetValue?[] ReadOperatingTemperature(List<Line> lines, Columns columns)
    {
        foreach (Candidate candidate in FindCandidates(lines, s_operatingTemperature))
        {
            int start = candidate.ValueLine.Words.IndexOf(candidate.Tokens[0]);
            string text = Normalize(string.Join(" ", candidate.ValueLine.Words.Skip(start).Take(5).Select(w => w.Text)));
            Match match = TemperatureRangePattern().Match(text);
            if (match.Success && TryNumber(match.Groups[1].Value, out double min) && TryNumber(match.Groups[2].Value, out double max) && min < max)
            {
                return Shared(columns, new DatasheetValue($"{Format(min)}|{Format(max)}", candidate.Source));
            }
        }

        return new DatasheetValue?[columns.Count];
    }

    private static DatasheetValue?[] Shared(Columns columns, DatasheetValue? value) => [.. Enumerable.Repeat(value, columns.Count)];

    private static string? NumberOf(string token) => TryNumber(token, out double value) ? Format(value) : null;

    // A number whose unit, when it is part of the token, is the expected one: "235Ah" for a capacity, not "12,03kWh".
    private static string? UnitOf(string token, string unit)
    {
        Match match = NumberPattern().Match(Normalize(token).Trim());
        return match.Success && (match.Groups[2].Value.Length == 0 || match.Groups[2].Value == unit) ? NumberOf(token) : null;
    }

    private static string? IntegerOf(string token) => TryNumber(token, out double value) && value == Math.Floor(value) && value > 0 ? Format(value) : null;

    // "27.3/26.1": AC values at 220/380 V and 230/400 V (or input/output); the last one is taken.
    private static string? LastOf(string token) => NumberOf(Normalize(token).Split('/')[^1]);

    // "36+36" or "20+20(A)": the value of one MPPT.
    private static string? PerMpptOf(string token)
    {
        string first = Normalize(token).Split('+', '/')[0];
        return TryNumber(first, out double value) ? Format(value) : null;
    }

    // "160-650", "200V-650V" or "250~650V".
    private static string? RangeOf(string token)
    {
        Match match = RangePattern().Match(Normalize(token));
        return match.Success && TryNumber(match.Groups[1].Value, out double low) && TryNumber(match.Groups[2].Value, out double high) && low < high
            ? $"{Format(low)}-{Format(high)}"
            : null;
    }

    // "2/2+2": 2 MPPTs with up to 2 strings each; "2": 2 MPPTs.
    private static (string Mppts, string? Strings)? MpptAndStrings(string token)
    {
        string[] parts = Normalize(token).Split('/');
        if (!TryNumber(parts[0], out double mppts) || mppts < 1 || mppts != Math.Floor(mppts))
        {
            return null;
        }

        string? strings = null;
        if (parts.Length > 1)
        {
            double[] counts = [.. parts[1].Split('+').Select(p => TryNumber(p, out double n) ? n : 0)];
            if (counts.Length > 0 && counts.Max() >= 1)
            {
                strings = Format(counts.Max());
            }
        }

        return (Format(mppts), strings);
    }

    /// <summary>
    /// Reads a number with an optional unit ("800V", "7.5kW", "-0,30", "22,5%", "5.12kWh", "100Ah"); kW and kVA are
    /// converted to W, Wh to kWh.
    /// </summary>
    internal static bool TryNumber(string token, out double value)
    {
        value = 0;
        Match match = NumberPattern().Match(Normalize(token).Trim());
        if (!match.Success)
        {
            return false;
        }

        string number = match.Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        string unit = match.Groups[2].Value;
        if (unit == "Wh")
        {
            value /= 1000;
        }
        else if (unit.StartsWith('k') && unit != "kWh")
        {
            value *= 1000;
        }

        return true;
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string? FindManufacturer(IReadOnlyList<DatasheetWord> words, IEnumerable<string>? manufacturers)
    {
        string text = string.Concat(words.Select(w => w.Text)).ToLowerInvariant();
        return (manufacturers ?? []).Concat(KnownManufacturers)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => (Name: m, Count: Regex.Matches(text, Regex.Escape(m.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant())).Count))
            .Where(m => m.Count > 0)
            .OrderByDescending(m => m.Count)
            .Select(m => m.Name)
            .FirstOrDefault();
    }

    private static Regex Re(string pattern) => new(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [GeneratedRegex(@"^([+-]?\d+(?:[.,]\d+)?)\s*(kWh|Wh|Ah|k?W[p]?|k?VA|V|A|%|°?C|K|mm|kg)?[*)]?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*V?\s*[-~]\s*(\d+(?:[.,]\d+)?)\s*V?$", RegexOptions.CultureInvariant)]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*mm[2²]", RegexOptions.CultureInvariant)]
    private static partial Regex CableAreaPattern();

    [GeneratedRegex(@"(\d{2,4})\s*/\s*(\d{2,4})\s*mm(?![2²])", RegexOptions.CultureInvariant)]
    private static partial Regex CableLengthPattern();

    [GeneratedRegex(@"\(\+\)\s*(\d{2,4})\s*mm.*?\(-\)\s*(\d{2,4})\s*mm", RegexOptions.CultureInvariant)]
    private static partial Regex SignedLengthPattern();

    [GeneratedRegex(@"(?i)hochformat|portrait", RegexOptions.CultureInvariant)]
    private static partial Regex PortraitWordPattern();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)V?[-~](\d+(?:[.,]\d+)?)V?", RegexOptions.CultureInvariant)]
    private static partial Regex SpacedRangePattern();

    [GeneratedRegex(@"([+-]?\d+(?:[.,]\d+)?)\s*(?:°?C)?\s*(?:to|bis|~|\.\.\.|…|-)\s*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex TemperatureRangePattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-/+_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModelPattern();

    [GeneratedRegex(@"^\d+([.,]\d+)?(k?Wh|Ah|k?W|k?VA|V|A|mm|kg|Hz)$", RegexOptions.CultureInvariant)]
    private static partial Regex UnitPattern();

    #endregion
}
