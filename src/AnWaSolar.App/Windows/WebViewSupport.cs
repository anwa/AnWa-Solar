using System.Diagnostics;

using AnWaSolar.Core.Services;

using Microsoft.Web.WebView2.Core;

namespace AnWaSolar.Windows;

/// <summary>
/// Shared WebView2 setup of the report preview and the manual: one environment with its data folder in the per-user
/// data folder (the installation folder is read-only), the colour scheme of the application theme and external links.
/// </summary>
internal static class WebViewSupport
{
    private static Task<CoreWebView2Environment>? s_environment;

    /// <summary>The environment, created on first use.</summary>
    public static Task<CoreWebView2Environment> GetEnvironmentAsync(IUserDataLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return s_environment ??= CoreWebView2Environment.CreateAsync(null, Path.Combine(location.DirectoryPath, "WebView2"));
    }

    /// <summary>Forgets a failed environment so that the next attempt starts again.</summary>
    public static void ResetEnvironment() => s_environment = null;

    /// <summary>Lets pages with <c>color-scheme: light dark</c> follow the application theme.</summary>
    public static void ApplyColorScheme(CoreWebView2? core, bool dark)
    {
        if (core is not null)
        {
            core.Profile.PreferredColorScheme = dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
        }
    }

    /// <summary>Opens a web or mail link in the default application; other schemes are ignored.</summary>
    public static bool OpenExternal(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
            || parsed.Scheme is not ("https" or "http" or "mailto"))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
        return true;
    }
}
