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

    private static readonly Regex[] s_operatingTemperature = [Re(@"(?<!nenn)betriebstemperatur|operatingtemperature|operationaltemperature|operatingambienttemperature")];

    private static readonly Dictionary<string, Regex[]> s_inverterLabels = new()
    {
        [DatasheetFields.MaxDcPowerW] = [Re(@"max\.?dc-?eingangsleistung|max\.?dcinputpower|max(imum)?\.?dcpower"), Re(@"max\.?pv-?eingangsleistung|max\.?pvinputpower")],
        [DatasheetFields.MaxDcVoltageV] = [Re(@"max\.?dc-?eingangsspannung|max(imum)?\.?(dc|pv)inputvoltage|max\.?pv-?eingangsspannung|maximuminputvoltage")],
        [DatasheetFields.StartVoltageV] = [Re(@"startspannung|start-?upvoltage|startvoltage|einschaltspannung")],
        [DatasheetFields.MpptRangeV] = [Re(@"mppt?-?spannungsbereich|mppt?-?voltagerange?|mppt?operatingvoltagerange|mpp-?bereich")],
        [DatasheetFields.NominalDcVoltageV] = [Re(@"nenn-?dc-?eingangsspannung|(rated|nominal)(dc)?inputvoltage|nenneingangsspannung")],
        [DatasheetFields.MaxInputCurrentA] = [Re(@"max\.?betriebs-?pv-?eingangsstrom|max\.?operatingpvinputcurrent|max(imum)?\.?(pv|dc)?inputcurrent(?!.*short)|max\.?(pv-?)?eingangsstrom")],
        [DatasheetFields.MaxShortCircuitCurrentA] = [Re(@"eingangs-?kurzschlus+strom|inputshort-?circuitcurrent|max(imum)?\.?short-?circuitcurrent|max\.?kurzschlus+strom|iscpv")],
    };

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
            columns = ModuleColumns(lines, pmax);
            foreach ((string key, Regex[] patterns) in s_moduleLabels)
            {
                values[key] = ReadColumns(lines, patterns, columns, NumberOf);
            }

            foreach ((string key, Regex[] patterns) in s_coefficientLabels)
            {
                values[key] = Shared(columns, FirstValue(lines, patterns, NumberOf, sameLineOnly: true));
            }

            DatasheetValue?[] temperature = ReadOperatingTemperature(lines, columns);
            values[DatasheetFields.OperatingMinC] = [.. temperature.Select(v => v is null ? null : v with { Text = v.Text.Split('|')[0] })];
            values[DatasheetFields.OperatingMaxC] = [.. temperature.Select(v => v is null ? null : v with { Text = v.Text.Split('|')[1] })];
        }
        else
        {
            columns = InverterColumns(lines);
            foreach ((string key, Regex[] patterns) in s_inverterLabels)
            {
                Func<string, string?> read = key switch
                {
                    DatasheetFields.MpptRangeV => RangeOf,
                    DatasheetFields.MaxInputCurrentA or DatasheetFields.MaxShortCircuitCurrentA => PerMpptOf,
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
        }

        string? manufacturer = FindManufacturer(words, manufacturers);
        values[DatasheetFields.Manufacturer] = Shared(columns, manufacturer is null ? null : new DatasheetValue(manufacturer, "Manufacturer name in the datasheet"));
        values[DatasheetFields.Model] = [.. columns.Names.Select(n => string.IsNullOrWhiteSpace(n) ? null : new DatasheetValue(n, "Column heading in the datasheet"))];
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
                if (IsCoefficient(line.Key) && !pattern.ToString().Contains("coeff", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = pattern.Match(line.Key);
                if (!match.Success)
                {
                    continue;
                }

                int labelEnd = WordIndexAt(line, match.Index + match.Length - 1);
                double labelRight = line.Words[labelEnd].Right;
                List<DatasheetWord> same = ValueTokens(line.Words.Skip(labelEnd + 1));
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
                    List<DatasheetWord> tokens = ValueTokens(neighbour.Words.Where(w => w.Left >= labelRight - 1));
                    if (tokens.Count > 0 && !HasOwnLabel(neighbour, labelRight))
                    {
                        yield return new Candidate(line, neighbour, tokens, order++);
                        break;
                    }
                }
            }
        }
    }

    private static bool IsCoefficient(string key) => key.Contains("koeff", StringComparison.Ordinal) || key.Contains("coeff", StringComparison.Ordinal);

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

    private static Columns ModuleColumns(List<Line> lines, Candidate? pmax)
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

        return new Columns([.. names.Select((n, i) => string.IsNullOrEmpty(n) ? numbers[i].Text + " W" : n)], centers);
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

    private static string? IntegerOf(string token) => TryNumber(token, out double value) && value == Math.Floor(value) && value > 0 ? Format(value) : null;

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

    /// <summary>Reads a number with an optional unit ("800V", "7.5kW", "-0,30", "22,5%"); kW and kVA are converted to W.</summary>
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

        if (match.Groups[2].Value.StartsWith('k'))
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

    [GeneratedRegex(@"^([+-]?\d+(?:[.,]\d+)?)\s*(k?W[p]?|k?VA|V|A|%|°?C|K|mm|kg)?[*)]?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^(\d+(?:[.,]\d+)?)\s*V?\s*[-~]\s*(\d+(?:[.,]\d+)?)\s*V?$", RegexOptions.CultureInvariant)]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"([+-]?\d+(?:[.,]\d+)?)\s*(?:°?C)?\s*(?:to|bis|~|\.\.\.|…|-)\s*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex TemperatureRangePattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.\-/+_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModelPattern();

    [GeneratedRegex(@"^\d+([.,]\d+)?(k?W|k?VA|V|A|mm|kg|Hz)$", RegexOptions.CultureInvariant)]
    private static partial Regex UnitPattern();

    #endregion
}
