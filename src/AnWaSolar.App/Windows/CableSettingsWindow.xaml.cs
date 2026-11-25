using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

using AnWaSoft.Design.Wpf.Controls;
using AnWaSoft.Design.Wpf.Status;

using AnWaSolar.Core.Calculation;

using MaterialDesignThemes.Wpf;

namespace AnWaSolar.Windows;

/// <summary>Cable runs and installation conditions for the cable and fuse sizing.</summary>
public partial class CableSettingsWindow : ChromeWindow
{
    private readonly List<TextBox> _dcLengths = [];
    private readonly CableTables _tables;

    public CableSettingsWindow(CableSettings current, CableTables tables, int mpptCount)
    {
        _tables = tables;
        InitializeComponent();
        Settings = current.Clone();
        CultureInfo c = CultureInfo.CurrentCulture;

        DcInstallationCombo.ItemsSource = Enum.GetValues<SolarCableInstallation>().Select(i => new KeyValuePair<SolarCableInstallation, string>(i, CableTexts.Installation(i))).ToList();
        DcInstallationCombo.DisplayMemberPath = "Value";
        DcInstallationCombo.SelectedValuePath = "Key";
        DcInstallationCombo.SelectedValue = Settings.DcInstallation;
        Fill(DcCrossSectionCombo, tables.CrossSections, Settings.DcCrossSectionMm2, automatic: true);
        Fill(LeadCrossSectionCombo, tables.CrossSections, Settings.ModuleLeadCrossSectionMm2, automatic: false);
        LeadLengthInput.Text = Settings.ModuleLeadLengthM.ToString(c);

        BusbarCheck.IsChecked = Settings.BatteryBusbar;
        Fill(BatteryCrossSectionCombo, tables.BatteryCable.CrossSections, Settings.BatteryCableCrossSectionMm2, automatic: true);
        Fill(MainCrossSectionCombo, tables.BatteryCable.CrossSections, Settings.BatteryMainCrossSectionMm2, automatic: true);
        BatteryLengthInput.Text = Settings.BatteryCableLengthM.ToString(c);
        MainLengthInput.Text = Settings.BatteryMainLengthM.ToString(c);
        BatteryAmbientInput.Text = Settings.BatteryAmbientC.ToString(c);
        BatteryDropInput.Text = (Settings.BatteryDropLimitPct ?? tables.BatteryCable.VoltageDropLimitPct).ToString(c);
        UpdateBusbar();

        for (int i = 1; i <= Math.Max(1, mpptCount); i++)
        {
            var box = new TextBox
            {
                Style = (Style)FindResource("MaterialDesignOutlinedTextBox"),
                Margin = new Thickness(0, 0, 0, 26),
                Text = Settings.DcLength(i).ToString(c),
            };
            HintAssist.SetHint(box, $"Cable length MPPT {i}");
            HintAssist.SetHelperText(box, "One way; 0 = not entered");
            TextFieldAssist.SetSuffixText(box, "m");
            AutomationProperties.SetName(box, $"Cable length MPPT {i}");
            DcLengthsPanel.Children.Add(box);
            _dcLengths.Add(box);
        }

        DcAmbientInput.Text = Settings.DcAmbientC.ToString(c);
        DcGroupingInput.Text = Settings.DcGroupedCircuits.ToString(c);
        DcDropInput.Text = (Settings.DcDropLimitPct ?? tables.VoltageDrop.DcLimitPct).ToString(c);

        AcMethodCombo.ItemsSource = tables.AcMethods.Select(m => new KeyValuePair<string, string>(m, CableTexts.AcMethod(m))).ToList();
        AcMethodCombo.DisplayMemberPath = "Value";
        AcMethodCombo.SelectedValuePath = "Key";
        AcMethodCombo.SelectedValue = tables.AcMethods.Contains(Settings.AcMethod) ? Settings.AcMethod : tables.AcMethods.FirstOrDefault();
        AcLengthInput.Text = Settings.AcLengthM.ToString(c);
        AcAmbientInput.Text = Settings.AcAmbientC.ToString(c);
        AcGroupingInput.Text = Settings.AcGroupedCircuits.ToString(c);
        AcDropInput.Text = (Settings.AcDropLimitPct ?? tables.VoltageDrop.AcLimitPct).ToString(c);
    }

    public CableSettings Settings { get; }

    // Cross-sections as choices; "Automatic" (null) first when the calculation may choose.
    private static void Fill(ComboBox combo, double[] areas, double? selected, bool automatic)
    {
        var items = new List<KeyValuePair<double?, string>>();
        if (automatic)
        {
            items.Add(new(null, "Automatic"));
        }

        items.AddRange(areas.Select(a => new KeyValuePair<double?, string>(a, a.ToString("0.#", CultureInfo.CurrentCulture) + " mm²")));
        combo.ItemsSource = items;
        combo.DisplayMemberPath = "Value";
        combo.SelectedItem = items.FirstOrDefault(i => i.Key == selected, items[0]);
    }

