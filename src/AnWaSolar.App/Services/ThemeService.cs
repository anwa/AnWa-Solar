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

    private void ApplyEffective(bool dark)
    {
        var palette = new PaletteHelper();
        Theme theme = palette.GetTheme();
        theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
        theme.SetPrimaryColor(dark ? s_darkPrimary : s_lightPrimary);
        palette.SetTheme(theme);

        bool changed = IsDark != dark;
        IsDark = dark;
        if (changed)
        {
            EffectiveThemeChanged?.Invoke(this, dark);
        }
    }
}
