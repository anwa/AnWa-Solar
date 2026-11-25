using System.Windows;
using System.Windows.Input;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;
using AnWaSolar.Windows;

using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AnWaSolar.Services;

/// <summary>Imports PDF datasheets into the own master data and shows stored datasheets.</summary>
public sealed class DatasheetService(IDataStore store, IUserDataLocation userData, ILoggerFactory loggers)
{
    private readonly ILogger _logger = loggers.CreateLogger<DatasheetService>();

    public IDataStore Store => store;

    /// <summary>True when the entry has a stored datasheet.</summary>
    public bool HasDatasheet(IMasterDataEntry? entry) => entry is not null && store.GetDatasheetPath(entry) is not null;

    /// <summary>
    /// Asks for a PDF datasheet, reads it and shows the import dialog. Returns the saved entry, or null when the user
    /// cancelled.
    /// </summary>
    /// <param name="preferredKind">Kind to start with; null detects it from the datasheet.</param>
    public async Task<IMasterDataEntry?> ImportAsync(Window owner, DatasheetKind? preferredKind)
    {
        var dialog = new OpenFileDialog
        {
            Title = L.T("Import datasheet"),
            Filter = "PDF datasheet (*.pdf)|*.pdf",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
        }

        string path = dialog.FileName;
        IReadOnlyList<DatasheetWord> words;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            // Reading a PDF can take a moment; it does not block the window.
            words = await Task.Run(() => PdfTextReader.Read(path));
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Datasheet {Path} could not be read.", path);
            MessageDialog.Show(owner, L.F($"The datasheet could not be read.\n\n{ex.Message}"), L.T("Import datasheet"), StatusKind.Error);
            return null;
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }

        _logger.LogInformation("Datasheet {Path} read: {Words} words.", path, words.Count);
        if (words.Count == 0)
        {
            MessageDialog.Show(owner,
                L.T("The PDF contains no text, for example because it is a scan. You can enter the values by hand; the datasheet is stored with them."),
                L.T("Import datasheet"), StatusKind.Warning);
        }

        DatasheetKind kind = preferredKind ?? DatasheetParser.DetectKind(words);
        var window = new ImportDatasheetWindow(path, words, kind, store, this) { Owner = owner };
        return window.ShowDialog() == true ? window.Saved : null;
    }

    /// <summary>Shows the stored datasheet of an entry.</summary>
    public void Show(Window owner, IMasterDataEntry entry)
    {
        if (store.GetDatasheetPath(entry) is { } path)
        {
            ShowFile(owner, path, $"{entry.Manufacturer} {entry.Model}");
        }
    }

    /// <summary>Shows a PDF file in a datasheet window.</summary>
    public void ShowFile(Window owner, string path, string title)
    {
        var window = new DatasheetWindow(path, title, userData, loggers.CreateLogger<DatasheetWindow>()) { Owner = owner };
        window.Show();
    }
}
