using AnWaSolar.Core.Datasheets;

using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AnWaSolar.Core.Tests;

/// <summary>
/// Datasheet recognition with the layout and values of real datasheets: Trina Vertex S+ TSM-NEG9R.28 (power classes
/// 425–450 W, German) and Deye SUN-14/15/16/18/20K-SG05LP3-EU-SM2 (German).
/// </summary>
public sealed class DatasheetParserTests
{
    private const double FontSize = 7;

    // Words of one table row; x is the left edge of each word.
    private static IEnumerable<DatasheetWord> Row(double y, params (double X, string Text)[] items) =>
        items.SelectMany(item => item.Text.Split(' ').Select((part, i) =>
        {
            double left = item.X + item.Text.Split(' ').Take(i).Sum(p => (p.Length + 1) * FontSize * 0.5);
            return new DatasheetWord(part, 2, left, left + part.Length * FontSize * 0.5, y, y + FontSize);
        }));

    private static (double, string)[] Columns(params string[] values) => [.. values.Select((v, i) => (200.0 + i * 40, v))];

    internal static List<(double X, double Y, string Text)> TrinaLayout()
    {
        var rows = new List<(double, double, string)>();
        void Add(double y, string label, params string[] values)
        {
            rows.Add((40, y, label));
            rows.AddRange(values.Select((v, i) => (200.0 + i * 40, y, v)));
        }

        Add(520, "TSM-NEG9R.28 Datenblatt Trina Solar");
        Add(500, string.Empty, "TSM-425", "TSM-430", "TSM-435", "TSM-440", "TSM-445", "TSM-450");
        Add(490, "ELEKTRISCHE DATEN (STC)", "NEG9R.28", "NEG9R.28", "NEG9R.28", "NEG9R.28", "NEG9R.28", "NEG9R.28");
        Add(470, "Nominalleistung-PMAX (Wp)*", "425", "430", "435", "440", "445", "450");
        Add(460, "Leistungstoleranz-P MAX (W)", "0/+5", "0/+5", "0/+5", "0/+5", "0/+5");
        Add(450, "Spannung im MPP-UMPP (V)", "42,9", "43,2", "43,6", "44,0", "44,3", "44,6");
        Add(440, "Strom im MPP-IMPP (A)", "9,92", "9,96", "9,99", "10,01", "10,05", "10,09");
        rows.Add((460, 440, "Rückseite 1,6 mm"));
        Add(430, "Leerlaufspannung-U OC (V)", "50,9", "51,4", "51,8", "52,2", "52,6", "52,9");
        Add(420, "Kurzschlusstrom-I SC (A)", "10,56", "10,59", "10,64", "10,67", "10,71", "10,74");
        Add(410, "Modulwirkungsgrad m (%)", "21,3", "21,5", "21,8", "22,0", "22,3", "22,5");
        Add(390, "ELECTRICAL DATA (NOCT)");
        Add(380, "Nominalleistung-PMAX (Wp)", "324", "328", "332", "335", "339", "343");
        Add(370, "Leerlaufspannung-UOC (V)", "48,2", "48,7", "49,1", "49,4", "49,8", "50,1");
        Add(350, "NOCT(Nennbetriebstemperatur der Zelle) 43 °C (±2 K)");
        rows.Add((300, 350, "Betriebstemperatur –40 to +85 ºC"));
        Add(340, "Temperaturkoeffizient von PMAX –0,30 %/ K");
        rows.Add((300, 340, "Maximale Systemspannung 1500 V DC (IEC)"));
        Add(330, "Temperaturkoeffizient von VOC –0,24 %/ K");
        Add(320, "Temperaturkoeffizient von ISC 0,04 %/ K");
        Add(300, "© 2023 Trina Solar Limited. www.trinasolar.com");
        return rows;
    }

    private static List<DatasheetWord> Words(List<(double X, double Y, string Text)> layout) =>
        [.. layout.Where(r => r.Text.Length > 0).SelectMany(r => Row(r.Y, (r.X, r.Text)))];

