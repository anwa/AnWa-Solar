using System.IO.Compression;
using System.Text.Json;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Projects;
using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using static AnWaSolar.Core.Tests.StringCalculatorTests;

namespace AnWaSolar.Core.Tests;

/// <summary>Projects: files in the user data folder, the takeover of the old settings, the ZIP export and the report.</summary>
public sealed class ProjectTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnWaSolar.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ProjectService Service() => new(NullLogger<ProjectService>.Instance, new Location(_root));

    private static Project Sample(string name = "Müller roof") => new()
    {
        Name = name,
        Customer = "Familie Müller",
        Address = "Hauptstraße 1\n12345 Musterstadt",
        Notes = "South roof, 30°.",
        CreatedAt = new DateTime(2026, 1, 1),
        Parameters = new CalculationParameters { MinTempC = -25, MaxTempC = 75, SafetyMarginPct = 3 },
        Inverter = Deye18K(),
        Strings =
        [
            new ProjectString { MpptIndex = 1, Module = AikoA500(), ModulesPerString = 11, ParallelStrings = 2 },
            new ProjectString { MpptIndex = 2, ModulesPerString = 10, IsEnabled = false },
        ],
        Cables = new CableSettings { DcLengthsM = [20, 15], BatteryBusbar = true },
        Battery = new BatterySettings { Enabled = true, Battery = new SelectedRef { Manufacturer = "Deye", Model = "SE-F16" }, InParallel = 2 },
    };

    [Fact]
    public void Project_is_saved_and_loaded_with_all_values()
    {
        ProjectService service = Service();
        string path = service.Create(Sample());

        Project loaded = service.Load(path);

        Assert.Equal(Path.Combine(_root, ProjectService.FolderName, "Müller roof.json"), path);
        Assert.Equal("Familie Müller", loaded.Customer);
        Assert.Equal(-25, loaded.Parameters.MinTempC);
        Assert.Equal("SUN-18K-SG05LP3-EU-SM2", loaded.Inverter!.Model);
        Assert.Equal(28800, loaded.Inverter.MaxDcInputPowerW); // the copy keeps the datasheet values
        Assert.Equal(14.05, loaded.Strings[0].Module!.IscA);
        Assert.Equal(2, loaded.Strings[0].ParallelStrings);
        Assert.False(loaded.Strings[1].IsEnabled);
        Assert.Equal([20.0, 15.0], loaded.Cables.DcLengthsM);
        Assert.True(loaded.Cables.BatteryBusbar);
        Assert.Equal("SE-F16", loaded.Battery.Battery!.Model);
        Assert.True(loaded.ModifiedAt > loaded.CreatedAt);
        Assert.Empty(Directory.GetFiles(service.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public void Same_names_get_their_own_files_and_the_list_shows_the_latest_first()
    {
        ProjectService service = Service();
        string first = service.Create(Sample("Roof: east/west"));
        string second = service.Create(Sample("Roof: east/west"));
        service.Save(first, service.Load(first));

        Assert.Equal("Roof_ east_west.json", Path.GetFileName(first));
        Assert.Equal("Roof_ east_west (2).json", Path.GetFileName(second));
        Assert.Equal([first, second], service.List().Select(p => p.Path));
    }

    [Fact]
    public void Unreadable_or_newer_project_is_reported()
    {
        ProjectService service = Service();
        Directory.CreateDirectory(service.DirectoryPath);
        string broken = Path.Combine(service.DirectoryPath, "broken.json");
        File.WriteAllText(broken, "{ \"Name\": ");
        string newer = Path.Combine(service.DirectoryPath, "newer.json");
        File.WriteAllText(newer, "{ \"SchemaVersion\": 99, \"Name\": \"Future\" }");

        Assert.Throws<IOException>(() => service.Load(broken));
        Assert.Contains("newer version", Assert.Throws<IOException>(() => service.Load(newer)).Message, StringComparison.Ordinal);
        Assert.Empty(service.List());
    }

    [Fact]
    public void Old_settings_become_a_project_with_copies_of_the_entries()
    {
        var settings = new SettingsService(NullLogger<SettingsService>.Instance, new Location(Path.Combine(_root, "user")), Path.Combine(_root, "none.json"));
        settings.SaveParameters(new CalculationParameters { MinTempC = -15 });
        settings.SaveLastSelection(new LastSelection
        {
            Inverter = new SelectedRef { Manufacturer = "Deye", Model = "SUN-18K-SG05LP3-EU-SM2" },
            Strings = [new MpptStringSelection { MpptIndex = 1, ModulesPerString = 11, Module = new SelectedRef { Manufacturer = "AIKO", Model = "AIKO-A500-MAH60Db" } }],
        });
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new Location(Path.Combine(_root, "data"))));
        store.Load();

        Project project = ProjectMigration.FromSettings(settings, store, "My project", new DateTime(2026, 11, 25))!;

        Assert.Equal("My project", project.Name);
        Assert.Equal(-15, project.Parameters.MinTempC);
        Assert.Equal(28800, project.Inverter!.MaxDcInputPowerW);
        Assert.Equal("AIKO-A500-MAH60Db", Assert.Single(project.Strings).Module!.Model);
    }

    [Fact]
    public void Settings_without_inverter_give_no_project()
    {
        var settings = new SettingsService(NullLogger<SettingsService>.Instance, new Location(Path.Combine(_root, "user")), Path.Combine(_root, "none.json"));
        var store = new JsonDataStore(NullLogger<JsonDataStore>.Instance, new JsonSerializerOptions(), new DataPathProvider(new Location(Path.Combine(_root, "data"))));

        Assert.Null(ProjectMigration.FromSettings(settings, store, "My project", DateTime.Now));
    }

    [Fact]
    public void Zip_contains_report_datasheets_project_and_the_missing_list()
    {
        Directory.CreateDirectory(_root);
        string report = Path.Combine(_root, "report.pdf");
        File.WriteAllBytes(report, [0x25, 0x50, 0x44, 0x46]);
        string sheet = Path.Combine(_root, "sheet.pdf");
        File.WriteAllBytes(sheet, [0x25, 0x50, 0x44, 0x46, 0x31]);
        string zipPath = Path.Combine(_root, "export.zip");

        IReadOnlyList<string> names = ProjectExporter.WriteZip(zipPath, Sample(), report, "# Report",
            [new("JA Solar JAM54D41-460/LR", sheet), new("Deye SUN-18K-SG05LP3-EU-SM2", null), new("ja solar JAM54D41-460/LR", sheet)]);

        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        string[] entries = [.. zip.Entries.Select(e => e.FullName).Order()];
        Assert.Equal(
        [
            "Müller roof/Datasheets/JA Solar JAM54D41-460_LR.pdf",
            "Müller roof/Missing datasheets.txt",
            "Müller roof/Müller roof - Report.pdf",
            "Müller roof/Müller roof.json",
        ], entries);
        Assert.Equal(4, names.Count);
        using var missing = new StreamReader(zip.GetEntry("Müller roof/Missing datasheets.txt")!.Open());
        Assert.Contains("- Deye SUN-18K-SG05LP3-EU-SM2", missing.ReadToEnd(), StringComparison.Ordinal);
        Assert.False(File.Exists(zipPath + ".tmp"));
    }

    [Fact]
    public void Zip_without_pdf_report_contains_the_markdown()
    {
        Directory.CreateDirectory(_root);
        string zipPath = Path.Combine(_root, "export.zip");

        ProjectExporter.WriteZip(zipPath, Sample(), null, "# Report", []);

        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        Assert.NotNull(zip.GetEntry("Müller roof/Müller roof - Report.md"));
        Assert.Null(zip.GetEntry("Müller roof/Missing datasheets.txt"));
    }

    [Fact]
    public void Report_starts_with_the_project_data()
    {
        Project project = Sample();
        StringCalculationResult result = StringCalculator.Calculate(Deye18K(), [Aiko(1, 11), Aiko(2, 11)], Parameters());

        string report = ReportBuilder.Build(result, Parameters(), new DateTime(2026, 11, 25), null, null, null, project.Info("x.json"));

        Assert.StartsWith("# Report: Müller roof", report, StringComparison.Ordinal);
        Assert.Contains("- Customer: Familie Müller", report, StringComparison.Ordinal);
        Assert.Contains("- Address: Hauptstraße 1, 12345 Musterstadt", report, StringComparison.Ordinal);
        Assert.Contains("South roof, 30°.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Recent_projects_keep_the_last_five_latest_first()
    {
        var settings = new SettingsService(NullLogger<SettingsService>.Instance, new Location(Path.Combine(_root, "user")), Path.Combine(_root, "none.json"));
        foreach (string name in new[] { "a", "b", "c", "d", "e", "f", "c" })
        {
            settings.AddRecentProject(Path.Combine(_root, name + ".json"));
        }

        var reread = new SettingsService(NullLogger<SettingsService>.Instance, new Location(Path.Combine(_root, "user")), Path.Combine(_root, "none.json"));
        Assert.Equal(["c", "f", "e", "d", "b"], reread.LoadRecentProjects().Select(Path.GetFileNameWithoutExtension));
    }

    private sealed class Location(string path) : IUserDataLocation
    {
        public string DirectoryPath { get; } = path;
    }
}
