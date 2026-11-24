using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using AnWaSolar.Core;
using AnWaSolar.Core.Manual;
using AnWaSolar.Core.Services;
using AnWaSolar.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace AnWaSolar.Windows;

/// <summary>
/// The user manual: the Markdown pages embedded in the executable, shown with WebView2. Pages, links between pages
/// and KaTeX are served from a local host name; web and mail links open in the default application.
/// </summary>
public partial class ManualWindow : ChromeWindow
{
    private const string Host = "manual.anwasolar.invalid";
    private static readonly string s_katexDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "katex");

    private readonly IMarkdownService _markdown;
    private readonly IUserDataLocation _userData;
    private readonly ThemeService _theme;
    private readonly ILogger _logger;
    private readonly ManualIndex _index;
    private string? _pendingHighlight;

    public ManualWindow(IMarkdownService markdown, IUserDataLocation userData, ThemeService theme, ILogger logger)
    {
        _markdown = markdown;
        _userData = userData;
        _theme = theme;
        _logger = logger;
        InitializeComponent();

        _index = new ManualIndex(ManualResources.Load(typeof(ManualWindow).Assembly));
        Title = _index.Title;
        PageTree.ItemsSource = _index.Tree;

        InputBindings.Add(new KeyBinding(new ActionCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(GoBack), Key.Left, ModifierKeys.Alt));
        _theme.EffectiveThemeChanged += OnEffectiveThemeChanged;
        Closed += (_, _) => _theme.EffectiveThemeChanged -= OnEffectiveThemeChanged;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private static string UrlOf(string path, string? anchor = null)
        => $"https://{Host}/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString))
           + (string.IsNullOrEmpty(anchor) ? string.Empty : "#" + anchor);

    private async Task InitializeAsync()
    {
        if (_index.IsEmpty || _index.StartPage is null)
        {
            ShowUnavailable("The manual is not included in this build.");
            return;
        }

        try
        {
            CoreWebView2Environment environment = await WebViewSupport.GetEnvironmentAsync(_userData);
            await PageView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            WebViewSupport.ResetEnvironment();
            _logger.LogError(ex, "The manual (WebView2) could not be started.");
            ShowUnavailable("The manual needs the Microsoft Edge WebView2 Runtime, which could not be started. See the log for details.");
            return;
        }

        CoreWebView2 core = PageView.CoreWebView2;
        WebViewSupport.ApplyColorScheme(core, _theme.IsDark);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.AddWebResourceRequestedFilter($"https://{Host}/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnResourceRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.NavigationCompleted += OnNavigationCompleted;
        core.Navigate(UrlOf(_index.StartPage));
    }

    private void ShowUnavailable(string message)
    {
        PageView.Visibility = Visibility.Collapsed;
        UnavailableText.Text = message;
        UnavailableText.Visibility = Visibility.Visible;
    }

    private void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        CoreWebView2Environment environment = PageView.CoreWebView2.Environment;
        string path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath);
        if (HtmlDocument.TryResolveAsset(s_katexDirectory, path, out string file, out string contentType))
        {
            e.Response = environment.CreateWebResourceResponse(File.OpenRead(file), 200, "OK", $"Content-Type: {contentType}");
            return;
        }

        string page = ManualIndex.Normalize(path);
        string body = _index.Text(page) is { } markdown
            ? _markdown.ToHtml(markdown)
            : $"<h1>Page not found</h1><p>{System.Net.WebUtility.HtmlEncode(page)}</p>";
        string html = HtmlDocument.Create(body, _index.TitleOf(page));
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        e.Response = environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
    }

    // Links to other hosts leave the manual: web and mail links open in the default application.
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith($"https://{Host}/", StringComparison.Ordinal))
        {
            return;
        }

        e.Cancel = true;
        OpenExternal(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.StartsWith($"https://{Host}/", StringComparison.Ordinal))
        {
            PageView.CoreWebView2.Navigate(e.Uri);
        }
        else
        {
            OpenExternal(e.Uri);
        }
    }

    private void OpenExternal(string uri)
    {
        try
        {
            WebViewSupport.OpenExternal(uri);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Link {Uri} could not be opened.", uri);
            AppDialog.Show(this, $"The link could not be opened: {ex.Message}", "Manual", DialogKind.Warning);
        }
    }

    // Highlights the first search hit on the page that was opened from the search results.
    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        BackButton.IsEnabled = PageView.CoreWebView2.CanGoBack;
        if (_pendingHighlight is not { } query || !e.IsSuccess)
        {
            return;
        }

        _pendingHighlight = null;
        try
        {
            await PageView.CoreWebView2.ExecuteScriptAsync($"window.find({JsonSerializer.Serialize(query)}, false, false, true);");
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            _logger.LogWarning(ex, "Search hit could not be highlighted.");
        }
    }

    private void OnEffectiveThemeChanged(object? sender, bool dark) => WebViewSupport.ApplyColorScheme(PageView.CoreWebView2, dark);

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ManualNode { Path: { } path })
        {
            Open(path);
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim();
        bool searching = query.Length >= 2;
        IReadOnlyList<ManualHit> hits = searching ? _index.Search(query) : [];
        HitList.ItemsSource = hits;
        HitList.Visibility = searching && hits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoHitsText.Visibility = searching && hits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageTree.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnHitSelected(object sender, SelectionChangedEventArgs e)
    {
        if (HitList.SelectedItem is ManualHit hit)
        {
            _pendingHighlight = SearchBox.Text.Trim();
            Open(hit.Path);
            HitList.SelectedItem = null;
        }
    }

    private void Open(string path) => PageView.CoreWebView2?.Navigate(UrlOf(path));

    private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        if (PageView.CoreWebView2 is { CanGoBack: true } core)
        {
            core.GoBack();
        }
    }

    /// <summary>Minimal ICommand for key bindings.</summary>
    private sealed class ActionCommand(Action action) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
