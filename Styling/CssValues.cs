using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace ShellOverlay.Styling;

/// <summary>Безопасный парсер CSS-значений в типы Avalonia.</summary>
internal static class CssValues
{
    // ════════════════════════════════════════════════════════════
    //  Colors
    // ════════════════════════════════════════════════════════════

    public static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();

        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        { color = Colors.Transparent; return true; }

        if (value[0] == '#') return TryParseHex(value, out color);
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return TryParseRgb(value, out color);
        if (value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) return TryParseHsl(value, out color);

        // Named colours via Avalonia (red, blue, white, ...).
        try { color = Color.Parse(value); return true; }
        catch { return false; }
    }

    private static bool TryParseHex(string value, out Color color)
    {
        color = default;
        var h = value[1..];

        try
        {
            switch (h.Length)
            {
                case 3:
                    color = Color.FromRgb(
                        (byte)(Convert.ToByte(h[0].ToString(), 16) * 17),
                        (byte)(Convert.ToByte(h[1].ToString(), 16) * 17),
                        (byte)(Convert.ToByte(h[2].ToString(), 16) * 17));
                    return true;
                case 4:
                    color = Color.FromArgb(
                        (byte)(Convert.ToByte(h[3].ToString(), 16) * 17),
                        (byte)(Convert.ToByte(h[0].ToString(), 16) * 17),
                        (byte)(Convert.ToByte(h[1].ToString(), 16) * 17),
                        (byte)(Convert.ToByte(h[2].ToString(), 16) * 17));
                    return true;
                case 6:
                    color = Color.FromRgb(
                        Convert.ToByte(h[..2], 16),
                        Convert.ToByte(h.Substring(2, 2), 16),
                        Convert.ToByte(h.Substring(4, 2), 16));
                    return true;
                case 8:
                    // CSS-order: #RRGGBBAA → Avalonia: FromArgb(A,R,G,B)
                    color = Color.FromArgb(
                        Convert.ToByte(h.Substring(6, 2), 16),
                        Convert.ToByte(h[..2], 16),
                        Convert.ToByte(h.Substring(2, 2), 16),
                        Convert.ToByte(h.Substring(4, 2), 16));
                    return true;
            }
        }
        catch { /* malformed hex */ }

        return false;
    }

    private static bool TryParseRgb(string value, out Color color)
    {
        color = default;
        if (!TryUnwrapFunction(value, out var args)) return false;
        var parts = args.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return false;

        if (!TryChannel(parts[0], out var r)) return false;
        if (!TryChannel(parts[1], out var g)) return false;
        if (!TryChannel(parts[2], out var b)) return false;

        byte a = 255;
        if (parts.Length >= 4 && !TryAlpha(parts[3], out a)) return false;

        color = Color.FromArgb(a, (byte)r, (byte)g, (byte)b);
        return true;
    }

    private static bool TryParseHsl(string value, out Color color)
    {
        color = default;
        if (!TryUnwrapFunction(value, out var args)) return false;
        var parts = args.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return false;

        if (!double.TryParse(parts[0].TrimEnd('d', 'e', 'g'), NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) return false;
        if (!double.TryParse(parts[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return false;
        if (!double.TryParse(parts[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) return false;

        byte a = 255;
        if (parts.Length >= 4 && !TryAlpha(parts[3], out a)) return false;

        color = HslToRgb(h, s / 100.0, l / 100.0, a);
        return true;
    }

    private static bool TryUnwrapFunction(string value, out string args)
    {
        args = "";
        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close <= open) return false;
        args = value[(open + 1)..close].Trim();
        return true;
    }

    private static bool TryChannel(string s, out int value)
    {
        value = 0;
        s = s.Trim();
        double n;
        if (s.EndsWith('%'))
        {
            if (!double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) return false;
            n = pct / 100.0 * 255.0;
        }
        else if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return false;

        if (double.IsNaN(n) || double.IsInfinity(n)) return false;
        value = Math.Clamp((int)Math.Round(n), 0, 255);
        return true;
    }

    private static bool TryAlpha(string s, out byte alpha)
    {
        alpha = 255;
        s = s.Trim();
        double n;
        if (s.EndsWith('%'))
        {
            if (!double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) return false;
            n = pct / 100.0;
        }
        else if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return false;

        if (double.IsNaN(n) || double.IsInfinity(n)) return false;
        alpha = (byte)Math.Clamp((int)Math.Round(n * 255.0), 0, 255);
        return true;
    }

    private static Color HslToRgb(double h, double s, double l, byte a)
    {
        h = ((h % 360) + 360) % 360 / 360.0;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        double r, g, b;
        if (s < 1e-6) { r = g = b = l; }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3.0);
            g = Hue(p, q, h);
            b = Hue(p, q, h - 1.0 / 3.0);
        }
        return Color.FromArgb(a, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    // ════════════════════════════════════════════════════════════
    //  Brush (solid или градиент)
    // ════════════════════════════════════════════════════════════

    public static bool TryParseBrush(string value, out IBrush brush)
    {
        brush = null!;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();

        if (value.StartsWith("linear-gradient", StringComparison.OrdinalIgnoreCase))
            return TryParseLinearGradient(value, out brush);

        if (TryParseColor(value, out var c))
        {
            brush = new SolidColorBrush(c);
            return true;
        }
        return false;
    }

    private static bool TryParseLinearGradient(string value, out IBrush brush)
    {
        brush = null!;
        if (!TryUnwrapFunction(value, out var inner)) return false;

        var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        var angle = 180.0;
        var startIdx = 0;

        if (parts[0].EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        {
            if (!double.TryParse(parts[0][..^3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out angle)) return false;
            startIdx = 1;
        }
        else if (parts[0].StartsWith("to ", StringComparison.OrdinalIgnoreCase))
        {
            angle = parts[0].ToLowerInvariant() switch
            {
                "to top" => 0,
                "to right" => 90,
                "to bottom" => 180,
                "to left" => 270,
                _ => 180
            };
            startIdx = 1;
        }

        var stops = new GradientStops();
        var stopCount = parts.Length - startIdx;
        if (stopCount < 2) return false;

        for (var i = 0; i < stopCount; i++)
        {
            var token = parts[startIdx + i];
            var sub = token.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (sub.Length == 0) return false;

            if (!TryParseColor(sub[0], out var c)) return false;

            double offset = (double)i / (stopCount - 1);
            if (sub.Length >= 2)
            {
                var off = sub[1].TrimEnd('%');
                if (double.TryParse(off, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
                    offset = pct / 100.0;
            }
            stops.Add(new GradientStop(c, Math.Clamp(offset, 0, 1)));
        }

        var rad = (angle - 90) * Math.PI / 180.0;
        var dx = Math.Cos(rad) * 0.5;
        var dy = Math.Sin(rad) * 0.5;

        brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative),
            GradientStops = stops,
        };
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Lengths / Spacing / Time
    // ════════════════════════════════════════════════════════════

    public static bool TryParseLength(string value, out double length)
    {
        length = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var cleaned = value.Trim()
            .Replace("px", "", StringComparison.OrdinalIgnoreCase)
            .Replace("dip", "", StringComparison.OrdinalIgnoreCase)
            .Replace("em", "", StringComparison.OrdinalIgnoreCase);

        if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out length)) return false;
        if (double.IsNaN(length) || double.IsInfinity(length)) return false;
        return true;
    }

    public static bool TryParseThickness(string value, out Thickness thickness)
    {
        thickness = default;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => TryParseLength(s, out var d) ? (double?)d : null)
                         .Where(d => d.HasValue)
                         .Select(d => d!.Value)
                         .ToArray();

        thickness = parts.Length switch
        {
            1 => new Thickness(parts[0]),
            2 => new Thickness(parts[1], parts[0], parts[1], parts[0]),
            4 => new Thickness(parts[3], parts[0], parts[1], parts[2]),
            _ => default,
        };
        return parts.Length is 1 or 2 or 4;
    }

    public static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.FromMilliseconds(150);
        value = value.Trim();

        if (value.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
            && ms >= 0)
        { time = TimeSpan.FromMilliseconds(ms); return true; }

        if (value.EndsWith('s')
            && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)
            && sec >= 0)
        { time = TimeSpan.FromSeconds(sec); return true; }

        return false;
    }

    // ════════════════════════════════════════════════════════════
    //  Enums — только валидные значения, никаких (Type)int
    // ════════════════════════════════════════════════════════════

    public static bool TryParseFontWeight(string value, out FontWeight weight)
    {
        weight = FontWeight.Normal;
        switch (value.Trim().ToLowerInvariant())
        {
            case "thin" or "100": weight = FontWeight.Thin; return true;
            case "extralight" or "extra-light" or "ultralight" or "200": weight = FontWeight.ExtraLight; return true;
            case "light" or "300": weight = FontWeight.Light; return true;
            case "normal" or "regular" or "400": weight = FontWeight.Normal; return true;
            case "medium" or "500": weight = FontWeight.Medium; return true;
            case "semibold" or "semi-bold" or "demibold" or "600": weight = FontWeight.SemiBold; return true;
            case "bold" or "700": weight = FontWeight.Bold; return true;
            case "extrabold" or "extra-bold" or "ultrabold" or "800": weight = FontWeight.ExtraBold; return true;
            case "black" or "heavy" or "900": weight = FontWeight.Black; return true;
            default: return false;
        }
    }

    public static bool TryParseFontStyle(string value, out FontStyle style)
    {
        style = FontStyle.Normal;
        switch (value.Trim().ToLowerInvariant())
        {
            case "normal": style = FontStyle.Normal; return true;
            case "italic": style = FontStyle.Italic; return true;
            case "oblique": style = FontStyle.Oblique; return true;
            default: return false;
        }
    }

    public static bool TryParseTextAlignment(string value, out TextAlignment align)
    {
        align = TextAlignment.Left;
        switch (value.Trim().ToLowerInvariant())
        {
            case "left" or "start": align = TextAlignment.Left; return true;
            case "center": align = TextAlignment.Center; return true;
            case "right" or "end": align = TextAlignment.Right; return true;
            case "justify": align = TextAlignment.Justify; return true;
            default: return false;
        }
    }

    public static bool TryParseHorizontalAlignment(string value, out HorizontalAlignment align)
    {
        align = HorizontalAlignment.Left;
        switch (value.Trim().ToLowerInvariant())
        {
            case "left" or "start": align = HorizontalAlignment.Left; return true;
            case "center": align = HorizontalAlignment.Center; return true;
            case "right" or "end": align = HorizontalAlignment.Right; return true;
            case "stretch": align = HorizontalAlignment.Stretch; return true;
            default: return false;
        }
    }

    public static bool TryParseCursor(string value, out Cursor cursor)
    {
        cursor = null!;
        var type = value.Trim().ToLowerInvariant() switch
        {
            "pointer" or "hand" => StandardCursorType.Hand,
            "text" or "ibeam" => StandardCursorType.Ibeam,
            "wait" or "busy" => StandardCursorType.Wait,
            "cross" or "crosshair" => StandardCursorType.Cross,
            "help" => StandardCursorType.Help,
            "none" => StandardCursorType.None,
            "default" or "auto" => StandardCursorType.Arrow,
            _ => (StandardCursorType?)null,
        };
        if (type is null) return false;
        cursor = new Cursor(type.Value);
        return true;
    }

    // ════════════════════════════════════════════════════════════
    //  Complex values
    // ════════════════════════════════════════════════════════════

    public static bool TryParseTransform(string value, out ITransform transform)
    {
        transform = TransformOperations.Identity;
        try
        {
            transform = TransformOperations.Parse(value);
            return true;
        }
        catch { return false; }
    }

    public static bool TryParseBoxShadows(string value, out BoxShadows shadows)
    {
        shadows = default;
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<BoxShadow>();

        foreach (var part in parts)
        {
            if (!TryParseOneShadow(part, out var s)) return false;
            list.Add(s);
        }
        if (list.Count == 0) return false;

        shadows = new BoxShadows(list[0], list.Skip(1).ToArray());
        return true;
    }

    private static bool TryParseOneShadow(string value, out BoxShadow shadow)
    {
        shadow = default;
        string? colorToken = null;
        var nums = new List<double>();

        foreach (var t in TokenizeTopLevel(value))
        {
            if (t.StartsWith('#') || t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) || t.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
            { colorToken = t; continue; }

            var cleaned = t.Replace("px", "").Trim();
            if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                nums.Add(d);
        }

        if (nums.Count < 2) return false;
        // OffsetX, OffsetY, Blur?, Spread?
        var color = Colors.Black;
        if (colorToken is not null && TryParseColor(colorToken, out var c)) color = c;

        shadow = new BoxShadow
        {
            OffsetX = nums[0],
            OffsetY = nums[1],
            Blur = nums.Count >= 3 ? nums[2] : 0,
            Spread = nums.Count >= 4 ? nums[3] : 0,
            Color = color,
        };
        return true;
    }

    private static IEnumerable<string> TokenizeTopLevel(string value)
    {
        var sb = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var c in value)
        {
            if (c == '(') depth++;
            else if (c == ')') depth--;

            if (depth == 0 && (c == ' ' || c == '\t'))
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    public static bool TryParseTransitions(string value, out Transitions transitions)
    {
        transitions = new Transitions();

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts)
        {
            var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            var property = tokens[0].ToLowerInvariant();
            var duration = TimeSpan.FromMilliseconds(150);
            for (var i = 1; i < tokens.Length; i++)
                if (TryParseTime(tokens[i], out var t)) duration = t;

            switch (property)
            {
                case "transform":
                    transitions.Add(new Avalonia.Animation.TransformOperationsTransition
                    { Property = Visual.RenderTransformProperty, Duration = duration });
                    break;
                case "background" or "background-color":
                    transitions.Add(new Avalonia.Animation.BrushTransition
                    { Property = Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, Duration = duration });
                    break;
                case "color" or "foreground":
                    transitions.Add(new Avalonia.Animation.BrushTransition
                    { Property = Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, Duration = duration });
                    break;
                case "opacity":
                    transitions.Add(new Avalonia.Animation.DoubleTransition
                    { Property = Visual.OpacityProperty, Duration = duration });
                    break;
            }
        }
        return transitions.Count > 0;
    }
}