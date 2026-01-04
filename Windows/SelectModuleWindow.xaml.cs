using System;
using System.Linq;
using System.Windows;
using AnWaSolar.Models;
using Microsoft.Extensions.Logging;

namespace AnWaSolar;

public partial class SelectModuleWindow : Window
{
    private readonly ILogger<SelectModuleWindow> _logger;
    private readonly PVModule[] _all;

    public PVModule? Selected { get; private set; }

    #region Konstruktor

    public SelectModuleWindow(ILogger<SelectModuleWindow> logger, System.Collections.Generic.IEnumerable<PVModule> modules)
    {
        InitializeComponent();
        _logger = logger;
        _all = modules?.ToArray() ?? Array.Empty<PVModule>();

        var hersteller = _all.Select(x => x.Hersteller).Distinct().OrderBy(x => x).ToArray();
        HerstellerCombo.ItemsSource = hersteller;
        if (hersteller.Length > 0) HerstellerCombo.SelectedItem = hersteller[0];

        _logger.LogInformation("Modul-Auswahlfenster geöffnet. Verfügbare Hersteller={Count}.", hersteller.Length);
    }

    #endregion

    #region Events

    private void OnHerstellerSelectionChanged(object sender, RoutedEventArgs e)
    {
        var h = HerstellerCombo.SelectedItem as string;
        var models = _all.Where(x => string.Equals(x.Hersteller, h, StringComparison.OrdinalIgnoreCase))
                         .Select(x => x.Model)
                         .Distinct()
                         .OrderBy(x => x)
                         .ToArray();
        ModelCombo.ItemsSource = models;
        if (models.Length > 0) ModelCombo.SelectedItem = models[0];
    }

    private void OnModelSelectionChanged(object sender, RoutedEventArgs e)
    {
        var h = HerstellerCombo.SelectedItem as string;
        var m = ModelCombo.SelectedItem as string;
        var mod = _all.FirstOrDefault(x =>
            string.Equals(x.Hersteller, h, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Model, m, StringComparison.OrdinalIgnoreCase));

        Selected = mod;
        UpdateDetails(mod);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
        {
            MessageBox.Show("Bitte Hersteller und Modell wählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _logger.LogInformation("PV-Modul ausgewählt: {Hersteller} {Model}.", Selected.Hersteller, Selected.Model);
        DialogResult = true;
        Close();
    }

    #endregion

    #region Hilfsmethoden

    private void UpdateDetails(PVModule? mod)
    {
        if (mod is null)
        {
            DetailHersteller.Text = "-";
            DetailModel.Text = "-";
            DetailPmax.Text = "-";
            DetailMpp.Text = "-";
            DetailOcSc.Text = "-";
            DetailCoeffs.Text = "-";
            DetailTempRange.Text = "-";
            return;
        }

        DetailHersteller.Text = mod.Hersteller;
        DetailModel.Text = mod.Model;
        DetailPmax.Text = mod.NominalleistungPmaxWp.ToString();
        DetailMpp.Text = $"{mod.SpannungImMppUmppV:F2} / {mod.StromImMppImppA:F2}";
        DetailOcSc.Text = $"{mod.LeerlaufspannungUocV:F2} / {mod.KurzschlusstromIscA:F2}";
        DetailCoeffs.Text = $"{mod.TemperaturkoeffPmaxProzentProGradC:F2} / {mod.TemperaturkoeffVocProzentProGradC:F2} / {mod.TemperaturkoeffIscProzentProGradC:F2}";
        DetailTempRange.Text = $"{mod.BetriebstemperaturMinC:F0} … {mod.BetriebstemperaturMaxC:F0}";
    }

    #endregion
}
