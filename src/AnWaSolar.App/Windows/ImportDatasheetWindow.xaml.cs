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

namespace AnWaSolar.Windows;

/// <summary>
/// Shows the values read from a datasheet for checking, correcting and completing, and saves the entry with the
/// datasheet into the own master data.
/// </summary>
public partial class ImportDatasheetWindow : ChromeWindow
{
    private readonly string _path;
    private readonly IReadOnlyList<DatasheetWord> _words;
    private readonly IDataStore _store;
    private readonly DatasheetService _datasheets;
    private readonly Dictionary<string, FieldRow> _rows = [];
    private DatasheetParseResult? _result;
    private bool _loading;

    public ImportDatasheetWindow(string path, IReadOnlyList<DatasheetWord> words, DatasheetKind kind, IDataStore store, DatasheetService datasheets)
    {
        _path = path;
        _words = words;
        _store = store;
        _datasheets = datasheets;
        InitializeComponent();

        int pages = words.Count == 0 ? 0 : words.Max(w => w.Page);
        FileText.Text = $"{Path.GetFileName(path)} · {words.Count} words on {pages} page(s)";
        KindSelector.SelectedIndex = (int)kind;
    }

    /// <summary>The saved entry after Apply.</summary>
    public IMasterDataEntry? Saved { get; private set; }

    private DatasheetKind Kind => (DatasheetKind)Math.Max(0, KindSelector.SelectedIndex);

    private sealed record FieldRow(DatasheetField Field, TextBox Box, PackIcon Icon)
    {
        public DatasheetValue? Found { get; set; }
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        IEnumerable<string> manufacturers = _store.Modules.Select(m => m.Manufacturer).Concat(_store.Inverters.Select(i => i.Manufacturer))
            .Concat(_store.Batteries.Select(b => b.Manufacturer)).Distinct();
        _result = DatasheetParser.Parse(_words, Kind, manufacturers);
        BuildFields();

        _loading = true;
        // A column without heading (model names as graphics) is told apart by its energy.
        VariantCombo.ItemsSource = _result.Variants.Select((v, i) => !string.IsNullOrWhiteSpace(v) ? v
            : _result.Get(DatasheetFields.NominalEnergyKWh, i) is { } energy ? $"Column {i + 1} · {energy.Text} kWh" : $"Column {i + 1}").ToList();
        VariantCombo.Visibility = _result.Variants.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        VariantCombo.SelectedIndex = 0;
        _loading = false;
        FillValues(0);
    }

