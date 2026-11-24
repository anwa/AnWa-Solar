using System.Reflection;

namespace AnWaSolar.Core.Manual;

/// <summary>Reads the manual pages that the build embeds as resources named <c>manual/&lt;relative path&gt;</c>.</summary>
public static class ManualResources
{
    private const string Prefix = "manual/";

    /// <summary>File contents by relative path with <c>/</c>; empty when the assembly contains no manual.</summary>
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
