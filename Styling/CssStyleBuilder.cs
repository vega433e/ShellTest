using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using ShellOverlay.Logging;

namespace ShellOverlay.Styling;

public static class CssStyleBuilder
{
    public static IEnumerable<Style> Build(IEnumerable<CssRule> rules)
    {
        foreach (var rule in rules)
        {
            Style? style;
            try
            {
                style = CreateStyle(rule);
            }
            catch (Exception ex)
            {
                OverlayLog.Warn($"CSS selector '{rule.Selector}' skipped: {ex.Message}");
                continue;
            }

            if (style is null) continue;
            ApplySetters(style, rule);
            yield return style;
        }
    }

    private static Style? CreateStyle(CssRule rule)
    {
        var type = rule.TypeName.ToLowerInvariant();
        var cls = rule.ClassName;
        var id = rule.Id;
        var hasClass = !string.IsNullOrEmpty(cls);
        var hasId = !string.IsNullOrEmpty(id);

        if (hasId)
            return new Style(x => x.Name(id));

        if (type is "button")
        {
            if (hasClass && rule.IsHover) return new Style(x => x.OfType<Button>().Class(cls).Pointerover());
            if (hasClass) return new Style(x => x.OfType<Button>().Class(cls));
            return rule.IsHover ? new Style(x => x.OfType<Button>().Pointerover()) : new Style(x => x.OfType<Button>());
        }

        if (hasClass && rule.IsHover)
            return new Style(x => x.OfType<TemplatedControl>().Class(cls).Pointerover());

        if (hasClass)
            return new Style(x => x.OfType<Control>().Class(cls));

        return null;
    }

    private static void ApplySetters(Style style, CssRule rule)
    {
        foreach (var (key, value) in rule.Properties)
        {
            switch (key.ToLowerInvariant())
            {
                case "background":
                case "background-color":
                    if (CssParser.TryParseColor(value, out var bg))
                    {
                        var brush = new SolidColorBrush(bg);
                        style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, brush));
                        style.Setters.Add(new Setter(Border.BackgroundProperty, brush));
                        style.Setters.Add(new Setter(Panel.BackgroundProperty, brush));
                    }
                    break;

                case "color":
                    if (CssParser.TryParseColor(value, out var fg))
                    {
                        var brush = new SolidColorBrush(fg);
                        style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, brush));
                        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, brush));
                    }
                    break;

                case "border-color":
                    if (CssParser.TryParseColor(value, out var bc))
                    {
                        var brush = new SolidColorBrush(bc);
                        style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, brush));
                        style.Setters.Add(new Setter(Border.BorderBrushProperty, brush));
                    }
                    break;

                case "border-width":
                    if (CssParser.TryParseDouble(value, out var bw))
                    {
                        var thickness = new Avalonia.Thickness(bw);
                        style.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, thickness));
                        style.Setters.Add(new Setter(Border.BorderThicknessProperty, thickness));
                    }
                    break;

                case "border-radius":
                    if (CssParser.TryParseDouble(value, out var radius))
                    {
                        var cr = new CornerRadius(radius);
                        style.Setters.Add(new Setter(TemplatedControl.CornerRadiusProperty, cr));
                        style.Setters.Add(new Setter(Border.CornerRadiusProperty, cr));
                    }
                    break;

                case "padding":
                    if (CssParser.TryParseThickness(value, out var pad))
                    {
                        style.Setters.Add(new Setter(TemplatedControl.PaddingProperty, pad));
                        style.Setters.Add(new Setter(Decorator.PaddingProperty, pad));
                        style.Setters.Add(new Setter(TextBlock.PaddingProperty, pad));
                    }
                    break;

                case "margin":
                    if (CssParser.TryParseThickness(value, out var margin))
                        style.Setters.Add(new Setter(Layoutable.MarginProperty, margin));
                    break;

                case "width":
                    if (CssParser.TryParseDouble(value, out var w))
                        style.Setters.Add(new Setter(Layoutable.WidthProperty, w));
                    break;

                case "height":
                    if (CssParser.TryParseDouble(value, out var h))
                        style.Setters.Add(new Setter(Layoutable.HeightProperty, h));
                    break;

                case "opacity":
                    if (CssParser.TryParseDouble(value, out var op))
                        style.Setters.Add(new Setter(Visual.OpacityProperty, op));
                    break;

                case "font-size":
                    if (CssParser.TryParseDouble(value, out var fs))
                    {
                        style.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, fs));
                        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, fs));
                    }
                    break;

                case "font-weight":
                    if (TryParseFontWeight(value, out var fw))
                    {
                        style.Setters.Add(new Setter(TemplatedControl.FontWeightProperty, fw));
                        style.Setters.Add(new Setter(TextBlock.FontWeightProperty, fw));
                    }
                    break;

                case "transform":
                    try
                    {
                        style.Setters.Add(new Setter(Visual.RenderTransformProperty, TransformOperations.Parse(value)));
                    }
                    catch
                    {
                        OverlayLog.Warn($"Unknown transform '{value}'");
                    }
                    break;

                case "transition":
                    if (TryParseTransition(value, out var transitions))
                        style.Setters.Add(new Setter(Animatable.TransitionsProperty, transitions));
                    break;
            }
        }
    }

    private static bool TryParseFontWeight(string value, out FontWeight weight)
    {
        weight = FontWeight.Normal;
        switch (value.ToLowerInvariant())
        {
            case "normal": weight = FontWeight.Normal; return true;
            case "bold": weight = FontWeight.Bold; return true;
            case "semibold": weight = FontWeight.SemiBold; return true;
            case "light": weight = FontWeight.Light; return true;
        }
        if (int.TryParse(value, out var n))
        {
            weight = (FontWeight)n;
            return true;
        }
        return false;
    }

    private static bool TryParseTransition(string value, out Transitions transitions)
    {
        transitions = [];
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var duration = TimeSpan.FromMilliseconds(150);
        foreach (var part in parts)
        {
            if (CssParser.TryParseTime(part, out var t))
                duration = t;
        }

        if (value.Contains("transform", StringComparison.OrdinalIgnoreCase))
        {
            transitions.Add(new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = duration,
            });
        }

        if (value.Contains("background", StringComparison.OrdinalIgnoreCase))
        {
            transitions.Add(new BrushTransition
            {
                Property = TemplatedControl.BackgroundProperty,
                Duration = duration,
            });
        }

        if (value.Contains("opacity", StringComparison.OrdinalIgnoreCase))
        {
            transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = duration,
            });
        }

        return transitions.Count > 0;
    }
}
