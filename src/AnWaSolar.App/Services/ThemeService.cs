using System.Windows;
using System.Windows.Media;

using AnWaSolar.Core.Services;

using MaterialDesignThemes.Wpf;

using Microsoft.Win32;

namespace AnWaSolar.Services;

/// <summary>
/// Applies the System, Light or Dark theme. In System mode the theme follows the Windows app theme and changes live
/// when the user switches it in Windows.
/// </summary>
public sealed class ThemeService : IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // Indigo 500 in light mode; the lighter Indigo 300 in dark mode keeps primary texts and borders readable.
    private static readonly Color s_lightPrimary = (Color)ColorConverter.ConvertFromString("#3F51B5");
    private static readonly Color s_darkPrimary = (Color)ColorConverter.ConvertFromString("#7986CB");

    private bool _subscribed;

    /// <summary>Raised after the effective theme changed. The argument is true for dark.</summary>
    public event EventHandler<bool>? EffectiveThemeChanged;

    public AppTheme Mode { get; private set; } = AppTheme.System;

    public bool IsDark { get; private set; }

    public void Apply(AppTheme mode)
    {
        Mode = mode;
        if (mode == AppTheme.System && !_subscribed)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _subscribed = true;
        }
        else if (mode != AppTheme.System && _subscribed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _subscribed = false;
        }

        ApplyEffective(mode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsWindowsAppThemeDark(),
        });
    }

    /// <summary>The next mode of the toggle button: System → Light → Dark → System.</summary>
    public static AppTheme Next(AppTheme mode) => mode switch
    {
        AppTheme.System => AppTheme.Light,
        AppTheme.Light => AppTheme.Dark,
        _ => AppTheme.System,
    };

    /// <summary>Reads the Windows app theme; light when the value is missing.</summary>
    public static bool IsWindowsAppThemeDark()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _subscribed = false;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Mode == AppTheme.System && e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            Application.Current?.Dispatcher.BeginInvoke(() => ApplyEffective(IsWindowsAppThemeDark()));
        }
    }

    // Light and dark values of the application brushes defined in Resources/Theme.xaml. The title bar keeps the
    // indigo of the app in both themes (darker in dark mode); the state colours are lighter in dark mode for contrast.
    private static readonly (string Key, string Light, string Dark)[] s_appBrushes =
    [
        ("App.TitleBar", "#3F51B5", "#283593"),
        ("App.TextMuted", "#5F6368", "#A9AEB8"),
        ("App.PanelBackground", "#F4F5FA", "#181A20"),
        ("App.RowAlternate", "#0A3F51B5", "#147986CB"),
        ("State.Ok", "#2E7D32", "#81C784"),
        ("State.Warning", "#B26A00", "#FFB74D"),
        ("State.Error", "#C62828", "#EF9A9A"),
        ("State.Neutral", "#5F6368", "#A9AEB8"),
        ("State.OkTint", "#E8F5E9", "#1E3324"),
        ("State.WarningTint", "#FFF4E0", "#3A2E17"),
        ("State.ErrorTint", "#FDECEA", "#3D1F22"),
        ("State.NeutralTint", "#EEEFF3", "#2A2D35"),
    ];

    private static void ApplyAppBrushes(bool dark)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        foreach ((string key, string light, string darkValue) in s_appBrushes)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkValue : light));
            brush.Freeze();
            app.Resources[key] = brush;
        }
    }

    private void ApplyEffective(bool dark)
    {
        var palette = new PaletteHelper();
        Theme theme = palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(dark ? s_darkPrimary : s_lightPrimary);
        palette.SetTheme(theme);
        ApplyAppBrushes(dark);

        bool changed = IsDark != dark;
        IsDark = dark;
        if (changed)
        {
            EffectiveThemeChanged?.Invoke(this, dark);
        }
    }
}