    private static double? Selected(ComboBox combo) => combo.SelectedItem is KeyValuePair<double?, string> item ? item.Key : null;

    private void OnBusbarChanged(object sender, RoutedEventArgs e) => UpdateBusbar();

    private void UpdateBusbar()
    {
        bool busbar = BusbarCheck.IsChecked == true;
        MainCablePanel.IsEnabled = busbar;
        BusbarText.Text = busbar
            ? "Each battery has a cable to the busbar, and a main cable runs from the busbar to the inverter. Each cable gets a fuse."
            : "Each battery has its own cable to the inverter (one battery: one cable). Each cable gets a fuse.";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var lengths = new List<double>();
        foreach (TextBox box in _dcLengths)
        {
            if (!TryParse(box.Text, out double length) || length < 0)
            {
                Fail("Cable lengths must be 0 or more metres.");
                return;
            }

            lengths.Add(length);
        }

        if (!TryParse(AcLengthInput.Text, out double acLength) || acLength < 0)
        {
            Fail("The AC line length must be 0 or more metres.");
            return;
        }

        if (!TryParse(DcAmbientInput.Text, out double dcAmbient) || CableTables.Factor(_tables.SolarCable.AmbientFactors, dcAmbient) is null
            || !TryParse(AcAmbientInput.Text, out double acAmbient) || CableTables.Factor(_tables.AcCable.AmbientFactors, acAmbient) is null)
        {
            Fail("An ambient temperature lies above the tables.");
            return;
        }

        if (!int.TryParse(DcGroupingInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int dcGroup) || dcGroup < 1
            || !int.TryParse(AcGroupingInput.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int acGroup) || acGroup < 1
            || CableTables.Factor(_tables.Grouping.Factors, dcGroup) is null || CableTables.Factor(_tables.Grouping.Factors, acGroup) is null)
        {
            Fail("The number of circuits laid together must be a whole number within the grouping table (1 to 9).");
            return;
        }

        if (!TryParse(DcDropInput.Text, out double dcDrop) || dcDrop <= 0 || dcDrop > 10
            || !TryParse(AcDropInput.Text, out double acDrop) || acDrop <= 0 || acDrop > 10
            || !TryParse(BatteryDropInput.Text, out double batteryDrop) || batteryDrop <= 0 || batteryDrop > 10)
        {
            Fail("The voltage drop limits must be above 0 and at most 10 %.");
            return;
        }

        if (!TryParse(LeadLengthInput.Text, out double leadLength) || leadLength <= 0 || leadLength > 10)
        {
            Fail("The length per module lead must be above 0 and at most 10 m.");
            return;
        }

        if (!TryParse(BatteryLengthInput.Text, out double batteryLength) || batteryLength < 0
            || !TryParse(MainLengthInput.Text, out double mainLength) || mainLength < 0)
        {
            Fail("The battery cable lengths must be 0 or more metres.");
            return;
        }

        if (!TryParse(BatteryAmbientInput.Text, out double batteryAmbient) || CableTables.Factor(_tables.AcCable.AmbientFactors, batteryAmbient) is null)
        {
            Fail("The ambient temperature of the battery cables lies above the tables.");
            return;
        }

        Settings.DcLengthsM = lengths;
        Settings.DcInstallation = (SolarCableInstallation)DcInstallationCombo.SelectedValue;
        Settings.DcAmbientC = dcAmbient;
        Settings.DcGroupedCircuits = dcGroup;
        Settings.DcDropLimitPct = dcDrop;
        Settings.AcLengthM = acLength;
        Settings.AcMethod = (string)AcMethodCombo.SelectedValue;
        Settings.AcAmbientC = acAmbient;
        Settings.AcGroupedCircuits = acGroup;
        Settings.AcDropLimitPct = acDrop;
        Settings.DcCrossSectionMm2 = Selected(DcCrossSectionCombo);
        Settings.ModuleLeadLengthM = leadLength;
        Settings.ModuleLeadCrossSectionMm2 = Selected(LeadCrossSectionCombo) ?? Settings.ModuleLeadCrossSectionMm2;
        Settings.BatteryBusbar = BusbarCheck.IsChecked == true;
        Settings.BatteryCableLengthM = batteryLength;
        Settings.BatteryCableCrossSectionMm2 = Selected(BatteryCrossSectionCombo);
        Settings.BatteryMainLengthM = mainLength;
        Settings.BatteryMainCrossSectionMm2 = Selected(MainCrossSectionCombo);
        Settings.BatteryAmbientC = batteryAmbient;
        Settings.BatteryDropLimitPct = batteryDrop;
        DialogResult = true;
    }

    private void Fail(string message) => MessageDialog.Show(this, message, Title, StatusKind.Error);

    private static bool TryParse(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
