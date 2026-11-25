using System;
using System.Globalization;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Services;

namespace AnWaSolar.Windows;

public partial class ParametersWindow : ChromeWindow
{
    public CalculationParameters Parameters { get; private set; }

    public ParametersWindow(CalculationParameters current)
    {
        InitializeComponent();

        Parameters = current.Clone();

        CultureInfo c = CultureInfo.CurrentCulture;
        MinTempInput.Text = Parameters.MinTempC.ToString(c);
        MaxTempInput.Text = Parameters.MaxTempC.ToString(c);
        SafetyMarginInput.Text = Parameters.SafetyMarginPct.ToString(c);
        UnbalancedInput.Text = (Parameters.UnbalancedLoadLimitVA / 1000).ToString(c);
        RegistrationInput.Text = (Parameters.SimplifiedRegistrationLimitVA / 1000).ToString(c);
        FeedInInput.Text = Parameters.FeedInLimitPct.ToString(c);
        RatioMinInput.Text = Parameters.DcAcRatioMin.ToString(c);
        RatioMaxInput.Text = Parameters.DcAcRatioMax.ToString(c);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryParseDouble(MinTempInput.Text, out var tMin) ||
            !TryParseDouble(MaxTempInput.Text, out var tMax) ||
            tMin >= tMax)
        {
            MessageDialog.Show(this, L.T("Invalid temperature range. Tmin must be lower than Tmax."), Title, StatusKind.Error);
            return;
        }
        if (!TryParseDouble(SafetyMarginInput.Text, out var marginPct) || marginPct < 0 || marginPct > 50)
        {
            MessageDialog.Show(this, L.T("Invalid safety margin. Allowed range: 0–50 %."), Title, StatusKind.Error);
            return;
        }

        if (!TryParseDouble(UnbalancedInput.Text, out var unbalancedKva) || unbalancedKva <= 0
            || !TryParseDouble(RegistrationInput.Text, out var registrationKva) || registrationKva <= 0)
        {
            MessageDialog.Show(this, L.T("The unbalanced load limit and the registration limit must be greater than 0 kVA."), Title, StatusKind.Error);
            return;
        }
        if (!TryParseDouble(FeedInInput.Text, out var feedInPct) || feedInPct <= 0 || feedInPct > 100)
        {
            MessageDialog.Show(this, L.T("Invalid feed-in limit. Allowed range: above 0 up to 100 % (100 means no limit)."), Title, StatusKind.Error);
            return;
        }
        if (!TryParseDouble(RatioMinInput.Text, out var ratioMin) || !TryParseDouble(RatioMaxInput.Text, out var ratioMax)
            || ratioMin <= 0 || ratioMin >= ratioMax)
        {
            MessageDialog.Show(this, L.T("Invalid DC/AC ratio range. The lower value must be greater than 0 and lower than the upper value."), Title, StatusKind.Error);
            return;
        }

        Parameters.MinTempC = tMin;
        Parameters.MaxTempC = tMax;
        Parameters.SafetyMarginPct = marginPct;
        Parameters.UnbalancedLoadLimitVA = unbalancedKva * 1000;
        Parameters.SimplifiedRegistrationLimitVA = registrationKva * 1000;
        Parameters.FeedInLimitPct = feedInPct;
        Parameters.DcAcRatioMin = ratioMin;
        Parameters.DcAcRatioMax = ratioMax;

        DialogResult = true;
        Close();
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
