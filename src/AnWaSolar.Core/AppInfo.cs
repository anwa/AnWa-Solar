using System.Reflection;
using System.Text;

namespace AnWaSolar.Core;

/// <summary>Product, author and license information shown in the About window.</summary>
public static class AppInfo
{
    public const string Product = "AnWa-Solar";
    public const string Author = "Andreas Wansner";
    public const string Email = "andreas@anwa-soft.de";
    public const string License = "GNU General Public License v3.0";
    public const string SourceCode = "https://github.com/anwa/AnWa-Solar";
    public const string ThirdParty = "KaTeX 0.19.1 (MIT License) for formulas in the report and the manual";

    /// <summary>Copyright line from the assembly (the year of the build).</summary>
    public static string Copyright(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? $"© {Author}";
    }

    /// <summary>Build information written by the build (<c>SourceCommit</c>, <c>BuildDate</c>), or null.</summary>
    public static string? BuildInfo(Assembly assembly, string key)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string? value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>A section of the About window: heading and lines.</summary>
public sealed record AboutSection(string Heading, IReadOnlyList<string> Lines);

/// <summary>Values of the running installation that the About window shows.</summary>
public sealed record AboutFacts(
    string Version,
    string? Commit,
    string? BuildDate,
    string Framework,
    string OperatingSystem,
    string MaterialDesignVersion,
    string WebView2Version,
    string InstallationFolder,
    string SettingsFile,
    string LogFolder,
    string WebView2DataFolder,
    string Copyright);

/// <summary>Contents of the About window.</summary>
public static class AboutContent
{
    public static string Title(AboutFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return $"{AppInfo.Product} {facts.Version}";
    }

    public static IReadOnlyList<AboutSection> Sections(AboutFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return
        [
            new("Version", [
                Title(facts),
                $"Build: {facts.BuildDate ?? "unknown"}" + (facts.Commit is null ? string.Empty : $" · commit {facts.Commit}"),
            ]),
            new("Runtime", [
                facts.Framework,
                $"MaterialDesignThemes {facts.MaterialDesignVersion}",
                $"WebView2 Runtime {facts.WebView2Version}",
                facts.OperatingSystem,
            ]),
            new("Installation", [facts.InstallationFolder]),
            new("Data", [
                $"Settings: {facts.SettingsFile}",
                $"Logs: {facts.LogFolder}",
                $"WebView2: {facts.WebView2DataFolder}",
            ]),
            new("Contact", [$"Author: {AppInfo.Author}", $"Email: {AppInfo.Email}", $"Source code: {AppInfo.SourceCode}"]),
            new("License", [AppInfo.License, AppInfo.ThirdParty]),
        ];
    }

    /// <summary>All texts as plain text for the clipboard.</summary>
    public static string ToText(AboutFacts facts)
    {
        var text = new StringBuilder(Title(facts)).AppendLine().AppendLine();
        foreach (AboutSection section in Sections(facts))
        {
            text.AppendLine(section.Heading);
            foreach (string line in section.Lines)
            {
                text.Append("  ").AppendLine(line);
            }

            text.AppendLine();
        }

        return text.Append(facts.Copyright).ToString();
    }
}
