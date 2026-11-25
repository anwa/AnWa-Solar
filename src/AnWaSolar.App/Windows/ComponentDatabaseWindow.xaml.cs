using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;
using AnWaSolar.Services;

using MaterialDesignThemes.Wpf;

using Microsoft.Win32;

namespace AnWaSolar.Windows;

/// <summary>
/// The component database: a tree of kinds, manufacturers and series, and an editor with one column per power class.
/// Saving writes an own series; a shipped series is saved as an own copy that replaces it.
/// </summary>
public partial class ComponentDatabaseWindow : ChromeWindow
{
    private const double ColumnWidth = 180;

    private static readonly (DatasheetKind Kind, string Label)[] s_kinds =
    [
        (DatasheetKind.Module, L.T("PV modules")),
        (DatasheetKind.Inverter, L.T("Inverters")),
        (DatasheetKind.Battery, L.T("Batteries")),
    ];

    private readonly DatasheetService _datasheets;
    private readonly List<Dictionary<string, TextBox>> _columns = [];

    // The series in the editor: its kind, its key when it exists, whether it is own, and an attached datasheet.
    private DatasheetKind _kind = DatasheetKind.Module;
    private SeriesKey? _shown;
    private bool _isOwn;
    private string? _pendingDatasheet;
    private bool _dirty;
    private bool _loading;
    private TreeViewItem? _selectedItem;

    public ComponentDatabaseWindow(DatasheetService datasheets)
    {
        _datasheets = datasheets;
        InitializeComponent();
        BuildTree(null);
        Closing += (_, e) => e.Cancel = !ConfirmDiscard();
    }

    /// <summary>True when the master data was changed in this window.</summary>
    public bool Changed { get; private set; }

    private IDataStore Store => _datasheets.Store;

    private sealed record SeriesKey(DatasheetKind Kind, string Manufacturer, string Series);

    private IEnumerable<IMasterDataEntry> Entries(DatasheetKind kind) => kind switch
    {
        DatasheetKind.Inverter => Store.Inverters,
        DatasheetKind.Battery => Store.Batteries,
        _ => Store.Modules,
    };

    private static Dictionary<string, string?> ValuesOf(IMasterDataEntry entry) => entry switch
    {
        Inverter i => DatasheetEntryBuilder.ToValues(i),
        Battery b => DatasheetEntryBuilder.ToValues(b),
        PVModule m => DatasheetEntryBuilder.ToValues(m),
        _ => [],
    };

    private List<IMasterDataEntry> VariantsOf(SeriesKey key) =>
        [.. Entries(key.Kind).Where(e => Same(e.Manufacturer, key.Manufacturer) && Same(e.Series, key.Series))];

    private static string Noun(DatasheetKind kind) => kind switch
    {
        DatasheetKind.Inverter => "inverters",
        DatasheetKind.Battery => "batteries",
        _ => L.T("PV modules"),
    };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    #region Tree

