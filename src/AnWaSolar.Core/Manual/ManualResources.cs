using System.Reflection;

namespace AnWaSolar.Core.Manual;

/// <summary>
/// Reads the manual pages that the build embeds as resources named <c>manual/&lt;relative path&gt;</c>. The English pages
/// are in the root, the German ones below <c>de/</c> with the same file names.
/// </summary>
public static class ManualResources
{
    private const string Prefix = "manual/";

    /// <summary>The pages of a language ("de" or "en"); English when the language has no pages.</summary>
    public static IReadOnlyDictionary<string, string> Load(Assembly assembly, string language)
    {
        IReadOnlyDictionary<string, string> all = Load(assembly);
        string folder = language + "/";
        var localized = all.Where(f => f.Key.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(f => f.Key[folder.Length..], f => f.Value, StringComparer.OrdinalIgnoreCase);
        return localized.Count > 0 && language != "en"
            ? localized
            : all.Where(f => !f.Key.StartsWith("de/", StringComparison.OrdinalIgnoreCase)).ToDictionary(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>File contents of all languages by relative path with <c>/</c>; empty when the assembly contains no manual.</summary>
    public static IReadOnlyDictionary<string, string> Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in assembly.GetManifestResourceNames())
        {
            string normalized = name.Replace('\\', '/');
            if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using Stream? stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            files[normalized[Prefix.Length..]] = reader.ReadToEnd();
        }

        return files;
    }
}
