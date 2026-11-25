using System;
using System.Globalization;
using System.Linq;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;

using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Services;

namespace AnWaSolar.Windows;

public partial class SelectModuleWindow : ChromeWindow
{
    private readonly DatasheetService _datasheets;
    private readonly SeriesPicker<PVModule> _picker;

    public PVModule? Selected { get; private set; }

    /// <summary>True when a datasheet was imported in this dialog; the master data was reloaded.</summary>
    public bool Imported { get; private set; }

    #region Constructor

    public SelectModuleWindow(DatasheetService datasheets)
    {
        _datasheets = datasheets;
        InitializeComponent();
        _picker = new SeriesPicker<PVModule>(ManufacturerCombo, null, SeriesCombo, ModelCombo,
            null, entry =>
            {
                Selected = entry;
                UpdateDetails(entry);
            });
        LoadList(null);
    }

    // Fills the lists and selects the given entry, or the first manufacturer.
    private void LoadList(PVModule? select) => _picker.Load(_datasheets.Store.Modules, select?.Manufacturer, select?.Model);

    #endregion

    #region Events

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (await _datasheets.ImportAsync(this, DatasheetKind.Module) is { } entry)
        {
            Imported = true;
            LoadList(entry as PVModule);
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

    private void UpdateDetails(PVModule? mod)
    {
        DatasheetButton.IsEnabled = _datasheets.HasDatasheet(mod);
        DetailSource.Text = mod is null ? "-" : mod.IsUserEntry
            ? DatasheetButton.IsEnabled ? L.T("Own master data, with datasheet") : L.T("Own master data")
            : L.T("Shipped master data");
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
