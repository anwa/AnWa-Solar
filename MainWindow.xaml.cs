using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using AnWaSolar.Services;
using AnWaSolar.Models;

namespace AnWaSolar;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;
    private readonly IMarkdownService _markdown;
    private readonly IDataStore _dataStore;

    public MainWindow(ILogger<MainWindow> logger, IMarkdownService markdown, IDataStore dataStore)
    {
        InitializeComponent();
        _logger = logger;
        _markdown = markdown;
        _dataStore = dataStore;

        _logger.LogInformation("Hauptfenster initialisiert.");

        // Daten ins UI setzen
        ModuleCombo.ItemsSource = _dataStore.Module;
        ModuleCombo.SelectedItem = _dataStore.Module.FirstOrDefault();

        InverterCombo.ItemsSource = _dataStore.Wechselrichter;
        InverterCombo.SelectedItem = _dataStore.Wechselrichter.FirstOrDefault();
    }

    private void OnCalculateStringClick(object sender, RoutedEventArgs e)
    {
        var modul = ModuleCombo.SelectedItem as PVModule;
        var wr = InverterCombo.SelectedItem as Wechselrichter;

        if (modul is null || wr is null)
        {
            StringCalcOutput.Text = "Bitte sowohl PV-Modul als auch Wechselrichter auswählen.";
            return;
        }

        // Platzhalter-Ausgabe: zeigt die wichtigsten Felder
        StringCalcOutput.Text =
            $"Auswahl:\n" +
            $"- Modul: {modul.Hersteller} {modul.Model}, UMPP={modul.SpannungImMppUmppV} V, IMPP={modul.StromImMppImppA} A, UOC={modul.LeerlaufspannungUocV} V\n" +
            $"- Wechselrichter: {wr.Hersteller} {wr.Model}, MPPT={wr.MpptSpannungsbereichV} V, Max PV-Strom={wr.MaxBetriebsPvEingangsstromA} A\n";

        _logger.LogInformation("String-Berechnung (Demo) ausgeführt: Modul={Modul}, WR={WR}",
            modul.DisplayName, wr.DisplayName);
    }

    private void OnConvertMarkdownClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var md = MarkdownInput.Text ?? string.Empty;
            var html = _markdown.ToHtml(md);
            HtmlOutput.Text = html;

            _logger.LogInformation("Markdown erfolgreich in HTML konvertiert. Länge={Laenge}", html.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler bei der Markdown-Konvertierung.");
            MessageBox.Show("Fehler bei der Markdown-Konvertierung. Details siehe Log.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
