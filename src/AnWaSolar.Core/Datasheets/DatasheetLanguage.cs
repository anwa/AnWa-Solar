namespace AnWaSolar.Core.Datasheets;

/// <summary>
/// Language of a datasheet. A series keeps up to one German and one English datasheet and, from earlier versions, one
/// datasheet without a stated language; the datasheet of the application language is shown and exported.
/// </summary>
public static class DatasheetLanguage
{
    /// <summary>A datasheet whose language is not stated.</summary>
    public const string NotStated = "";

    /// <summary>The languages a datasheet can have, the one without a stated language first.</summary>
    public static IReadOnlyList<string> All { get; } = [NotStated, L.German, L.English];

    private static readonly HashSet<string> s_german = new(StringComparer.OrdinalIgnoreCase)
    {
        "und", "der", "die", "das", "mit", "für", "bei", "nicht", "leistung", "nennleistung", "spannung", "strom",
        "wirkungsgrad", "abmessungen", "gewicht", "betriebstemperatur", "eingang", "ausgang", "maximale", "maximaler",
        "technische", "kurzschlussstrom", "leerlaufspannung", "zellen", "anzahl", "schutzart",
    };

    private static readonly HashSet<string> s_english = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "with", "for", "not", "power", "rated", "voltage", "current", "efficiency", "dimensions", "weight",
        "operating", "temperature", "input", "output", "maximum", "technical", "short-circuit", "open-circuit", "cells",
        "number", "protection",
    };

    /// <summary>
    /// Guesses the language of a datasheet from its words: German or English when one language clearly prevails,
    /// otherwise <see cref="NotStated"/> (for example for bilingual or scanned datasheets).
    /// </summary>
    public static string Detect(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        int german = 0;
        int english = 0;
        foreach (string word in words)
        {
            string clean = word.Trim().Trim(',', '.', ':', ';', '(', ')', '/', '*');
            if (s_german.Contains(clean))
            {
                german++;
            }
            else if (s_english.Contains(clean))
            {
                english++;
            }
        }

        return german >= 5 && german > english * 1.5 ? L.German
            : english >= 5 && english > german * 1.5 ? L.English
            : NotStated;
    }

    /// <summary>
    /// The datasheet to show and export for a language: the datasheet in that language, otherwise the one without a
    /// stated language, otherwise the one in the other language; null without any.
    /// </summary>
    /// <param name="files">Datasheets by language (<see cref="NotStated"/>, "de", "en").</param>
    public static string? Pick(IReadOnlyDictionary<string, string> files, string language)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.TryGetValue(language, out string? exact))
        {
            return exact;
        }

        if (files.TryGetValue(NotStated, out string? unstated))
        {
            return unstated;
        }

        return files.Where(f => f.Key != language).OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Value).FirstOrDefault();
    }

    /// <summary>Display name of a datasheet language in the application language.</summary>
    public static string DisplayName(string language) => language switch
    {
        L.German => L.T("German"),
        L.English => L.T("English"),
        _ => L.T("Language not stated"),
    };
}
