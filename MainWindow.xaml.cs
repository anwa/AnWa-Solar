using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using AnWaSolar.Models;
using AnWaSolar.Services;
using Microsoft.Extensions.Logging;

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

        // Daten fürs UI
        ModuleCombo.ItemsSource = _dataStore.Module;
        ModuleCombo.SelectedItem = _dataStore.Module.FirstOrDefault();

        InverterCombo.ItemsSource = _dataStore.Wechselrichter;
        InverterCombo.SelectedItem = _dataStore.Wechselrichter.FirstOrDefault();
    }

    private bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool TryParseInt(string? text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) ||
               int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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

        if (!TryParseDouble(MinTempInput.Text, out var tMin))
        {
            StringCalcOutput.Text = "Ungültiger Wert für Min. Temperatur.";
            return;
        }
        if (!TryParseDouble(MaxTempInput.Text, out var tMax))
        {
            StringCalcOutput.Text = "Ungültiger Wert für Max. Temperatur.";
            return;
        }
        if (!TryParseInt(ModuleProStringInput.Text, out var moduleProString) || moduleProString <= 0)
        {
            StringCalcOutput.Text = "Ungültige Anzahl 'Module pro String' (muss > 0 sein).";
            return;
        }
        if (!TryParseInt(ParalleleStringsInput.Text, out var paralleleStrings) || paralleleStrings <= 0)
        {
            StringCalcOutput.Text = "Ungültige Anzahl 'Parallele Strings' (muss > 0 sein).";
            return;
        }
        if (!TryParseDouble(SicherheitsmargeInput.Text, out var sicherheitsmargeProzent))
        {
            StringCalcOutput.Text = "Ungültiger Wert für die Sicherheitsmarge (%).";
            return;
        }
        if (sicherheitsmargeProzent < 0 || sicherheitsmargeProzent > 50)
        {
            StringCalcOutput.Text = "Sicherheitsmarge muss zwischen 0 % und 50 % liegen.";
            return;
        }

        if (tMin >= tMax)
        {
            StringCalcOutput.Text = "Die Min. Temperatur muss kleiner als die Max. Temperatur sein.";
            return;
        }
        _logger.LogInformation("Parameter: Tmin={Tmin}°C, Tmax={Tmax}°C, Module/String={MPS}, Parallele Strings={PS}, Sicherheitsmarge={Margin}%",
            tMin, tMax, moduleProString, paralleleStrings, sicherheitsmargeProzent);

        _logger.LogInformation(
            "String-Berechnung gestartet. Tmin={Tmin}°C, Tmax={Tmax}°C, ModuleProString={MPS}, ParalleleStrings={PS}, Modul={Modul}, WR={WR}",
            tMin, tMax, moduleProString, paralleleStrings, modul.DisplayName, wr.DisplayName);

        // Platzhalter: Hier wird später die eigentliche Auslegung/Bewertung erfolgen.
        StringCalcOutput.Text =
            $"Eingaben:" + Environment.NewLine +
            $"- Modul: {modul.Hersteller} {modul.Model} (UMPP={modul.SpannungImMppUmppV} V, UOC={modul.LeerlaufspannungUocV} V, IMPP={modul.StromImMppImppA} A)" + Environment.NewLine +
            $"- Wechselrichter: {wr.Hersteller} {wr.Model} (MPPT-Bereich {wr.MpptSpannungsbereichV} V, Max Strings/MPPT={(wr.MaxStringsProMppt == 0 ? "unbekannt" : wr.MaxStringsProMppt)})" + Environment.NewLine +
            $"- Tmin={tMin} °C, Tmax={tMax} °C, Module/String={moduleProString}, Parallele Strings={paralleleStrings}, Sicherheitsmarge={sicherheitsmargeProzent} %" + Environment.NewLine +
            $"Berechnung folgt nach finaler Parametrisierung.";
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
