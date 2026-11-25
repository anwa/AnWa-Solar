using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Calculation;
using AnWaSolar.Core.Datasheets;
using AnWaSolar.Core.Models;
using AnWaSolar.Core.Services;
using AnWaSolar.Services;

namespace AnWaSolar.Windows;

/// <summary>Battery module and bank configuration for the compatibility check with the hybrid inverter.</summary>
public partial class BatterySettingsWindow : ChromeWindow
{
    private readonly DatasheetService _datasheets;
    private readonly SeriesPicker<Battery> _picker;

    public BatterySettingsWindow(BatterySettings current, Inverter? inverter, DatasheetService datasheets)
    {
        _datasheets = datasheets;
        InitializeComponent();
        _picker = new SeriesPicker<Battery>(ManufacturerCombo, null, SeriesCombo, ModelCombo, null, entry =>
        {
            Selected = entry;
            UpdateDetails();
        });
        Settings = current.Clone();

        InverterNote.Text = inverter switch
        {
            null => "Select an inverter first.",
            { HasBatteryInput: true } => string.Create(CultureInfo.CurrentCulture,
                $"{inverter.Manufacturer} {inverter.Model}: battery {inverter.BatteryVoltageRangeV} V, max. charge {inverter.MaxBatteryChargeCurrentA:0.#} A, max. discharge {inverter.MaxBatteryDischargeCurrentA:0.#} A."),
            _ => $"{inverter.Manufacturer} {inverter.Model} has no battery input in the master data. Import its datasheet when it is a hybrid inverter.",
        };

        SeriesInput.Text = Settings.InSeries.ToString(CultureInfo.CurrentCulture);
        ParallelInput.Text = Settings.InParallel.ToString(CultureInfo.CurrentCulture);
        EnabledCheck.IsChecked = Settings.Enabled;
        LoadList(Settings.Battery);
        UpdateEnabled();
    }

    public BatterySettings Settings { get; }

    /// <summary>True when a datasheet was imported in this dialog; the master data was reloaded.</summary>
    public bool Imported { get; private set; }

    private Battery? Selected { get; set; }

    private void LoadList(SelectedRef? select) => _picker.Load(_datasheets.Store.Batteries, select?.Manufacturer, select?.Model);

    private void OnEnabledChanged(object sender, RoutedEventArgs e) => UpdateEnabled();

    private void UpdateEnabled() => BatteryPanel.IsEnabled = EnabledCheck.IsChecked == true;

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (await _datasheets.ImportAsync(this, DatasheetKind.Battery) is Battery battery)
        {
            Imported = true;
            EnabledCheck.IsChecked = true;
            LoadList(new SelectedRef { Manufacturer = battery.Manufacturer, Model = battery.Model });
        }
    }

    private void OnDatasheetClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not null)
        {
            _datasheets.Show(this, Selected);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        bool enabled = EnabledCheck.IsChecked == true;
        if (enabled)
        {
            if (Selected is null)
            {
                Fail("Select a battery, or import its datasheet.");
                return;
            }

            if (!TryCount(SeriesInput.Text, out int series) || !TryCount(ParallelInput.Text, out int parallel))
            {
                Fail("The numbers of modules in series and in parallel must be whole numbers from 1 to 99.");
                return;
            }

            Settings.Battery = new SelectedRef { Manufacturer = Selected.Manufacturer, Model = Selected.Model };
            Settings.InSeries = series;
            Settings.InParallel = parallel;
        }

        Settings.Enabled = enabled;
        DialogResult = true;
    }

    private void UpdateDetails()
    {
        DatasheetButton.IsEnabled = _datasheets.HasDatasheet(Selected);
        DetailGrid.Children.Clear();
        DetailGrid.RowDefinitions.Clear();
        if (Selected is not { } b)
        {
            AddDetail("Battery", "–");
            return;
        }

        CultureInfo c = CultureInfo.CurrentCulture;
        string Stated(double? value, string format, string unit) => value is { } v ? v.ToString(format, c) + unit : "not stated";
        string Count(int value) => value > 0 ? value.ToString(c) : "not stated";
        AddDetail("Nominal voltage", b.NominalVoltageV.ToString("0.#", c) + " V");
        AddDetail("Operating voltage range", b.VoltageRangeV + " V");
        AddDetail("Nominal capacity", Stated(b.NominalCapacityAh, "0.#", " Ah"));
        AddDetail("Nominal / usable energy", b.NominalEnergyKWh.ToString("0.##", c) + " kWh / " + Stated(b.UsableEnergyKWh, "0.##", " kWh"));
        AddDetail("Depth of discharge", Stated(b.DepthOfDischargePct, "0.#", " %"));
        AddDetail("Max. charge / discharge current", $"{b.MaxChargeCurrentA.ToString("0.#", c)} A / {b.MaxDischargeCurrentA.ToString("0.#", c)} A");
        AddDetail("Max. modules in series / parallel", $"{Count(b.MaxModulesInSeries)} / {Count(b.MaxModulesInParallel)}");
        AddDetail("Source", b.IsUserEntry ? (DatasheetButton.IsEnabled ? "Own master data, with datasheet" : "Own master data") : "Shipped master data");
    }

    private void AddDetail(string label, string value)
    {
        int row = DetailGrid.RowDefinitions.Count;
        DetailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var name = new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 4) };
        name.SetResourceReference(TextBlock.ForegroundProperty, "AnWaSoft.Brush.TextMuted");
        var text = new TextBlock { Text = value, Margin = new Thickness(0, 4, 0, 4), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(name, row);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 1);
        DetailGrid.Children.Add(name);
        DetailGrid.Children.Add(text);
    }

    private void Fail(string message) => MessageDialog.Show(this, message, Title, StatusKind.Error);

    private static bool TryCount(string? text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) && value is >= 1 and <= 99;
}