    private static List<DatasheetWord> DeyeWords() =>
    [
        .. Row(700, (40, "Technische Daten")),
        .. Row(690, Columns("SUN-14K-SG05LP3", "SUN-15K-SG05LP3", "SUN-16K-SG05LP3", "SUN-18K-SG05LP3", "SUN-20K-SG05LP3")),
        .. Row(682, (40, "Modell")),
        .. Row(674, Columns("-EU-SM2", "-EU-SM2", "-EU-SM2", "-EU-SM2", "-EU-SM2")),
        .. Row(660, (40, "PV String Eingangsdaten")),
        .. Row(650, [(40, "Max. PV Access Power (W)"), .. Columns("28000", "30000", "32000", "36000", "40000")]),
        .. Row(640, [(40, "Max.DC-Eingangsleistung (W)"), .. Columns("22400", "24000", "25600", "28800", "32000")]),
        .. Row(630, (40, "Max.DC-Eingangsspannung (V)"), (280, "800")),
        .. Row(620, (40, "Startspannung (V)"), (280, "160")),
        .. Row(610, (40, "MPPT-Spannungsbereich (V)"), (280, "160-650")),
        .. Row(600, (40, "Nenn-DC-Eingangsspannung (V)"), (280, "550")),
        .. Row(590, (40, "Max. Betriebs-PV-Eingangsstrom (A)"), (280, "36+36")),
        .. Row(580, (40, "Max. Eingangs-Kurzschlussstrom (A)"), (280, "54+54")),
        .. Row(572, (40, "Anzahl der MPP Trackers/")),
        .. Row(568, (280, "2/2+2")),
        .. Row(564, (40, "Anzahl der Strings MPP Tracker")),
        .. Row(556, [(40, "Nenn-AC-Eingangs-/Ausgangswirkleistung"), .. Columns("14000", "15000", "16000", "18000", "20000")]),
        .. Row(548, [(40, "Max. AC-Eingangs-/Ausgangs-Scheinleistung"), .. Columns("15400", "16500", "17600", "19800", "22000")]),
        .. Row(544, (40, "AC-Eingangs-/Ausgangsnennstrom (A)"), (200, "21 .3/20,3"), (240, "22.8/21.8"), (280, "24 .3/23.2"), (320, "27 .3/26.1"), (360, "30 .4/29")),
        .. Row(542, (40, "Form des Netzanschlusses 3L+N+PE")),
        .. Row(540, (40, "Dreiphasen-Hybrid-Wechselrichter, Wechselrichter-Topologie, AC-Ausgang, Netz, www.deyeinverter.com")),
    ];

    [Fact]
    public void Module_power_classes_and_stc_values_are_read_per_column()
    {
        List<DatasheetWord> words = Words(TrinaLayout());

        Assert.Equal(DatasheetKind.Module, DatasheetParser.DetectKind(words));
        DatasheetParseResult result = DatasheetParser.Parse(words, DatasheetKind.Module, ["Trina", "AIKO"]);

        Assert.Equal("Trina", result.Manufacturer);
        Assert.Equal(6, result.Variants.Count);
        int tsm445 = 4;
        Assert.Equal("TSM-445-NEG9R.28", result.Variants[tsm445]);
        Assert.Equal("445", result.Get(DatasheetFields.PmaxW, tsm445)!.Text);
        Assert.Equal("44.3", result.Get(DatasheetFields.VmppV, tsm445)!.Text);
        Assert.Equal("10.05", result.Get(DatasheetFields.ImppA, tsm445)!.Text);
        Assert.Equal("52.6", result.Get(DatasheetFields.VocV, tsm445)!.Text); // STC, not NOCT (49.8)
        Assert.Equal("10.71", result.Get(DatasheetFields.IscA, tsm445)!.Text);
        Assert.Equal("22.3", result.Get(DatasheetFields.EfficiencyPct, tsm445)!.Text);
        Assert.Equal("10.09", result.Get(DatasheetFields.ImppA, 5)!.Text); // "1,6 mm" right of the table is ignored
    }

