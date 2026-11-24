namespace AnWaSolar.Core.Tests;

public sealed class AboutContentTests
{
    private static readonly AboutFacts s_facts = new(
        Version: "1.2.3",
        Commit: "abc1234",
        BuildDate: "2026-11-24",
        Framework: ".NET 11.0.0",
        OperatingSystem: "Windows 11 (X64)",
        MaterialDesignVersion: "5.4.0",
        WebView2Version: "131.0",
        InstallationFolder: @"C:\Program Files\AnWa-Solar",
        SettingsFile: @"C:\Users\test\AppData\Local\AnWa-Solar\settings.json",
        LogFolder: @"C:\Users\test\AppData\Local\AnWa-Solar\logs",
        WebView2DataFolder: @"C:\Users\test\AppData\Local\AnWa-Solar\WebView2",
        Copyright: "© 2026 Andreas Wansner");

    [Fact]
    public void Contact_names_the_author_and_email()
    {
        AboutSection contact = Assert.Single(AboutContent.Sections(s_facts), s => s.Heading == "Contact");

        Assert.Contains("Author: Andreas Wansner", contact.Lines);
        Assert.Contains("Email: andreas@anwa-soft.de", contact.Lines);
    }

    [Fact]
    public void Text_contains_version_paths_and_license()
    {
        string text = AboutContent.ToText(s_facts);

        Assert.StartsWith("AnWa-Solar 1.2.3", text, StringComparison.Ordinal);
        Assert.Contains("Build: 2026-11-24 · commit abc1234", text, StringComparison.Ordinal);
        Assert.Contains(@"Settings: C:\Users\test\AppData\Local\AnWa-Solar\settings.json", text, StringComparison.Ordinal);
        Assert.Contains("GNU General Public License v3.0", text, StringComparison.Ordinal);
        Assert.Contains("KaTeX", text, StringComparison.Ordinal);
        Assert.EndsWith("© 2026 Andreas Wansner", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_reference_to_another_company()
    {
        Assert.DoesNotContain("omterra", AboutContent.ToText(s_facts), StringComparison.OrdinalIgnoreCase);
    }
}
