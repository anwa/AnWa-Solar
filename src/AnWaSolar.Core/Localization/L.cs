using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AnWaSolar.Core.Localization;

/// <summary>
/// Texts in the language of the application. English is the source language: every text in the code is English and
/// is looked up in the German table <c>Localization/de.json</c> (English text → German text) when the language is
/// German; a text without translation stays English. Interpolated texts are looked up by their template, for example
/// <c>L.F($"{count} limits violated")</c> by <c>"{0} limits violated"</c>, and formatted with the current culture.
/// </summary>
public static class L
{
    public const string English = "en";
    public const string German = "de";

    private static IReadOnlyDictionary<string, string>? s_current;
    private static readonly Lazy<IReadOnlyDictionary<string, string>> s_german = new(LoadGerman);

    /// <summary>Language of the texts: <see cref="English"/> or <see cref="German"/>.</summary>
    public static string Language { get; private set; } = English;

    /// <summary>Culture of numbers and dates for the language: de-DE or en-GB.</summary>
    public static CultureInfo Culture => CultureFor(Language);

    /// <summary>The German table, for tests and the translation of the application windows.</summary>
    public static IReadOnlyDictionary<string, string> GermanTexts => s_german.Value;

    public static CultureInfo CultureFor(string language) => CultureInfo.GetCultureInfo(language == German ? "de-DE" : "en-GB");

    /// <summary>Sets the language of the texts; an unknown language gives English.</summary>
    public static void SetLanguage(string? language)
    {
        Language = language == German ? German : English;
        s_current = Language == German ? s_german.Value : null;
    }

    /// <summary>The text in the current language.</summary>
    public static string T(string english)
    {
        ArgumentNullException.ThrowIfNull(english);
        return s_current is not null && s_current.TryGetValue(english, out string? text) ? text : english;
    }

    /// <summary>True when a German text exists for the English text.</summary>
    public static bool TryTranslate(string english, out string text)
    {
        if (s_current is not null && s_current.TryGetValue(english, out string? found))
        {
            text = found;
            return true;
        }

        text = english;
        return false;
    }

    /// <summary>An interpolated text in the current language, formatted with the current culture.</summary>
    public static string F(ref TextHandler handler) => handler.ToStringAndClear();

    private static IReadOnlyDictionary<string, string> LoadGerman()
    {
        using Stream? stream = typeof(L).Assembly.GetManifestResourceStream("AnWaSolar.Core.Localization.de.json");
        if (stream is null)
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream, new JsonSerializerOptions { AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip })
            ?? [];
    }
}

/// <summary>
/// Collects an interpolated text as a template with numbered placeholders ("{0:F1} A") and its values, so that the
/// template can be looked up in the translation table.
/// </summary>
[InterpolatedStringHandler]
public ref struct TextHandler
{
    private readonly StringBuilder _template;
    private readonly List<object?> _values;

    public TextHandler(int literalLength, int formattedCount)
    {
        _template = new StringBuilder(literalLength + (formattedCount * 6));
        _values = new List<object?>(formattedCount);
    }

    public readonly void AppendLiteral(string value) => _template.Append(value.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal));

    public readonly void AppendFormatted<T>(T value) => AppendFormatted(value, 0, null);

    public readonly void AppendFormatted<T>(T value, string? format) => AppendFormatted(value, 0, format);

    public readonly void AppendFormatted<T>(T value, int alignment, string? format = null)
    {
        _template.Append('{').Append(_values.Count.ToString(CultureInfo.InvariantCulture));
        if (alignment != 0)
        {
            _template.Append(',').Append(alignment.ToString(CultureInfo.InvariantCulture));
        }

        if (format is not null)
        {
            _template.Append(':').Append(format);
        }

        _template.Append('}');
        _values.Add(value);
    }

    /// <summary>The template as it appears in the translation table.</summary>
    public readonly string Template => _template.ToString();

    public readonly string ToStringAndClear() => string.Format(CultureInfo.CurrentCulture, L.T(_template.ToString()), [.. _values]);
}