    // Kinds, manufacturers and series; own series are marked. Selects the given series, otherwise the first one.
    private void BuildTree(SeriesKey? select)
    {
        _loading = true;
        SeriesTree.Items.Clear();
        TreeViewItem? toSelect = null;
        foreach ((DatasheetKind kind, string label) in s_kinds)
        {
            var kindItem = new TreeViewItem { Header = label, Tag = kind, IsExpanded = true };
            AutomationProperties.SetName(kindItem, label);
            foreach (IGrouping<string, IMasterDataEntry> manufacturer in Entries(kind).GroupBy(e => e.Manufacturer, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var manufacturerItem = new TreeViewItem { Header = manufacturer.Key, Tag = kind, IsExpanded = true };
                foreach (IGrouping<string, IMasterDataEntry> series in manufacturer.GroupBy(e => e.Series, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var key = new SeriesKey(kind, manufacturer.Key, series.Key);
                    int count = series.Count();
                    string own = series.Any(e => e.IsUserEntry) ? L.T(" · own") : string.Empty;
                    var seriesItem = new TreeViewItem { Header = $"{series.Key} ({count}){own}", Tag = key };
                    AutomationProperties.SetName(seriesItem, $"{manufacturer.Key} {series.Key}");
                    manufacturerItem.Items.Add(seriesItem);
                    if (select is not null && select.Kind == kind && Same(select.Manufacturer, key.Manufacturer) && Same(select.Series, key.Series))
                    {
                        toSelect = seriesItem;
                    }
                }

                kindItem.Items.Add(manufacturerItem);
            }

            SeriesTree.Items.Add(kindItem);
        }

        _loading = false;
        if (toSelect is not null)
        {
            toSelect.IsSelected = true;
            toSelect.BringIntoView();
        }
        else
        {
            ShowEmpty(DatasheetKind.Module);
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_loading || e.NewValue is not TreeViewItem item || ReferenceEquals(item, _selectedItem))
        {
            return;
        }

        if (!ConfirmDiscard())
        {
            // Back to the series being edited.
            _loading = true;
            if (_selectedItem is not null)
            {
                _selectedItem.IsSelected = true;
            }
            else
            {
                item.IsSelected = false;
            }

            _loading = false;
            return;
        }

        _selectedItem = item;
        switch (item.Tag)
        {
            case SeriesKey key:
                ShowSeries(key);
                break;
            case DatasheetKind kind:
                ShowEmpty(kind);
                break;
            default:
                break;
        }
    }

    #endregion

    #region Editor

    private void ShowEmpty(DatasheetKind kind)
    {
        _kind = kind;
        _shown = null;
        _isOwn = false;
        _pendingDatasheet = null;
        Fill(string.Empty, string.Empty, []);
        SourceText.Text = L.F($"Select a series on the left, or create a new series of {Noun(kind)}.");
        UpdateButtons();
    }

    private void ShowSeries(SeriesKey key)
    {
        List<IMasterDataEntry> variants = VariantsOf(key);
        _kind = key.Kind;
        _shown = key;
        _isOwn = variants.Any(v => v.IsUserEntry);
        _pendingDatasheet = null;
        Fill(key.Manufacturer, key.Series, [.. variants.Select(ValuesOf)]);
        SourceText.Text = _isOwn
            ? L.T("Own series in your master data.") + (DatasheetPath() is null ? L.T(" No datasheet attached.") : string.Empty)
            : L.T("Shipped series. Saving creates an own copy that replaces it; the shipped data stays unchanged.");
        UpdateButtons();
    }

    // Header, one row per field and one column per power class.
    private void Fill(string manufacturer, string series, List<Dictionary<string, string?>> variants)
    {
        _loading = true;
        ManufacturerInput.Text = manufacturer;
        SeriesInput.Text = series;
        ErrorText.Visibility = Visibility.Collapsed;
        _columns.Clear();
        EditorGrid.Children.Clear();
        EditorGrid.RowDefinitions.Clear();
        EditorGrid.ColumnDefinitions.Clear();
        EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        EditorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        IReadOnlyList<DatasheetField> fields = Fields;
        for (int r = 0; r < fields.Count; r++)
        {
            DatasheetField field = fields[r];
            EditorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new DockPanel { Margin = new Thickness(0, 4, 12, 4) };
            var fill = new Button
            {
                Style = (Style)FindResource("MaterialDesignIconButton"),
                Width = 26,
                Height = 26,
                ToolTip = L.T("Copy the value of the first power class to all power classes"),
                Content = new PackIcon { Kind = PackIconKind.ArrowExpandRight, Width = 16, Height = 16 },
            };
            AutomationProperties.SetName(fill, L.F($"Copy {field.Label} to all power classes"));
            fill.Click += (_, _) => FillRow(field.Key);
            DockPanel.SetDock(fill, Dock.Right);
            label.Children.Add(fill);
            var text = new TextBlock
            {
                Text = field.Unit.Length > 0 ? $"{field.Label} ({field.Unit})" : field.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                ToolTip = field.Hint,
                FontWeight = field.Required ? FontWeights.SemiBold : FontWeights.Normal,
            };
            label.Children.Add(text);
            Grid.SetRow(label, r + 1);
            EditorGrid.Children.Add(label);
        }

        foreach (Dictionary<string, string?> values in variants)
        {
            AddColumn(values);
        }

        _loading = false;
        _dirty = false;
        UpdateButtons();
    }

    private IReadOnlyList<DatasheetField> Fields => [.. DatasheetFields.For(_kind).Where(f => f.Key != DatasheetFields.Manufacturer)];

    private void AddColumn(Dictionary<string, string?> values)
    {
        int column = _columns.Count + 1;
        EditorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnWidth) });
        var boxes = new Dictionary<string, TextBox>();
        _columns.Add(boxes);

        var header = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var copy = new Button { Style = (Style)FindResource("MaterialDesignIconButton"), Width = 30, Height = 30, ToolTip = "Copy this power class", Content = new PackIcon { Kind = PackIconKind.ContentCopy, Width = 16, Height = 16 } };
        var remove = new Button { Style = (Style)FindResource("MaterialDesignIconButton"), Width = 30, Height = 30, ToolTip = "Remove this power class", Content = new PackIcon { Kind = PackIconKind.DeleteOutline, Width = 16, Height = 16 } };
        AutomationProperties.SetName(copy, L.F($"Copy power class {column}"));
        AutomationProperties.SetName(remove, L.F($"Remove power class {column}"));
        copy.Click += (_, _) =>
        {
            Dictionary<string, string?> copied = boxes.ToDictionary(b => b.Key, b => (string?)b.Value.Text);
            copied[DatasheetFields.Model] = string.Empty;
            AddColumn(copied);
            MarkDirty();
        };
        remove.Click += (_, _) => RemoveColumn(boxes);
        header.Children.Add(copy);
        header.Children.Add(remove);
        Grid.SetColumn(header, column);
        EditorGrid.Children.Add(header);

