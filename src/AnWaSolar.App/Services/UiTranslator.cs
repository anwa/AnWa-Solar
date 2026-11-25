using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

using MaterialDesignThemes.Wpf;

namespace AnWaSolar.Services;

/// <summary>
/// Translates the fixed English texts of the windows when an element is loaded: texts of text blocks, string contents,
/// headers, tool tips, hints, helper texts, accessible names and window titles. Bound texts and the text of input
/// boxes stay untouched; texts that the code sets later are translated by the code itself. WPF raises the loaded event
/// only for elements with their own handler, so a loaded window translates its whole logical and visual tree and
/// sets the inherited property <see cref="ActiveProperty"/>; elements that join the tree later, such as the content of
/// data templates and generated items, inherit it and are translated then.
/// </summary>
public static class UiTranslator
{
    private static bool s_registered;

    /// <summary>Inherited marker of a translated window; an element that inherits it is translated.</summary>
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.RegisterAttached("Active", typeof(bool), typeof(UiTranslator),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, (d, e) =>
        {
            if (e.NewValue is true)
            {
                Translate(d);
            }
        }));

    /// <summary>Gets whether the element belongs to a translated window.</summary>
    public static bool GetActive(DependencyObject element) => (bool)element.GetValue(ActiveProperty);

    /// <summary>Sets the marker of a translated window.</summary>
    public static void SetActive(DependencyObject element, bool value) => element.SetValue(ActiveProperty, value);

    /// <summary>Translates every element when it is loaded; does nothing for English.</summary>
    public static void Register()
    {
        if (s_registered || L.Language == L.English)
        {
            return;
        }

        s_registered = true;
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) => Translate(s as DependencyObject)), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            TranslateTree(s as DependencyObject);
            if (s is Window window)
            {
                SetActive(window, true);
            }
        }), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((s, _) => TranslateTree(s as DependencyObject)), handledEventsToo: true);
    }

    /// <summary>Translates the texts of an element and of all elements below it in the logical and visual tree.</summary>
    public static void TranslateTree(DependencyObject? root)
    {
        if (root is null || L.Language == L.English)
        {
            return;
        }

        var seen = new HashSet<DependencyObject>();
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            DependencyObject element = pending.Pop();
            if (!seen.Add(element))
            {
                continue;
            }

            Translate(element);
            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject node)
                {
                    pending.Push(node);
                }
            }

            if (element is Visual)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                {
                    pending.Push(VisualTreeHelper.GetChild(element, i));
                }
            }
        }
    }

    /// <summary>Translates the texts of one element.</summary>
    public static void Translate(DependencyObject? element)
    {
        if (element is null || L.Language == L.English)
        {
            return;
        }

        switch (element)
        {
            case TextBlock text:
                Replace(text, TextBlock.TextProperty);
                break;
            case Window window:
                Replace(window, Window.TitleProperty);
                break;
            case HeaderedContentControl headered:
                Replace(headered, HeaderedContentControl.HeaderProperty);
                Replace(headered, ContentControl.ContentProperty);
                break;
            case HeaderedItemsControl items:
                Replace(items, HeaderedItemsControl.HeaderProperty);
                break;
            case ContentControl content:
                Replace(content, ContentControl.ContentProperty);
                break;
            default:
                break;
        }

        Replace(element, FrameworkElement.ToolTipProperty);
        Replace(element, HintAssist.HintProperty);
        Replace(element, HintAssist.HelperTextProperty);
        Replace(element, AutomationProperties.NameProperty);
    }

    // Replaces a string value of the element or of its template that has a translation; bound values and values from
    // styles stay.
    private static void Replace(DependencyObject element, DependencyProperty property)
    {
        ValueSource source = DependencyPropertyHelper.GetValueSource(element, property);
        if (source.IsExpression || source.IsAnimated || source.IsCoerced
            || source.BaseValueSource is not (BaseValueSource.Local or BaseValueSource.ParentTemplate))
        {
            return;
        }

        if (element.GetValue(property) is string text && text.Length > 0 && L.TryTranslate(text, out string translated))
        {
            element.SetValue(property, translated);
        }
    }
}
