using System;
using System.Globalization;
using System.Windows;

namespace AnWaSolar;

public partial class ParametersWindow : Window
{
    public CalculationParameters Parameters { get; private set; }

    public ParametersWindow(CalculationParameters current)
    {
        InitializeComponent();

        Parameters = new CalculationParameters
        {
            MinTempC = current.MinTempC,
            MaxTempC = current.MaxTempC,
            SicherheitsmargePct = current.SicherheitsmargePct
        };

        MinTempInput.Text = Parameters.MinTempC.ToString(CultureInfo.CurrentCulture);
        MaxTempInput.Text = Parameters.MaxTempC.ToString(CultureInfo.CurrentCulture);
        SicherheitsmargeInput.Text = Parameters.SicherheitsmargePct.ToString(CultureInfo.CurrentCulture);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(MinTempInput.Text, out var tMin) ||
            !TryParseDouble(MaxTempInput.Text, out var tMax) ||
            tMin >= tMax)
        {
            MessageBox.Show("Ungültiger Temperaturbereich. Tmin < Tmax angeben.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (!TryParseDouble(SicherheitsmargeInput.Text, out var marginPct) || marginPct < 0 || marginPct > 50)
        {
            MessageBox.Show("Ungültige Sicherheitsmarge (%). Bereich 0–50 %.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Parameters.MinTempC = tMin;
        Parameters.MaxTempC = tMax;
        Parameters.SicherheitsmargePct = marginPct;

        DialogResult = true;
        Close();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
