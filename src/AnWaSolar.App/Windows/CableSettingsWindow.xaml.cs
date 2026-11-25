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
            || !TryParse(AcDropInput.Text, out double acDrop) || acDrop <= 0 || acDrop > 10)
        {
            Fail("The voltage drop limits must be above 0 and at most 10 %.");
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
        DialogResult = true;
    }

    private void Fail(string message) => MessageDialog.Show(this, message, Title, StatusKind.Error);

    private static bool TryParse(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
