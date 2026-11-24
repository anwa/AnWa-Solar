using AnWaSoft.Design.Wpf.Theming;

using AnWaSolar.Core.Services;

namespace AnWaSolar.Services;

/// <summary>
/// Converts between the theme saved in <c>settings.json</c> (<see cref="AppTheme"/>, Core) and the preference of the
/// design library's <see cref="ThemeService"/>. Explicit, so that it does not depend on the member order.
/// </summary>
public static class ThemeMapping
{
    public static ThemePreference ToPreference(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemePreference.Light,
        AppTheme.Dark => ThemePreference.Dark,
        _ => ThemePreference.System,
    };

    public static AppTheme ToAppTheme(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => AppTheme.Light,
        ThemePreference.Dark => AppTheme.Dark,
        _ => AppTheme.System,
    };
}