    private void OnVariantChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && VariantCombo.SelectedIndex >= 0)
        {
            FillValues(VariantCombo.SelectedIndex);
        }
    }

    // One row per field: label, text box, unit and a state icon.
    private void BuildFields()
    {
        FieldGrid.Children.Clear();
        FieldGrid.RowDefinitions.Clear();
        _rows.Clear();
        ErrorText.Visibility = Visibility.Collapsed;

        IReadOnlyList<DatasheetField> fields = DatasheetFields.For(Kind);
        for (int i = 0; i < fields.Count; i++)
        {
            DatasheetField field = fields[i];
            FieldGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = field.Required ? field.Label : $"{field.Label} (optional)",
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 12, 0),
            };
            var box = new TextBox { Margin = new Thickness(0, 6, 0, 14), VerticalAlignment = VerticalAlignment.Center };
            HintAssist.SetHelperText(box, field.Hint ?? string.Empty);
            AutomationProperties.SetName(box, field.Label);
            var unit = new TextBlock { Text = field.Unit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            unit.SetResourceReference(TextBlock.ForegroundProperty, "AnWaSoft.Brush.TextMuted");
            var icon = new PackIcon { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center };
            icon.SetResourceReference(StyleProperty, "AnWaSoft.StatusIcon");

            Grid.SetRow(label, i);
            Grid.SetRow(box, i);
            Grid.SetColumn(box, 1);
            Grid.SetRow(unit, i);
            Grid.SetColumn(unit, 2);
            Grid.SetRow(icon, i);
            Grid.SetColumn(icon, 3);
            FieldGrid.Children.Add(label);
            FieldGrid.Children.Add(box);
            FieldGrid.Children.Add(unit);
            FieldGrid.Children.Add(icon);

            var row = new FieldRow(field, box, icon);
            box.TextChanged += (_, _) => UpdateState(row);
            _rows[field.Key] = row;
        }
    }

    private void FillValues(int variant)
    {
        if (_result is null)
        {
            return;
        }

        foreach (FieldRow row in _rows.Values)
        {
            row.Found = _result.Get(row.Field.Key, variant);
            row.Box.Text = row.Found?.Text ?? string.Empty;
            UpdateState(row);
        }

        UpdateExistingNote();
    }

    // Read from the datasheet (OK), changed by the user (neutral), missing and required (warning).
    private void UpdateState(FieldRow row)
    {
        string text = row.Box.Text.Trim();
        (StatusKind kind, string tip) = text.Length == 0
            ? row.Field.Required
                ? (StatusKind.Warning, "Not found in the datasheet. Enter the value from the datasheet.")
                : (StatusKind.Neutral, "Optional; not found in the datasheet.")
            : row.Found is { } found && found.Text == text
                ? (StatusKind.Ok, $"Read from the datasheet: {found.Source}")
                : (StatusKind.Neutral, "Entered or changed by you.");
        StatusAssist.SetKind(row.Icon, kind);
        row.Icon.ToolTip = tip;
        row.Box.ToolTip = tip;

        if (row.Field.Key is DatasheetFields.Manufacturer or DatasheetFields.Model)
        {
            UpdateExistingNote();
        }
    }

    // Tells whether Apply adds an entry or replaces one with the same manufacturer and model.
    private void UpdateExistingNote()
    {
        string manufacturer = Value(DatasheetFields.Manufacturer) ?? string.Empty;
        string model = Value(DatasheetFields.Model) ?? string.Empty;
        IEnumerable<IMasterDataEntry> entries = Kind switch
        {
            DatasheetKind.Inverter => _store.Inverters,
            DatasheetKind.Battery => _store.Batteries,
            _ => _store.Modules,
        };
        IMasterDataEntry? existing = entries.FirstOrDefault(e =>
            string.Equals(e.Manufacturer, manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Model, model.Trim(), StringComparison.OrdinalIgnoreCase));

        ExistingText.Text = existing switch
        {
            null => string.Empty,
            { IsUserEntry: true } => $"Apply replaces your imported entry {existing.Manufacturer} {existing.Model}.",
            _ => $"{existing.Manufacturer} {existing.Model} is in the shipped master data. Apply saves your values as an own entry that replaces it in the selection; the shipped data stays unchanged.",
        };
        ExistingText.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private string? Value(string key) => _rows.TryGetValue(key, out FieldRow? row) ? row.Box.Text : null;

    private void OnOpenDatasheetClick(object sender, RoutedEventArgs e) => _datasheets.ShowFile(this, _path, Path.GetFileName(_path));

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Dictionary<string, string?> values = _rows.ToDictionary(r => r.Key, r => (string?)r.Value.Box.Text);
        IReadOnlyList<string> errors;
        IMasterDataEntry? entry;
        if (Kind == DatasheetKind.Module)
        {
            errors = DatasheetEntryBuilder.TryBuildModule(values, out PVModule? module);
            entry = module;
        }
        else if (Kind == DatasheetKind.Battery)
        {
            errors = DatasheetEntryBuilder.TryBuildBattery(values, out Battery? battery);
            entry = battery;
        }
        else
        {
            errors = DatasheetEntryBuilder.TryBuildInverter(values, out Inverter? inverter);
            entry = inverter;
        }

        if (errors.Count > 0 || entry is null)
        {
            ErrorText.Text = "Please correct:\n• " + string.Join("\n• ", errors);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            Saved = entry switch
            {
                PVModule module => _store.SaveModule(module, _path),
                Inverter inverter => _store.SaveInverter(inverter, _path),
                Battery battery => _store.SaveBattery(battery, _path),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageDialog.Show(this, $"The entry could not be saved.\n\n{ex.Message}", Title, StatusKind.Error);
            return;
        }

        DialogResult = true;
    }
}
