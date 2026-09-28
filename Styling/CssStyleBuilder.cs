using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ShellOverlay.Logging;

namespace ShellOverlay.Styling;

public static class CssStyleBuilder
{
    // propertyName → функция, которая возвращает (targetType, Setter)
    private delegate IEnumerable<(Type Type, Setter Setter)> Handler(string value);

    private static readonly Dictionary<string, Handler> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["background"] = Background,
            ["background-color"] = Background,
            ["color"] = Foreground,

            ["border-color"] = BorderColor,
            ["border-width"] = BorderWidth,
            ["border-radius"] = BorderRadius,
            ["border-top-left-radius"] = (v) => RadiusCorner(v, c => new CornerRadius(c, 0, 0, 0)),
            ["border-top-right-radius"] = (v) => RadiusCorner(v, c => new CornerRadius(0, c, 0, 0)),
            ["border-bottom-right-radius"] = (v) => RadiusCorner(v, c => new CornerRadius(0, 0, c, 0)),
            ["border-bottom-left-radius"] = (v) => RadiusCorner(v, c => new CornerRadius(0, 0, 0, c)),
            ["box-shadow"] = BoxShadow,

            ["width"] = (v) => Length(v, Layoutable.WidthProperty),
            ["height"] = (v) => Length(v, Layoutable.HeightProperty),
            ["min-width"] = (v) => Length(v, Layoutable.MinWidthProperty),
            ["max-width"] = (v) => Length(v, Layoutable.MaxWidthProperty),
            ["min-height"] = (v) => Length(v, Layoutable.MinHeightProperty),
            ["max-height"] = (v) => Length(v, Layoutable.MaxHeightProperty),
            ["padding"] = Padding,
            ["margin"] = Margin,

            ["opacity"] = Opacity,
            ["visibility"] = Visibility,
            ["cursor"] = Cursor,
            ["transform"] = Transform,
            ["transition"] = Transition,

