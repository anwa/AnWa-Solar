using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;

using AnWaSolar.Core;
using AnWaSolar.Core.Logging;
using AnWaSolar.Core.Services;

using Microsoft.Web.WebView2.Core;

namespace AnWaSolar.Windows;

public partial class AboutWindow : ChromeWindow
{
    private readonly AboutFacts _facts;

    public AboutWindow(IUserDataLocation userData, ILogFilePathProvider logPaths)
    {
        ArgumentNullException.ThrowIfNull(userData);
        ArgumentNullException.ThrowIfNull(logPaths);
        InitializeComponent();

        Assembly app = typeof(AboutWindow).Assembly;
        _facts = new AboutFacts(
            Version: AppVersion.Current,
            Commit: AppInfo.BuildInfo(app, "SourceCommit"),
            BuildDate: AppInfo.BuildInfo(app, "BuildDate"),
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            MaterialDesignVersion: typeof(MaterialDesignThemes.Wpf.PackIcon).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            WebView2Version: WebView2Version(),
            InstallationFolder: AppContext.BaseDirectory.TrimEnd('\\'),
            SettingsFile: Path.Combine(userData.DirectoryPath, SettingsService.FileName),
            LogFolder: Path.GetDirectoryName(logPaths.GetCurrentLogFilePath()) ?? string.Empty,
            WebView2DataFolder: Path.Combine(userData.DirectoryPath, "WebView2"),
            Copyright: AppInfo.Copyright(app));

        TitleText.Text = AboutContent.Title(_facts);
        SectionList.ItemsSource = AboutContent.Sections(_facts);
        CopyrightText.Text = _facts.Copyright;
    }

    private static string WebView2Version()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString() ?? "not installed";
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return "not installed";
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AboutContent.ToText(_facts));
            CopiedText.Text = "Copied to the clipboard.";
        }
        catch (COMException)
        {
            CopiedText.Text = "The clipboard is in use; try again.";
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
