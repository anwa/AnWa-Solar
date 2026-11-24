using System.Reflection;

namespace AnWaSolar.Core;

/// <summary>
/// Application version as defined by <c>&lt;Version&gt;</c> in <c>Directory.Build.props</c>.
/// </summary>
public static class AppVersion
{
    /// <summary>Version of the application (all projects share the same version).</summary>
    public static string Current { get; } = FromAssembly(typeof(AppVersion).Assembly);

    /// <summary>Reads the informational version of an assembly, without build metadata.</summary>
    public static string FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        int metadata = version.IndexOf('+', StringComparison.Ordinal);
        return metadata >= 0 ? version[..metadata] : version;
    }
}
