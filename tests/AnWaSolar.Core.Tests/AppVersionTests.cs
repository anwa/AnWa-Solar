namespace AnWaSolar.Core.Tests;

public sealed class AppVersionTests
{
    [Fact]
    public void Current_is_a_semantic_version_without_metadata()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppVersion.Current);
    }

    [Fact]
    public void Current_matches_the_assembly_version()
    {
        Version? assemblyVersion = typeof(AppVersion).Assembly.GetName().Version;

        Assert.NotNull(assemblyVersion);
        Assert.Equal(assemblyVersion.ToString(3), AppVersion.Current);
    }
}
