using System.Windows;

using MaterialDesignThemes.Wpf;

namespace AnWaSolar.Windows;

/// <summary>Kind of an <see cref="AppDialog"/> message.</summary>
public enum DialogKind
{
    Info,
    Warning,
    Error,
}

/// <summary>Message dialog in the application style, used instead of the Windows message box.</summary>
public partial class AppDialog : ChromeWindow
{
    private AppDialog(string message, string title, DialogKind kind)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        (PackIconKind icon, string brush) = kind switch
        {
            DialogKind.Error => (PackIconKind.AlertCircle, "State.Error"),
            DialogKind.Warning => (PackIconKind.AlertOutline, "State.Warning"),
            _ => (PackIconKind.InformationOutline, "MaterialDesign.Brush.Primary"),
        };
        KindIcon.Kind = icon;
        KindIcon.SetResourceReference(ForegroundProperty, brush);
    }

    /// <summary>Shows a modal message. Without an owner the visible main window is used.</summary>
    public static void Show(Window? owner, string message, string title, DialogKind kind = DialogKind.Info)
    {
        var dialog = new AppDialog(message, title, kind);
        owner ??= Application.Current?.MainWindow is { IsVisible: true } main ? main : null;
        if (owner is not null && !ReferenceEquals(owner, dialog))
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
