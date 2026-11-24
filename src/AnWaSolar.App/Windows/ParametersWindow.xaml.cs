using System;
using System.Globalization;
using System.Windows;

using AnWaSolar.Core.Services;

namespace AnWaSolar.Windows;

public partial class ParametersWindow : ChromeWindow
{
    public CalculationParameters Parameters { get; private set; }

    public ParametersWindow(CalculationParameters current)
    {
        InitializeComponent();

        Parameters = new CalculationParameters
        {
            MinTempC = current.MinTempC,
            MaxTempC = current.MaxTempC,
            SafetyMarginPct = current.SafetyMarginPct
        };

        MinTempInput.Text = Parameters.MinTempC.ToString(CultureInfo.CurrentCulture);
        MaxTempInput.Text = Parameters.MaxTempC.ToString(CultureInfo.CurrentCulture);
        SafetyMarginInput.Text = Parameters.SafetyMarginPct.ToString(CultureInfo.CurrentCulture);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(MinTempInput.Text, out var tMin) ||
            !TryParseDouble(MaxTempInput.Text, out var tMax) ||
            tMin >= tMax)
        {
            AppDialog.Show(this, "Invalid temperature range. Tmin must be lower than Tmax.", Title, DialogKind.Error);
            return;
        }
        if (!TryParseDouble(SafetyMarginInput.Text, out var marginPct) || marginPct < 0 || marginPct > 50)
        {
            AppDialog.Show(this, "Invalid safety margin. Allowed range: 0–50 %.", Title, DialogKind.Error);
            return;
        }

        Parameters.MinTempC = tMin;
        Parameters.MaxTempC = tMax;
        Parameters.SafetyMarginPct = marginPct;

        DialogResult = true;
        Close();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
