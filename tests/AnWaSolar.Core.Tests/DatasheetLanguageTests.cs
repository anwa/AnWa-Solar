using System.Text.Json;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Localization;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AnWaSolar.Core.Tests;

/// <summary>Datasheets per language: choice by the application language, language detection and the series files.</summary>
[Collection(nameof(LanguageCollection))]
public sealed class DatasheetLanguageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        L.SetLanguage(L.English);
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private JsonDataStore Store()
    {
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new Location(_root)));
        store.Load();
        return store;
    }

    private string Pdf(string name, string content)
    {
        string path = Path.Combine(_root, "source", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static ComponentSeries<Battery> SeF(Battery battery) => new() { Manufacturer = "Deye", Series = "SE-F", Variants = [battery] };

    private static Battery SeF16(JsonDataStore store) => store.Batteries.Single(b => b.Model == "SE-F16");

    private static string? Shown(JsonDataStore store) =>
        store.GetDatasheetPath(store.Batteries.Single(b => b.Model == "SE-F16")) is { } path ? File.ReadAllText(path) : null;

    private string SeriesFolder => Path.Combine(_root, "Data", "Batteries", "Deye");

    [Theory]
    [InlineData("de", "de.pdf")]
    [InlineData("en", "en.pdf")]
    public void The_datasheet_of_the_language_is_chosen_when_both_exist(string language, string expected)
    {
        var files = new Dictionary<string, string> { ["de"] = "de.pdf", ["en"] = "en.pdf" };

        Assert.Equal(expected, DatasheetLanguage.Pick(files, language));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void The_only_datasheet_is_chosen_whatever_the_language(string language)
    {
        Assert.Equal("de.pdf", DatasheetLanguage.Pick(new Dictionary<string, string> { ["de"] = "de.pdf" }, language));
        Assert.Equal("en.pdf", DatasheetLanguage.Pick(new Dictionary<string, string> { ["en"] = "en.pdf" }, language));
        Assert.Equal("old.pdf", DatasheetLanguage.Pick(new Dictionary<string, string> { [""] = "old.pdf" }, language));
    }

    [Fact]
    public void A_datasheet_without_language_comes_before_the_other_language()
    {
        var files = new Dictionary<string, string> { [""] = "old.pdf", ["en"] = "en.pdf" };

        Assert.Equal("old.pdf", DatasheetLanguage.Pick(files, "de"));
        Assert.Equal("en.pdf", DatasheetLanguage.Pick(files, "en"));
        Assert.Null(DatasheetLanguage.Pick(new Dictionary<string, string>(), "de"));
    }

    [Fact]
    public void Language_is_detected_from_the_words_of_the_datasheet()
    {
        string[] german = "Elektrische Daten Nennleistung Leerlaufspannung Kurzschlussstrom Wirkungsgrad Abmessungen Gewicht und mit".Split(' ');
        string[] english = "Electrical data Rated power Open-circuit voltage Short-circuit current Efficiency Dimensions Weight and with".Split(' ');

        Assert.Equal(L.German, DatasheetLanguage.Detect(german));
        Assert.Equal(L.English, DatasheetLanguage.Detect(english));
        Assert.Equal(DatasheetLanguage.NotStated, DatasheetLanguage.Detect([.. german, .. english]));
        Assert.Equal(DatasheetLanguage.NotStated, DatasheetLanguage.Detect([]));
    }

    [Fact]
    public void Series_keeps_a_German_and_an_English_datasheet_and_shows_the_one_of_the_language()
    {
        JsonDataStore store = Store();
        Battery seF16 = SeF16(store);
        store.SaveSeries(SeF(seF16), Pdf("SE-F de.pdf", "%PDF de"), datasheetLanguage: L.German);
        store.SaveSeries(SeF(seF16), Pdf("SE-F en.pdf", "%PDF en"), datasheetLanguage: L.English);

        Assert.Equal("%PDF en", Shown(store));
        string json = File.ReadAllText(Path.Combine(SeriesFolder, "SE-F.json"));
        Assert.Contains("\"Datasheet file (de)\": \"SE-F (de).pdf\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Datasheet file (en)\": \"SE-F (en).pdf\"", json, StringComparison.Ordinal);
        Assert.Equal([L.German, L.English], store.GetSeriesDatasheets<Battery>("Deye", "SE-F").Keys.Order());

        L.SetLanguage(L.German);
        store.Load();

        Assert.Equal("%PDF de", Shown(store));
    }

    [Fact]
    public void Only_datasheet_is_shown_in_the_other_language()
    {
        JsonDataStore store = Store();
        store.SaveSeries(SeF(SeF16(store)), Pdf("SE-F.pdf", "%PDF de"), datasheetLanguage: L.German);

        Assert.Equal("%PDF de", Shown(store));
    }

    [Fact]
    public void Datasheet_of_an_earlier_version_stays_beside_a_new_language()
    {
        JsonDataStore store = Store();
        Battery seF16 = SeF16(store);
        store.SaveSeries(SeF(seF16), Pdf("old.pdf", "%PDF old"));
        store.SaveSeries(SeF(seF16), Pdf("en.pdf", "%PDF en"), datasheetLanguage: L.English);

        Assert.Equal("%PDF en", Shown(store));
        L.SetLanguage(L.German);
        store.Load();
        Assert.Equal("%PDF old", Shown(store));
        Assert.True(File.Exists(Path.Combine(SeriesFolder, "SE-F.pdf")));
    }

    [Fact]
    public void Renaming_moves_all_datasheets_and_deleting_removes_them()
    {
        JsonDataStore store = Store();
        Battery seF16 = SeF16(store);
        store.SaveSeries(SeF(seF16), Pdf("de.pdf", "%PDF de"), datasheetLanguage: L.German);
        store.SaveSeries(SeF(seF16), Pdf("en.pdf", "%PDF en"), datasheetLanguage: L.English);

        store.SaveSeries(new ComponentSeries<Battery> { Manufacturer = "Deye", Series = "SE-F16 series", Variants = [seF16] }, null, ("Deye", "SE-F"));

        IReadOnlyDictionary<string, string> moved = store.GetSeriesDatasheets<Battery>("Deye", "SE-F16 series");
        Assert.Equal("%PDF de", File.ReadAllText(moved[L.German]));
        Assert.Equal("%PDF en", File.ReadAllText(moved[L.English]));
        Assert.Equal(["SE-F16 series (de).pdf", "SE-F16 series (en).pdf", "SE-F16 series.json"],
            Directory.GetFiles(SeriesFolder).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        Assert.True(store.DeleteSeries<Battery>("Deye", "SE-F16 series"));
        Assert.False(Directory.Exists(SeriesFolder));
    }

    [Fact]
    public void Unknown_datasheet_language_is_refused()
    {
        JsonDataStore store = Store();

        Assert.Throws<ArgumentException>(() => store.SaveSeries(SeF(SeF16(store)), Pdf("fr.pdf", "%PDF fr"), datasheetLanguage: "fr"));
    }

    private sealed class Location(string path) : IUserDataLocation
    {
        public string DirectoryPath { get; } = path;
    }
}
