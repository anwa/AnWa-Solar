using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging;

namespace AnWaSolar.Core.Projects;

public interface IProjectService
{
    /// <summary>Folder of the project files.</summary>
    string DirectoryPath { get; }

    /// <summary>All readable projects in the folder, the most recently changed first.</summary>
    IReadOnlyList<ProjectInfo> List();

    /// <exception cref="IOException">The file cannot be read or is no project.</exception>
    Project Load(string path);

    /// <summary>Saves a new project into a new file named after the project and returns its path.</summary>
    string Create(Project project);

    /// <summary>Saves the project atomically and sets its modification time.</summary>
    void Save(string path, Project project);
}

/// <summary>
/// Stores projects as JSON files in <c>%LOCALAPPDATA%\AnWa-Solar\Projects</c>; files are written atomically.
/// </summary>
public sealed class ProjectService : IProjectService
{
    private readonly ILogger<ProjectService> logger;

    public ProjectService(ILogger<ProjectService> logger, IUserDataLocation userData)
        : this(logger, userData, null)
    {
    }

    /// <summary>Projects in the folder of the preferences, otherwise in the user data folder.</summary>
    public ProjectService(ILogger<ProjectService> logger, IUserDataLocation userData, ISettingsService? settings)
    {
        ArgumentNullException.ThrowIfNull(userData);
        this.logger = logger;
        string? folder = settings?.LoadPreferences().ProjectsFolder;
        DirectoryPath = string.IsNullOrWhiteSpace(folder) ? Path.Combine(userData.DirectoryPath, FolderName) : folder;
    }

    public const string FolderName = "Projects";

    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string DirectoryPath { get; }

    public IReadOnlyList<ProjectInfo> List()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return [];
        }

        var result = new List<ProjectInfo>();
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*.json"))
        {
            try
            {
                result.Add(Load(file).Info(file));
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Project file {Path} skipped.", file);
            }
        }

        return [.. result.OrderByDescending(p => p.ModifiedAt)];
    }

    public Project Load(string path)
    {
        try
        {
            Project? project = JsonSerializer.Deserialize<Project>(File.ReadAllText(path), s_options);
            if (project is null || string.IsNullOrWhiteSpace(project.Name))
            {
                throw new InvalidDataException(L.T("The file contains no project name."));
            }

            if (project.SchemaVersion > Project.CurrentSchemaVersion)
            {
                throw new InvalidDataException(L.F($"The project was saved by a newer version (schema {project.SchemaVersion})."));
            }

            project.Parameters ??= new();
            project.Cables ??= new();
            project.Battery ??= new();
            project.Strings ??= [];
            return project;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new IOException(L.F($"The project {Path.GetFileName(path)} cannot be read: {ex.Message}"), ex);
        }
    }

    public string Create(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(DirectoryPath);
        string baseName = FileNameOf(project.Name);
        string path = Path.Combine(DirectoryPath, baseName + ".json");
        for (int i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(DirectoryPath, $"{baseName} ({i}).json");
        }

        Save(path, project);
        logger.LogInformation("Project {Name} created in {Path}.", project.Name, path);
        return path;
    }

    public void Save(string path, Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ModifiedAt = DateTime.Now;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(project, s_options), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>A file or folder name from a project name, without characters Windows does not allow.</summary>
    public static string FileNameOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().TrimEnd('.');
        return clean.Length == 0 ? L.T("Project") : clean;
    }

    /// <summary>JSON of a project as it is saved, for the export.</summary>
    public static string Serialize(Project project) => JsonSerializer.Serialize(project, s_options);
}

/// <summary>A datasheet of the export: the entry's name and its PDF, or null when there is none.</summary>
public sealed record ExportDatasheet(string Name, string? Path);

/// <summary>Writes the ZIP export of a project: one folder with the report, the datasheets and the project file.</summary>
public static class ProjectExporter
{
    /// <summary>Name of the list of entries without datasheet, in the language of the application.</summary>
    public static string MissingFileName => L.T("Missing datasheets") + ".txt";

    /// <summary>
    /// Writes <paramref name="zipPath"/> with the folder named after the project: the report as PDF (or as Markdown when
    /// no PDF could be created), every available datasheet under <c>Datasheets</c>, the project file, and a list of
    /// the entries without datasheet. Returns the names of the files in the archive.
    /// </summary>
    public static IReadOnlyList<string> WriteZip(string zipPath, Project project, string? reportPdfPath, string reportMarkdown, IEnumerable<ExportDatasheet> datasheets)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(datasheets);
        string folder = ProjectService.FileNameOf(project.Name);
        var names = new List<string>();
        string temp = zipPath + ".tmp";
        using (FileStream stream = File.Create(temp))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            void AddText(string name, string text)
            {
                ZipArchiveEntry entry = zip.CreateEntry($"{folder}/{name}");
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(text);
                names.Add(name);
            }

            void AddFile(string name, string source)
            {
                zip.CreateEntryFromFile(source, $"{folder}/{name}");
                names.Add(name);
            }

            if (reportPdfPath is not null)
            {
                AddFile(L.F($"{folder} - Report") + ".pdf", reportPdfPath);
            }
            else
            {
                AddText(L.F($"{folder} - Report") + ".md", reportMarkdown);
            }

            var missing = new List<string>();
            foreach (ExportDatasheet sheet in datasheets.DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (sheet.Path is not null && File.Exists(sheet.Path))
                {
                    AddFile(L.T("Datasheets") + $"/{ProjectService.FileNameOf(sheet.Name)}.pdf", sheet.Path);
                }
                else
                {
                    missing.Add(sheet.Name);
                }
            }

            AddText($"{folder}.json", ProjectService.Serialize(project));
            if (missing.Count > 0)
            {
                AddText(MissingFileName, L.T("No datasheet is stored for these entries (shipped master data, or entered without a PDF):")
                    + Environment.NewLine + string.Join(Environment.NewLine, missing.Select(m => "- " + m)) + Environment.NewLine);
            }
        }

        File.Move(temp, zipPath, overwrite: true);
        return names;
    }
}
