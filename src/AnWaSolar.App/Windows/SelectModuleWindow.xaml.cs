using System;
using System.Globalization;
using System.Linq;
using System.Windows;

using AnWaSolar.Core.Models;

namespace AnWaSolar.Windows;

public partial class SelectModuleWindow : Window
{
    private readonly PVModule[] _all;

    public PVModule? Selected { get; private set; }

    #region Constructor

    public SelectModuleWindow(System.Collections.Generic.IEnumerable<PVModule> modules)
    {
        InitializeComponent();
        _all = modules?.ToArray() ?? Array.Empty<PVModule>();

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
        var mod = _all.FirstOrDefault(x =>
            string.Equals(x.Manufacturer, h, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Model, m, StringComparison.OrdinalIgnoreCase));

        Selected = mod;
        UpdateDetails(mod);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
        {
            MessageBox.Show("Please select a manufacturer and a model.", "Note", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    #endregion

    #region Helpers

    private void UpdateDetails(PVModule? mod)
    {
        if (mod is null)
        {
            DetailManufacturer.Text = "-";
            DetailModel.Text = "-";
            DetailPmax.Text = "-";
            DetailMpp.Text = "-";
            DetailOcSc.Text = "-";
            DetailCoeffs.Text = "-";
            DetailTempRange.Text = "-";
            return;
        }

        DetailManufacturer.Text = mod.Manufacturer;
        DetailModel.Text = mod.Model;
        DetailPmax.Text = mod.PmaxW.ToString(CultureInfo.CurrentCulture);
        DetailMpp.Text = $"{mod.VmppV:F2} / {mod.ImppA:F2}";
        DetailOcSc.Text = $"{mod.VocV:F2} / {mod.IscA:F2}";
        DetailCoeffs.Text = $"{mod.PmaxTempCoeffPctPerC:F2} / {mod.VocTempCoeffPctPerC:F2} / {mod.IscTempCoeffPctPerC:F2}";
        DetailTempRange.Text = $"{mod.OperatingTempMinC:F0} … {mod.OperatingTempMaxC:F0}";
    }

    #endregion
}
