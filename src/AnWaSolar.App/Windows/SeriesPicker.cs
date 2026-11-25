using System.Windows.Controls;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Windows;

/// <summary>
/// Chooses a master data entry step by step: manufacturer, optionally a type (for example "Three-phase hybrid
/// inverter"), series and power class (model). Each step lists only what the previous steps allow.
/// </summary>
internal sealed class SeriesPicker<T>
    where T : class, IMasterDataEntry
{
    public const string AllTypes = "All types";

    private readonly ComboBox _manufacturer;
    private readonly ComboBox? _type;
    private readonly ComboBox _series;
    private readonly ComboBox _model;
    private readonly Func<T, string>? _typeOf;
    private readonly Action<T?> _selected;
    private T[] _all = [];
    private bool _updating;

    public SeriesPicker(ComboBox manufacturer, ComboBox? type, ComboBox series, ComboBox model, Func<T, string>? typeOf, Action<T?> selected)
    {
        _manufacturer = manufacturer;
        _type = type;
        _series = series;
        _model = model;
        _typeOf = typeOf;
        _selected = selected;
        _manufacturer.SelectionChanged += (_, _) => FillTypes();
        if (_type is not null)
        {
            _type.SelectionChanged += (_, _) => FillSeries();
        }

        _series.SelectionChanged += (_, _) => FillModels();
        _model.SelectionChanged += (_, _) =>
        {
            if (!_updating)
            {
                _selected(Current);
            }
        };
    }

    /// <summary>The entry of the chosen manufacturer, series and model.</summary>
    public T? Current => _all.FirstOrDefault(e => Same(e.Manufacturer, _manufacturer.SelectedItem as string)
        && Same(e.Series, _series.SelectedItem as string) && Same(e.Model, _model.SelectedItem as string));

    /// <summary>Fills the lists and selects the given entry, otherwise the first manufacturer.</summary>
    public void Load(IEnumerable<T> entries, string? manufacturer, string? model)
    {
        _all = [.. entries];
        T? select = _all.FirstOrDefault(e => Same(e.Manufacturer, manufacturer) && Same(e.Model, model));
        string[] manufacturers = [.. _all.Select(e => e.Manufacturer).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
        _manufacturer.ItemsSource = manufacturers;
        _manufacturer.SelectedItem = null;
        _manufacturer.SelectedItem = select?.Manufacturer is { } m ? manufacturers.FirstOrDefault(x => Same(x, m)) : manufacturers.FirstOrDefault();
        if (select is not null)
        {
            if (_type is not null && _typeOf is not null)
            {
                _type.SelectedItem = AllTypes;
            }

            _series.SelectedItem = (_series.ItemsSource as IEnumerable<string>)?.FirstOrDefault(s => Same(s, select.Series));
            _model.SelectedItem = (_model.ItemsSource as IEnumerable<string>)?.FirstOrDefault(x => Same(x, select.Model));
        }

        _selected(Current);
    }

    private IEnumerable<T> OfManufacturer => _all.Where(e => Same(e.Manufacturer, _manufacturer.SelectedItem as string));

    private IEnumerable<T> OfType =>
        _type is null || _typeOf is null || _type.SelectedItem is not string type || type == AllTypes
            ? OfManufacturer
            : OfManufacturer.Where(e => _typeOf(e) == type);

    private void FillTypes()
    {
        if (_type is null || _typeOf is null)
        {
            FillSeries();
            return;
        }

        string[] types = [AllTypes, .. OfManufacturer.Select(_typeOf).Distinct().Order(StringComparer.OrdinalIgnoreCase)];
        _type.ItemsSource = types;
        _type.SelectedItem = null;
        _type.SelectedItem = AllTypes;
    }

    private void FillSeries()
    {
        string[] series = [.. OfType.Select(e => e.Series).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
        _series.ItemsSource = series;
        _series.SelectedItem = null;
        _series.SelectedItem = series.FirstOrDefault();
    }

    private void FillModels()
    {
        _updating = true;
        string[] models = [.. OfType.Where(e => Same(e.Series, _series.SelectedItem as string)).Select(e => e.Model).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
        _model.ItemsSource = models;
        _model.SelectedItem = models.FirstOrDefault();
        _updating = false;
        _selected(Current);
    }

    private static bool Same(string? a, string? b) => a is not null && b is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
