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

    // Values and inclusion per power class of the datasheet; the shown class is written back when another is chosen.
    private readonly List<Dictionary<string, string?>> _variantValues = [];
    private readonly List<bool> _included = [];
    private int _shown = -1;

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

        _variantValues.Clear();
        _included.Clear();
        _shown = -1;
        for (int i = 0; i < _result.Variants.Count; i++)
        {
            _variantValues.Add(DatasheetFields.For(Kind).ToDictionary(f => f.Key, f => _result.Get(f.Key, i)?.Text));
            _included.Add(true);
        }

        _loading = true;
        SeriesInput.Text = SeriesNames.Suggest(_result.Variants) ?? Path.GetFileNameWithoutExtension(_path);
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

    private void OnIncludeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading && _shown >= 0)
        {
            _included[_shown] = IncludeCheck.IsChecked == true;
            UpdateIncludedText();
        }
    }

    private void OnSeriesChanged(object sender, TextChangedEventArgs e) => UpdateExistingNote();

    private void UpdateIncludedText()
    {
        int count = _included.Count(i => i);
        IncludedText.Text = $"{count} of {_included.Count} power classes are saved";
        IncludeCheck.Visibility = _included.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        IncludedText.Visibility = IncludeCheck.Visibility;
    }

    // Keeps the texts of the shown power class; the manufacturer applies to the whole series.
    private void StoreShownValues()
    {
        if (_shown < 0 || _shown >= _variantValues.Count)
        {
            return;
        }

        foreach ((string key, FieldRow row) in _rows)
        {
            _variantValues[_shown][key] = row.Box.Text;
        }

        foreach (Dictionary<string, string?> values in _variantValues)
        {
            values[DatasheetFields.Manufacturer] = _rows[DatasheetFields.Manufacturer].Box.Text;
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
        if (_result is null || variant >= _variantValues.Count)
        {
            return;
        }

        StoreShownValues();
        _shown = variant;
        foreach (FieldRow row in _rows.Values)
        {
            row.Found = _result.Get(row.Field.Key, variant);
            row.Box.Text = _variantValues[variant].TryGetValue(row.Field.Key, out string? text) ? text ?? string.Empty : string.Empty;
            UpdateState(row);
        }

        _loading = true;
        IncludeCheck.IsChecked = _included[variant];
        _loading = false;
        UpdateIncludedText();
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

    // Tells whether Apply adds a series or replaces one with the same manufacturer and name.
    private void UpdateExistingNote()
    {
        if (!IsInitialized || _rows.Count == 0)
        {
            return;
        }

        string manufacturer = (Value(DatasheetFields.Manufacturer) ?? string.Empty).Trim();
        string series = SeriesInput.Text.Trim();
        IEnumerable<IMasterDataEntry> entries = Kind switch
        {
            DatasheetKind.Inverter => _store.Inverters,
            DatasheetKind.Battery => _store.Batteries,
            _ => _store.Modules,
        };
        IMasterDataEntry? existing = entries.FirstOrDefault(e =>
            string.Equals(e.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Series, series, StringComparison.OrdinalIgnoreCase));

        ExistingText.Text = existing switch
        {
            null => string.Empty,
            { IsUserEntry: true } => $"Apply replaces your series {existing.Manufacturer} {existing.Series}.",
            _ => $"{existing.Manufacturer} {existing.Series} is in the shipped master data. Apply saves your values as an own series that replaces it in the selection; the shipped data stays unchanged.",
        };
        ExistingText.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private string? Value(string key) => _rows.TryGetValue(key, out FieldRow? row) ? row.Box.Text : null;

    private void OnOpenDatasheetClick(object sender, RoutedEventArgs e) => _datasheets.ShowFile(this, _path, Path.GetFileName(_path));

    // Builds every included power class; the errors name the power class.
    private List<T> BuildVariants<T>(BuildEntry<T> build, List<string> errors)
        where T : class, IMasterDataEntry
    {
        var result = new List<T>();
        for (int i = 0; i < _variantValues.Count; i++)
        {
            if (!_included[i])
            {
                continue;
            }

            string name = VariantCombo.Items.Count > i ? VariantCombo.Items[i]?.ToString() ?? $"Column {i + 1}" : $"Column {i + 1}";
            IReadOnlyList<string> problems = build(_variantValues[i], out T? entry);
            if (problems.Count > 0 || entry is null)
            {
                errors.AddRange(problems.Select(p => _variantValues.Count > 1 ? $"{name}: {p}" : p));
            }
            else
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private delegate IReadOnlyList<string> BuildEntry<T>(IReadOnlyDictionary<string, string?> values, out T? entry);

    private IReadOnlyList<T> SaveSeries<T>(BuildEntry<T> build, List<string> errors)
        where T : class, IMasterDataEntry
    {
        List<T> variants = BuildVariants(build, errors);
        if (errors.Count > 0)
        {
            return [];
        }

        var series = new ComponentSeries<T>
        {
            Manufacturer = (Value(DatasheetFields.Manufacturer) ?? string.Empty).Trim(),
            Series = SeriesInput.Text.Trim(),
            Variants = variants,
        };
        return _store.SaveSeries(series, _path);
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        StoreShownValues();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SeriesInput.Text))
        {
            errors.Add("Series is missing.");
        }

        if (!_included.Any(i => i))
        {
            errors.Add("Choose at least one power class to save.");
        }

        if (errors.Count > 0)
        {
            ShowErrors(errors);
            return;
        }

        try
        {
            IReadOnlyList<IMasterDataEntry> saved = Kind switch
            {
                DatasheetKind.Module => SaveSeries<PVModule>(DatasheetEntryBuilder.TryBuildModule, errors),
                DatasheetKind.Battery => SaveSeries<Battery>(DatasheetEntryBuilder.TryBuildBattery, errors),
                _ => SaveSeries<Inverter>(DatasheetEntryBuilder.TryBuildInverter, errors),
            };
            if (errors.Count > 0)
            {
                ShowErrors(errors);
                return;
            }

            // The power class shown last is the one the caller selects.
            string shownModel = (_shown >= 0 ? _variantValues[_shown][DatasheetFields.Model] : null)?.Trim() ?? string.Empty;
            Saved = saved.FirstOrDefault(s => string.Equals(s.Model, shownModel, StringComparison.OrdinalIgnoreCase)) ?? saved.FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageDialog.Show(this, $"The entry could not be saved.\n\n{ex.Message}", Title, StatusKind.Error);
            return;
        }

        DialogResult = true;
    }

    private void ShowErrors(IEnumerable<string> errors)
    {
        ErrorText.Text = "Please correct:\n• " + string.Join("\n• ", errors);
        ErrorText.Visibility = Visibility.Visible;
    }
}
