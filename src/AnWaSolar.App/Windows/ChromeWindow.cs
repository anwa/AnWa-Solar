using System.Windows;
using System.Windows.Input;

namespace AnWaSolar.Windows;

/// <summary>
/// Base class of all application windows. The style <c>AppWindow</c> replaces the Windows title bar with the
/// application's own title bar (icon, title, optional subtitle and content, window buttons); this class handles the
/// window button commands.
/// </summary>
public class ChromeWindow : Window
{
    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ChromeWindow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleBarContentProperty =
        DependencyProperty.Register(nameof(TitleBarContent), typeof(object), typeof(ChromeWindow));

    public ChromeWindow()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this)));
    }

    /// <summary>Small text next to the title, for example the version.</summary>
    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Content on the right side of the title bar, before the window buttons (for example toolbar buttons).</summary>
    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }
}
