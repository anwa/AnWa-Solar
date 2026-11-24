using System;
using System.Globalization;
using System.Linq;
using System.Windows;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Windows;

public partial class SelectInverterWindow : ChromeWindow
{
    private readonly Inverter[] _all;

    public Inverter? Selected { get; private set; }

    #region Constructor

    public SelectInverterWindow(System.Collections.Generic.IEnumerable<Inverter> inverterList)
    {
        InitializeComponent();
        _all = inverterList?.ToArray() ?? Array.Empty<Inverter>();

        var manufacturers = _all.Select(x => x.Manufacturer).Distinct().OrderBy(x => x).ToArray();
        ManufacturerCombo.ItemsSource = manufacturers;
        if (manufacturers.Length > 0) ManufacturerCombo.SelectedItem = manufacturers[0];
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

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
        {
            AppDialog.Show(this, "Please select a manufacturer and a model.", Title);
            return;
        }
        DialogResult = true;
        Close();
    }

    #endregion

    #region Helpers

    private void UpdateDetails(Inverter? wr)
    {
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
