using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ShellOverlay.Styling;

/// <summary>
/// Строит Avalonia-Selector из CSS-правила и типа-цели.
/// Is&lt;T&gt;() работает только для StyledElement — для базовых типов
/// (Visual, Animatable, Layoutable, InputElement) используем Is&lt;StyledElement&gt;.
/// </summary>
internal static class StyleSelectorFactory
{
    public static Func<Selector?, Selector>? ForType(CssRule rule, Type targetType)
    {
        var selectorType = ResolveSelectorType(targetType);
        if (selectorType is null) return null;

        return Build(selectorType, rule.ClassName, rule.Id, rule.IsHover);
    }

    /// <summary>
    /// Is&lt;T&gt;() требует T : StyledElement. Если тип не StyledElement —
    /// используем StyledElement, setter с правильным AvaloniaProperty
    /// применится через наследование.
    /// </summary>
    private static Type? ResolveSelectorType(Type targetType)
    {
        if (typeof(StyledElement).IsAssignableFrom(targetType))
            return targetType;

        // Visual, Animatable, Layoutable, InputElement → StyledElement
        if (targetType == typeof(Layoutable)) return typeof(StyledElement);
        if (targetType == typeof(Visual)) return typeof(StyledElement);
        if (targetType == typeof(Animatable)) return typeof(StyledElement);
        if (targetType == typeof(InputElement)) return typeof(StyledElement);

        return null;
    }

    private static Func<Selector?, Selector> Build(Type type, string cls, string id, bool hover)
        => x =>
        {
            Selector s = SelectForType(x!, type);

            if (!string.IsNullOrEmpty(id))
                s = s.Name(id);
            else if (!string.IsNullOrEmpty(cls))
                s = s.Class(cls);

            if (hover)
                s = s.Class(":pointerover");

            return s;
        };

    private static Selector SelectForType(Selector x, Type type)
    {
        if (type == typeof(Border)) return x.Is<Border>();
        if (type == typeof(Decorator)) return x.Is<Decorator>();
        if (type == typeof(Button)) return x.Is<Button>();
        if (type == typeof(TemplatedControl)) return x.Is<TemplatedControl>();
        if (type == typeof(TextBlock)) return x.Is<TextBlock>();
        if (type == typeof(StackPanel)) return x.Is<StackPanel>();
        if (type == typeof(Panel)) return x.Is<Panel>();
        if (type == typeof(ContentControl)) return x.Is<ContentControl>();
        if (type == typeof(Image)) return x.Is<Image>();
        return x.Is<StyledElement>();
    }
}