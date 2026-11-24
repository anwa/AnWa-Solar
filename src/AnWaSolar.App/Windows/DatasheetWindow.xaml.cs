using System.Runtime.InteropServices;
using System.Windows;

using AnWaSoft.Design.Wpf.Controls;

using AnWaSolar.Core.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace AnWaSolar.Windows;

/// <summary>Shows a PDF datasheet with the PDF viewer of WebView2 (zoom, search, print).</summary>
public partial class DatasheetWindow : ChromeWindow
{
    private readonly string _path;
    private readonly IUserDataLocation _userData;
    private readonly ILogger _logger;

    public DatasheetWindow(string path, string title, IUserDataLocation userData, ILogger logger)
    {
        _path = path;
        _userData = userData;
        _logger = logger;
        InitializeComponent();
        Title = $"Datasheet – {title}";
        Loaded += async (_, _) => await ShowPdfAsync();
    }

    private async Task ShowPdfAsync()
    {
        try
        {
            CoreWebView2Environment environment = await WebViewSupport.GetEnvironmentAsync(_userData);
            await Viewer.EnsureCoreWebView2Async(environment);
            Viewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Viewer.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                // Links in the datasheet open in the default browser, not in a WebView2 window.
                e.Handled = true;
                WebViewSupport.OpenExternal(e.Uri);
            };
            Viewer.CoreWebView2.Navigate(new Uri(_path).AbsoluteUri);
            _logger.LogInformation("Datasheet shown: {Path}.", _path);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            WebViewSupport.ResetEnvironment();
            _logger.LogError(ex, "The datasheet viewer (WebView2) could not be started.");
            Viewer.Visibility = Visibility.Collapsed;
            FallbackText.Visibility = Visibility.Visible;
            FallbackText.Text = $"The datasheet cannot be shown here, because the Microsoft Edge WebView2 Runtime could not be started. The file is: {_path}";
        }
    }
}
