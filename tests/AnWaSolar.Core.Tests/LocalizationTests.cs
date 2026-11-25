using System.Globalization;
using System.Text.RegularExpressions;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Localization;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>The language switch changes a static state; these tests do not run in parallel with others.</summary>
[CollectionDefinition(nameof(LanguageCollection), DisableParallelization = true)]
public sealed class LanguageCollection;

/// <summary>The German texts and the formatting of translated texts.</summary>
[Collection(nameof(LanguageCollection))]
public sealed partial class LocalizationTests
{
    // Number and alignment of a placeholder; the format may differ (dates are written differently in German).
    [GeneratedRegex(@"\{\d+(,-?\d+)?(?=[:}])")]
    private static partial Regex Placeholder();

    [Fact]
    public void Every_German_text_has_the_placeholders_of_its_English_text()
    {
        Assert.NotEmpty(L.GermanTexts);
        foreach ((string english, string german) in L.GermanTexts)
        {
            string[] expected = [.. Placeholder().Matches(english).Select(m => m.Value).Order(StringComparer.Ordinal)];
            string[] actual = [.. Placeholder().Matches(german).Select(m => m.Value).Order(StringComparer.Ordinal)];
            Assert.True(expected.SequenceEqual(actual), $"\"{english}\" → \"{german}\"");
            Assert.False(string.IsNullOrWhiteSpace(german), english);
        }
    }

    [Fact]
    public void Interpolated_texts_keep_formats_and_braces()
    {
        double current = 28.7;
        int count = 3;

        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0} limits violated", count), L.F($"{count} limits violated"));
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "x {{ {0:F2} A }}", current), L.F($"x {{ {current:F2} A }}"));
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "[{0,5}]", count), L.F($"[{count,5}]"));
    }

    [Fact]
    public void German_results_and_report_use_the_German_texts_and_number_format()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            L.SetLanguage(L.German);
            CultureInfo.CurrentCulture = L.Culture;
            StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11, 3), Aiko(2, 11)], Parameters());

            ResultOverview overview = ResultPresenter.Present(result);
            string report = ReportBuilder.Build(result, Parameters(), new DateTime(2026, 11, 25, 9, 30, 0));

            Assert.Equal("3 Grenzen verletzt", overview.Headline);
            Assert.Contains(overview.Mppts[0].Rows, r => r.Quantity == "Isc aller Strings");
            Assert.Contains("Erstellt am 25.11.2026 09:30", report, StringComparison.Ordinal);
            Assert.Contains("## Wechselrichter", report, StringComparison.Ordinal);
            Assert.Contains("Stringspannung Voc: min=", report, StringComparison.Ordinal);
            Assert.Matches(@"\d,\d V", report); // decimal comma
        }
        finally
        {
            L.SetLanguage(L.English);
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Unknown_language_and_missing_texts_fall_back_to_English()
    {
        try
        {
            L.SetLanguage("fr");
            Assert.Equal(L.English, L.Language);
            L.SetLanguage(L.German);
            Assert.Equal("Some text without translation", L.T("Some text without translation"));
            Assert.Equal("Wechselrichter", L.T("Inverter"));
        }
        finally
        {
            L.SetLanguage(L.English);
        }
    }
}