            ["font-size"] = FontSize,
            ["font-weight"] = FontWeight,
            ["font-style"] = FontStyle,
            ["letter-spacing"] = LetterSpacing,
            ["line-height"] = LineHeight,
            ["text-align"] = TextAlign,
        };

    // ════════════════════════════════════════════════════════════
    //  Public API
    // ════════════════════════════════════════════════════════════

    public static IEnumerable<Style> Build(IEnumerable<CssRule> rules)
    {
        foreach (var rule in rules)
        {
            foreach (var style in BuildStylesForRule(rule))
                yield return style;
        }
    }

    private static IEnumerable<Style> BuildStylesForRule(CssRule rule)
    {
        // 1. Собрать setter'ы, сгруппированные по типу-владельцу свойства
        var byType = new Dictionary<Type, List<Setter>>();

        foreach (var (name, value) in rule.Properties)
        {
            if (!Registry.TryGetValue(name, out var handler))
            {
                OverlayLog.Warn($"CSS: unknown property '{name}' in '{rule.Selector}'");
                continue;
            }

            try
            {
                foreach (var (type, setter) in handler(value))
                {
                    if (!byType.TryGetValue(type, out var list))
                        byType[type] = list = new List<Setter>();
                    list.Add(setter);
                }
            }
            catch (Exception ex)
            {
                OverlayLog.Warn($"CSS: '{name}: {value}' failed ({ex.GetType().Name}: {ex.Message})");
            }
        }

        // 2. Для каждого типа — свой Style со своим селектором.
        foreach (var (type, setters) in byType)
        {
            var selector = StyleSelectorFactory.ForType(rule, type);
            if (selector is null)
            {
                OverlayLog.Warn($"CSS: no selector for type {type.Name} in '{rule.Selector}'");
                continue;
            }

            var style = new Style(selector);
            foreach (var s in setters)
                style.Setters.Add(s);

            OverlayLog.Info($"CSS: '{rule.Selector}' → {type.Name} ({setters.Count} setter(s))");
            yield return style;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Handlers — каждый возвращает (TargetType, Setter)
    // ════════════════════════════════════════════════════════════

    private static IEnumerable<(Type, Setter)> Background(string value)
    {
        if (!CssValues.TryParseBrush(value, out var brush)) yield break;
        yield return (typeof(Border), new Setter(Border.BackgroundProperty, brush));
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.BackgroundProperty, brush));
        yield return (typeof(Panel), new Setter(Panel.BackgroundProperty, brush));
    }

    private static IEnumerable<(Type, Setter)> Foreground(string value)
    {
        if (!CssValues.TryParseColor(value, out var c)) yield break;
        var brush = new SolidColorBrush(c);
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.ForegroundProperty, brush));
        yield return (typeof(TextBlock), new Setter(TextBlock.ForegroundProperty, brush));
    }

    private static IEnumerable<(Type, Setter)> BorderColor(string value)
    {
        if (!CssValues.TryParseColor(value, out var c)) yield break;
        var brush = new SolidColorBrush(c);
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.BorderBrushProperty, brush));
        yield return (typeof(Border), new Setter(Border.BorderBrushProperty, brush));
    }

    private static IEnumerable<(Type, Setter)> BorderWidth(string value)
    {
        if (!CssValues.TryParseLength(value, out var w)) yield break;
        var t = new Thickness(w);
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.BorderThicknessProperty, t));
        yield return (typeof(Border), new Setter(Border.BorderThicknessProperty, t));
    }

    private static IEnumerable<(Type, Setter)> BorderRadius(string value)
    {
        if (!CssValues.TryParseLength(value, out var r)) yield break;
        var cr = new CornerRadius(r);
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.CornerRadiusProperty, cr));
        yield return (typeof(Border), new Setter(Border.CornerRadiusProperty, cr));
    }

    private static IEnumerable<(Type, Setter)> RadiusCorner(string value, Func<double, CornerRadius> make)
    {
        if (!CssValues.TryParseLength(value, out var r)) yield break;
        yield return (typeof(Border), new Setter(Border.CornerRadiusProperty, make(r)));
    }

    private static IEnumerable<(Type, Setter)> BoxShadow(string value)
    {
        if (!CssValues.TryParseBoxShadows(value, out var sh)) yield break;
        yield return (typeof(Border), new Setter(Border.BoxShadowProperty, sh));
    }

    private static IEnumerable<(Type, Setter)> Length(string value, AvaloniaProperty<double> prop)
    {
        if (!CssValues.TryParseLength(value, out var l)) yield break;
        yield return (typeof(Layoutable), new Setter(prop, l));
    }

    private static IEnumerable<(Type, Setter)> Padding(string value)
    {
        if (!CssValues.TryParseThickness(value, out var t)) yield break;
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.PaddingProperty, t));
        yield return (typeof(Decorator), new Setter(Decorator.PaddingProperty, t));
        yield return (typeof(TextBlock), new Setter(TextBlock.PaddingProperty, t));
    }

    private static IEnumerable<(Type, Setter)> Margin(string value)
    {
        if (!CssValues.TryParseThickness(value, out var t)) yield break;
        yield return (typeof(Layoutable), new Setter(Layoutable.MarginProperty, t));
    }

    private static IEnumerable<(Type, Setter)> Opacity(string value)
    {
        if (!CssValues.TryParseLength(value, out var o)) yield break;
        yield return (typeof(Visual), new Setter(Visual.OpacityProperty, Math.Clamp(o, 0, 1)));
    }

    private static IEnumerable<(Type, Setter)> Visibility(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        bool? visible = v switch
        {
            "hidden" => false,
            "visible" => true,
            _ => null,
        };
        if (visible is null) yield break;
        yield return (typeof(Visual), new Setter(Visual.IsVisibleProperty, visible.Value));
    }

    private static IEnumerable<(Type, Setter)> Cursor(string value)
    {
        if (!CssValues.TryParseCursor(value, out var c)) yield break;
        yield return (typeof(InputElement), new Setter(InputElement.CursorProperty, c));
    }

    private static IEnumerable<(Type, Setter)> Transform(string value)
    {
        if (!CssValues.TryParseTransform(value, out var t)) yield break;
        yield return (typeof(Visual), new Setter(Visual.RenderTransformProperty, t));
    }

    private static IEnumerable<(Type, Setter)> Transition(string value)
    {
        if (!CssValues.TryParseTransitions(value, out var t)) yield break;
        yield return (typeof(Animatable), new Setter(Animatable.TransitionsProperty, t));
    }

    private static IEnumerable<(Type, Setter)> FontSize(string value)
    {
        if (!CssValues.TryParseLength(value, out var s)) yield break;
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.FontSizeProperty, s));
        yield return (typeof(TextBlock), new Setter(TextBlock.FontSizeProperty, s));
    }

    private static IEnumerable<(Type, Setter)> FontWeight(string value)
    {
        if (!CssValues.TryParseFontWeight(value, out var w)) yield break;
        yield return (typeof(TemplatedControl), new Setter(TemplatedControl.FontWeightProperty, w));
        yield return (typeof(TextBlock), new Setter(TextBlock.FontWeightProperty, w));
    }

    private static IEnumerable<(Type, Setter)> FontStyle(string value)
    {
        if (!CssValues.TryParseFontStyle(value, out var s)) yield break;
        yield return (typeof(TextBlock), new Setter(TextBlock.FontStyleProperty, s));
    }

    private static IEnumerable<(Type, Setter)> LetterSpacing(string value)
    {
        if (!CssValues.TryParseLength(value, out var s)) yield break;
        yield return (typeof(TextBlock), new Setter(TextBlock.LetterSpacingProperty, s));
    }

    private static IEnumerable<(Type, Setter)> LineHeight(string value)
    {
        if (!CssValues.TryParseLength(value, out var h)) yield break;
        yield return (typeof(TextBlock), new Setter(TextBlock.LineHeightProperty, h));
    }

    private static IEnumerable<(Type, Setter)> TextAlign(string value)
    {
        if (CssValues.TryParseTextAlignment(value, out var a))
            yield return (typeof(TextBlock), new Setter(TextBlock.TextAlignmentProperty, a));

        if (CssValues.TryParseHorizontalAlignment(value, out var h))
            yield return (typeof(ContentControl), new Setter(ContentControl.HorizontalContentAlignmentProperty, h));
    }
}