    [Fact]
    public void Module_temperature_values_keep_their_sign()
    {
        DatasheetParseResult result = DatasheetParser.Parse(Words(TrinaLayout()), DatasheetKind.Module);

        Assert.Equal("-0.3", result.Get(DatasheetFields.PmaxCoeff, 0)!.Text);
        Assert.Equal("-0.24", result.Get(DatasheetFields.VocCoeff, 0)!.Text);
        Assert.Equal("0.04", result.Get(DatasheetFields.IscCoeff, 0)!.Text);
        Assert.Null(result.Get(DatasheetFields.VmppCoeff, 0));
        Assert.Equal("-40", result.Get(DatasheetFields.OperatingMinC, 0)!.Text);
        Assert.Equal("85", result.Get(DatasheetFields.OperatingMaxC, 0)!.Text);
        Assert.Contains("Temperaturkoeffizient von VOC", result.Get(DatasheetFields.VocCoeff, 0)!.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Inverter_models_and_shared_values_are_read()
    {
        List<DatasheetWord> words = DeyeWords();

        Assert.Equal(DatasheetKind.Inverter, DatasheetParser.DetectKind(words));
        DatasheetParseResult result = DatasheetParser.Parse(words, DatasheetKind.Inverter, ["Deye"]);

        Assert.Equal("Deye", result.Manufacturer);
        int sun18k = 3;
        Assert.Equal("SUN-18K-SG05LP3-EU-SM2", result.Variants[sun18k]);
        Assert.Equal("28800", result.Get(DatasheetFields.MaxDcPowerW, sun18k)!.Text); // DC input power, not PV access power
        Assert.Equal("800", result.Get(DatasheetFields.MaxDcVoltageV, sun18k)!.Text);
        Assert.Equal("160", result.Get(DatasheetFields.StartVoltageV, sun18k)!.Text);
        Assert.Equal("160-650", result.Get(DatasheetFields.MpptRangeV, sun18k)!.Text);
        Assert.Equal("550", result.Get(DatasheetFields.NominalDcVoltageV, sun18k)!.Text);
        Assert.Equal("36", result.Get(DatasheetFields.MaxInputCurrentA, sun18k)!.Text);
        Assert.Equal("54", result.Get(DatasheetFields.MaxShortCircuitCurrentA, sun18k)!.Text);
        Assert.Equal("2", result.Get(DatasheetFields.MpptCount, sun18k)!.Text);
        Assert.Equal("2", result.Get(DatasheetFields.MaxStringsPerMppt, sun18k)!.Text);
    }

    [Fact]
    public void Inverter_ac_values_take_the_230_400_v_value_and_join_split_numbers()
    {
        DatasheetParseResult result = DatasheetParser.Parse(DeyeWords(), DatasheetKind.Inverter, ["Deye"]);
        int sun18k = 3;

        Assert.Equal("18000", result.Get(DatasheetFields.RatedAcPowerW, sun18k)!.Text);
        Assert.Equal("19800", result.Get(DatasheetFields.MaxAcApparentPowerVA, sun18k)!.Text);
        Assert.Equal("26.1", result.Get(DatasheetFields.RatedAcCurrentA, sun18k)!.Text); // "27 .3/26.1"
        Assert.Equal("21.8", result.Get(DatasheetFields.RatedAcCurrentA, 1)!.Text);
        Assert.Equal("3", result.Get(DatasheetFields.AcPhases, sun18k)!.Text);
    }

    [Fact]
    public void Missing_values_stay_empty()
    {
        DatasheetParseResult result = DatasheetParser.Parse([.. Row(500, (40, "Some unrelated text 42"))], DatasheetKind.Inverter);

        Assert.Single(result.Variants);
        Assert.Null(result.Get(DatasheetFields.MaxDcVoltageV, 0));
        Assert.Null(result.Manufacturer);
    }

    [Theory]
    [InlineData("800V", 800)]
    [InlineData("7.5kW", 7500)]
    [InlineData("–0,30", -0.30)]
    [InlineData("22,5%", 22.5)]
    public void Numbers_with_units_and_decimal_commas_are_read(string token, double expected)
    {
        Assert.True(DatasheetParser.TryNumber(token, out double value));
        Assert.Equal(expected, value, 6);
    }

    [Fact]
    public void Control_characters_between_words_become_spaces()
    {
        Assert.Equal("Max. PV Input Voltage", PdfTextReader.Clean("Max.\u0001PV\u0001Input\u0001Voltage\u0001"));
    }

    [Fact]
    public void Words_are_read_from_a_pdf_file()
    {
        // A PDF with the Trina table, written with the PdfPig writer.
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4);
        PdfPageBuilder page = builder.AddPage(PageSize.A4);
        foreach ((double x, double y, string text) in TrinaLayout().Where(r => r.Text.Length > 0))
        {
            page.AddText(Ascii(text), FontSize, new PdfPoint(x, y), font);
        }

        string path = Path.Combine(Path.GetTempPath(), $"AnWaSolar-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(path, builder.Build());
            IReadOnlyList<DatasheetWord> words = PdfTextReader.Read(path);
            DatasheetParseResult result = DatasheetParser.Parse(words, DatasheetParser.DetectKind(words));

            Assert.Equal(DatasheetKind.Module, result.Kind);
            Assert.Equal("TSM-445-NEG9R.28", result.Variants[4]);
            Assert.Equal("52.6", result.Get(DatasheetFields.VocV, 4)!.Text);
            Assert.Equal("-0.24", result.Get(DatasheetFields.VocCoeff, 4)!.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // The standard fonts of the PdfPig writer contain ASCII only.
    private static string Ascii(string text) => text
        .Replace("ü", "ue", StringComparison.Ordinal).Replace('–', '-').Replace("º", string.Empty, StringComparison.Ordinal)
        .Replace("°", string.Empty, StringComparison.Ordinal).Replace("±", "+/-", StringComparison.Ordinal).Replace("©", "(c)", StringComparison.Ordinal);

    [Fact]
    public void A_file_that_is_no_pdf_is_reported()
    {
        string path = Path.Combine(Path.GetTempPath(), $"AnWaSolar-{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllText(path, "not a pdf");
            Assert.Throws<IOException>(() => PdfTextReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