        IReadOnlyList<DatasheetField> fields = Fields;
        for (int r = 0; r < fields.Count; r++)
        {
            DatasheetField field = fields[r];
            var box = new TextBox
            {
                Margin = new Thickness(6, 2, 6, 2),
                Text = values.TryGetValue(field.Key, out string? text) ? text ?? string.Empty : string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = field.Key == DatasheetFields.Model ? FontWeights.SemiBold : FontWeights.Normal,
            };
            AutomationProperties.SetName(box, L.F($"{field.Label}, power class {column}"));
            box.TextChanged += OnEdited;
            Grid.SetRow(box, r + 1);
            Grid.SetColumn(box, column);
            EditorGrid.Children.Add(box);
            boxes[field.Key] = box;
        }
    }

    // Removes a power class; the columns are built again so that they stay in order.
    private void RemoveColumn(Dictionary<string, TextBox> boxes)
    {
        List<Dictionary<string, string?>> remaining = [.. _columns.Where(c => !ReferenceEquals(c, boxes)).Select(c => c.ToDictionary(b => b.Key, b => (string?)b.Value.Text))];
        bool dirty = _dirty;
        Fill(ManufacturerInput.Text, SeriesInput.Text, remaining);
        _dirty = dirty;
        MarkDirty();
    }

    private void FillRow(string key)
    {
        if (_columns.Count < 2)
        {
            return;
        }

        string text = _columns[0][key].Text;
        foreach (Dictionary<string, TextBox> column in _columns.Skip(1))
        {
            column[key].Text = text;
        }
    }

    private void OnAddVariantClick(object sender, RoutedEventArgs e)
    {
        Dictionary<string, string?> values = _columns.Count == 0
            ? []
            : _columns[^1].ToDictionary(b => b.Key, b => (string?)b.Value.Text);
        values[DatasheetFields.Model] = string.Empty;
        AddColumn(values);
        MarkDirty();
    }

    private void OnEdited(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            MarkDirty();
        }
    }

    private void MarkDirty()
    {
        _dirty = true;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        SaveButton.IsEnabled = _dirty;
        DiscardButton.IsEnabled = _dirty;
        DeleteButton.IsEnabled = _isOwn && _shown is not null;
        ShowDatasheetButton.IsEnabled = _pendingDatasheet is not null || DatasheetPath() is not null;
    }

    // Asks before changes are lost; true when there are none or they may be discarded.
    private bool ConfirmDiscard() => !_dirty
        || MessageDialog.Confirm(this, L.T("The series has unsaved changes. Discard them?"), Title, L.T("Discard"), L.T("Keep editing"), StatusKind.Warning);

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        _dirty = false;
        if (_shown is { } key)
        {
            ShowSeries(key);
        }
        else
        {
            ShowEmpty(_kind);
        }
    }

    #endregion

    #region Datasheet

    private string? DatasheetPath() =>
        _shown is { } key ? VariantsOf(key).Select(v => Store.GetDatasheetPath(v)).FirstOrDefault(p => p is not null) : null;

    private void OnAttachDatasheetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = L.T("Attach datasheet"), Filter = "PDF datasheet (*.pdf)|*.pdf", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            _pendingDatasheet = dialog.FileName;
            SourceText.Text = L.F($"Datasheet {Path.GetFileName(dialog.FileName)} is attached when you save.");
            MarkDirty();
        }
    }

    private void OnShowDatasheetClick(object sender, RoutedEventArgs e)
    {
        if ((_pendingDatasheet ?? DatasheetPath()) is { } path)
        {
            _datasheets.ShowFile(this, path, $"{ManufacturerInput.Text} {SeriesInput.Text}".Trim());
        }
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        _dirty = false;
        if (await _datasheets.ImportAsync(this, _kind) is { } entry)
        {
            Changed = true;
            DatasheetKind kind = entry switch
            {
                Inverter => DatasheetKind.Inverter,
                Battery => DatasheetKind.Battery,
                _ => DatasheetKind.Module,
            };
            BuildTree(new SeriesKey(kind, entry.Manufacturer, entry.Series));
        }
    }

    #endregion

    #region Save and delete

    private void OnNewSeriesClick(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        if (SeriesTree.SelectedItem is TreeViewItem { Tag: SeriesKey key })
        {
            _kind = key.Kind;
        }
        else if (SeriesTree.SelectedItem is TreeViewItem { Tag: DatasheetKind kind })
        {
            _kind = kind;
        }

        _shown = null;
        _isOwn = false;
        _pendingDatasheet = null;
        string manufacturer = SeriesTree.SelectedItem is TreeViewItem { Tag: SeriesKey selected } ? selected.Manufacturer : string.Empty;
        Fill(manufacturer, string.Empty, [new Dictionary<string, string?>()]);
        SourceText.Text = L.F($"New series of {Noun(_kind)}. Enter the values from the datasheet.");
        MarkDirty();
        SeriesInput.Focus();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        string manufacturer = ManufacturerInput.Text.Trim();
        string series = SeriesInput.Text.Trim();
        if (manufacturer.Length == 0)
        {
            errors.Add(L.T("Manufacturer is missing."));
        }

        if (series.Length == 0)
        {
            errors.Add(L.T("Series is missing."));
        }

        if (_columns.Count == 0)
        {
            errors.Add(L.T("Add at least one power class."));
        }

        if (errors.Count > 0)
        {
            ShowErrors(errors);
            return;
        }

        try
        {
            // A renamed own series replaces its old file; its datasheet moves with it.
            (string, string)? replaces = _isOwn && _shown is { } old ? (old.Manufacturer, old.Series) : null;
            string? datasheet = _pendingDatasheet ?? (_isOwn && _shown is { } renamed && (!Same(renamed.Manufacturer, manufacturer) || !Same(renamed.Series, series)) ? DatasheetPath() : null);
            bool saved = _kind switch
            {
                DatasheetKind.Inverter => Save<Inverter>(DatasheetEntryBuilder.TryBuildInverter, manufacturer, series, datasheet, replaces, errors),
                DatasheetKind.Battery => Save<Battery>(DatasheetEntryBuilder.TryBuildBattery, manufacturer, series, datasheet, replaces, errors),
                _ => Save<PVModule>(DatasheetEntryBuilder.TryBuildModule, manufacturer, series, datasheet, replaces, errors),
            };
            if (!saved)
            {
                ShowErrors(errors);
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageDialog.Show(this, L.F($"The series could not be saved.\n\n{ex.Message}"), Title, StatusKind.Error);
            return;
        }

        Changed = true;
        _dirty = false;
        BuildTree(new SeriesKey(_kind, manufacturer, series));
    }

    private delegate IReadOnlyList<string> BuildEntry<T>(IReadOnlyDictionary<string, string?> values, out T? entry);

    private bool Save<T>(BuildEntry<T> build, string manufacturer, string series, string? datasheet, (string, string)? replaces, List<string> errors)
        where T : class, IMasterDataEntry
    {
        var variants = new List<T>();
        for (int i = 0; i < _columns.Count; i++)
        {
            Dictionary<string, string?> values = _columns[i].ToDictionary(b => b.Key, b => (string?)b.Value.Text);
            values[DatasheetFields.Manufacturer] = manufacturer;
            string name = string.IsNullOrWhiteSpace(values[DatasheetFields.Model]) ? L.F($"Power class {i + 1}") : values[DatasheetFields.Model]!.Trim();
            IReadOnlyList<string> problems = build(values, out T? entry);
            if (problems.Count > 0 || entry is null)
            {
                errors.AddRange(problems.Select(p => $"{name}: {p}"));
            }
            else
            {
                variants.Add(entry);
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        Store.SaveSeries(new ComponentSeries<T> { Manufacturer = manufacturer, Series = series, Variants = variants }, datasheet, replaces);
        return true;
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_shown is not { } key || !_isOwn)
        {
            return;
        }

        if (!MessageDialog.Confirm(this, L.F($"Delete the own series {key.Manufacturer} {key.Series} with its {VariantsOf(key).Count} power classes and its datasheet? A shipped series of the same name comes back. This cannot be undone."),
            Title, L.T("Delete"), L.T("Cancel"), StatusKind.Warning))
        {
            return;
        }

        try
        {
            bool deleted = key.Kind switch
            {
                DatasheetKind.Inverter => Store.DeleteSeries<Inverter>(key.Manufacturer, key.Series),
                DatasheetKind.Battery => Store.DeleteSeries<Battery>(key.Manufacturer, key.Series),
                _ => Store.DeleteSeries<PVModule>(key.Manufacturer, key.Series),
            };
            Changed |= deleted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageDialog.Show(this, L.F($"The series could not be deleted.\n\n{ex.Message}"), Title, StatusKind.Error);
            return;
        }

        _dirty = false;
        BuildTree(VariantsOf(key).Count > 0 ? key : null);
    }

    private void ShowErrors(IEnumerable<string> errors)
    {
        ErrorText.Text = L.T("Please correct:\n• ") + string.Join("\n• ", errors);
        ErrorText.Visibility = Visibility.Visible;
    }

    #endregion
}
