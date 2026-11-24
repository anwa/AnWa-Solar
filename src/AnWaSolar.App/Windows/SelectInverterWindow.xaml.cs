using System;
using System.Globalization;
using System.Linq;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Services;

namespace AnWaSolar.Windows;

public partial class SelectInverterWindow : ChromeWindow
{
    private readonly DatasheetService _datasheets;
    private Inverter[] _all = [];

    public Inverter? Selected { get; private set; }

    /// <summary>True when a datasheet was imported in this dialog; the master data was reloaded.</summary>
    public bool Imported { get; private set; }

    #region Constructor

    public SelectInverterWindow(DatasheetService datasheets)
    {
        _datasheets = datasheets;
        InitializeComponent();
        LoadList(null);
    }

    // Fills the manufacturer list and selects the given entry, or the first manufacturer.
    private void LoadList(Inverter? select)
    {
        _all = [.. _datasheets.Store.Inverters];
        var manufacturers = _all.Select(x => x.Manufacturer).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
        ManufacturerCombo.ItemsSource = manufacturers;
        string? manufacturer = select is null ? manufacturers.FirstOrDefault() : manufacturers.FirstOrDefault(m => string.Equals(m, select.Manufacturer, StringComparison.OrdinalIgnoreCase));
        ManufacturerCombo.SelectedItem = null;
        ManufacturerCombo.SelectedItem = manufacturer;
        if (select is not null)
        {
            ModelCombo.SelectedItem = (ModelCombo.ItemsSource as IEnumerable<string>)?.FirstOrDefault(m => string.Equals(m, select.Model, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Events

    private void OnManufacturerSelectionChanged(object sender, RoutedEventArgs e)
    {
        var h = ManufacturerCombo.SelectedItem as string;
        var models = _all.Where(x => string.Equals(x.Manufacturer, h, StringComparison.OrdinalIgnoreCase))
                         .Select(x => x.Model)
                         .Distinct()
                         .OrderBy(x => x)
                         .ToArray();
        ModelCombo.ItemsSource = models;
        if (models.Length > 0) ModelCombo.SelectedItem = models[0];
    }

    private void OnModelSelectionChanged(object sender, RoutedEventArgs e)
    {
        var h = ManufacturerCombo.SelectedItem as string;
        var m = ModelCombo.SelectedItem as string;
        var wr = _all.FirstOrDefault(x =>
            string.Equals(x.Manufacturer, h, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Model, m, StringComparison.OrdinalIgnoreCase));

        Selected = wr;
        UpdateDetails(wr);
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (await _datasheets.ImportAsync(this, DatasheetKind.Inverter) is { } entry)
        {
            Imported = true;
            LoadList(entry as Inverter);
        }
    }

    private void OnDatasheetClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not null)
        {
            _datasheets.Show(this, Selected);
        }
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
        {
            MessageDialog.Show(this, "Please select a manufacturer and a model.", Title);
            return;
        }
        DialogResult = true;
        Close();
    }

    #endregion

    #region Helpers

    private void UpdateDetails(Inverter? wr)
    {
        DatasheetButton.IsEnabled = _datasheets.HasDatasheet(wr);
        DetailSource.Text = wr is null ? "-" : wr.IsUserEntry
            ? DatasheetButton.IsEnabled ? "Own master data, with datasheet" : "Own master data"
            : "Shipped master data";
        if (wr is null)
        {
            DetailManufacturer.Text = "-";
            DetailModel.Text = "-";
            DetailMppt.Text = "-";
            DetailVdcMax.Text = "-";
            DetailStart.Text = "-";
            DetailCurrents.Text = "-";
            DetailMpptCount.Text = "-";
            return;
        }
        DetailManufacturer.Text = wr.Manufacturer;
        DetailModel.Text = wr.Model;
        DetailMppt.Text = wr.MpptVoltageRangeV;
        DetailVdcMax.Text = wr.MaxDcInputVoltageV.ToString(CultureInfo.CurrentCulture);
        DetailStart.Text = wr.StartVoltageV.ToString(CultureInfo.CurrentCulture);
        DetailCurrents.Text = $"{wr.MaxInputCurrentA} / {wr.MaxShortCircuitCurrentA}";
        DetailMpptCount.Text = $"{wr.MpptCount} / {wr.MaxStringsPerMppt}";
    }

    #endregion
}
