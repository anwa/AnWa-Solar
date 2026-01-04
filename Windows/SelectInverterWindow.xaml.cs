using System;
using System.Linq;
using System.Windows;
using AnWaSolar.Models;

namespace AnWaSolar;

public partial class SelectInverterWindow : Window
{
    private readonly Wechselrichter[] _all;

    public Wechselrichter? Selected { get; private set; }

    #region Konstruktor

    public SelectInverterWindow(System.Collections.Generic.IEnumerable<Wechselrichter> inverterList)
    {
        InitializeComponent();
        _all = inverterList?.ToArray() ?? Array.Empty<Wechselrichter>();

        var hersteller = _all.Select(x => x.Hersteller).Distinct().OrderBy(x => x).ToArray();
        HerstellerCombo.ItemsSource = hersteller;
        if (hersteller.Length > 0) HerstellerCombo.SelectedItem = hersteller[0];
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
        var wr = _all.FirstOrDefault(x =>
            string.Equals(x.Hersteller, h, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Model, m, StringComparison.OrdinalIgnoreCase));

        Selected = wr;
        UpdateDetails(wr);
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
        {
            MessageBox.Show("Bitte Hersteller und Modell wählen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    #endregion

    #region Hilfsmethoden

    private void UpdateDetails(Wechselrichter? wr)
    {
        if (wr is null)
        {
            DetailHersteller.Text = "-";
            DetailModel.Text = "-";
            DetailMppt.Text = "-";
            DetailVdcMax.Text = "-";
            DetailStart.Text = "-";
            DetailCurrents.Text = "-";
            DetailMpptCount.Text = "-";
            return;
        }
        DetailHersteller.Text = wr.Hersteller;
        DetailModel.Text = wr.Model;
        DetailMppt.Text = wr.MpptSpannungsbereichV;
        DetailVdcMax.Text = wr.MaxDcEingangsspannungV.ToString();
        DetailStart.Text = wr.StartspannungV.ToString();
        DetailCurrents.Text = $"{wr.MaxBetriebsPvEingangsstromA} / {wr.MaxEingangsKurzschlussstromA}";
        DetailMpptCount.Text = $"{wr.AnzahlDerMpptTrackers} / {wr.MaxStringsProMppt}";
    }

    #endregion
}
