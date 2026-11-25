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
    private readonly SeriesPicker<Inverter> _picker;

    public Inverter? Selected { get; private set; }

    /// <summary>True when a datasheet was imported in this dialog; the master data was reloaded.</summary>
    public bool Imported { get; private set; }

    #region Constructor

    public SelectInverterWindow(DatasheetService datasheets)
    {
        _datasheets = datasheets;
        InitializeComponent();
        _picker = new SeriesPicker<Inverter>(ManufacturerCombo, TypeCombo, SeriesCombo, ModelCombo,
            SeriesNames.InverterType, entry =>
            {
                Selected = entry;
                UpdateDetails(entry);
            });
        LoadList(null);
    }

    // Fills the lists and selects the given entry, or the first manufacturer.
    private void LoadList(Inverter? select) => _picker.Load(_datasheets.Store.Inverters, select?.Manufacturer, select?.Model);

    #endregion

    #region Events

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
            MessageDialog.Show(this, L.T("Please select a manufacturer and a model."), Title);
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
            ? DatasheetButton.IsEnabled ? L.T("Own master data, with datasheet") : L.T("Own master data")
            : L.T("Shipped master data");
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
