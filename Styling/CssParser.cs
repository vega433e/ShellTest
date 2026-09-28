using System.Globalization;
using Avalonia;
using Avalonia.Media;
using ShellOverlay.Layout;

namespace ShellOverlay.Styling;

public sealed class CssRule
{
    public string Selector { get; init; } = "";
    public string TypeName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string Id { get; init; } = "";
    public bool IsHover { get; init; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class CssParser
{
    public static List<CssRule> Parse(string css)
    {
        var rules = new List<CssRule>();
        if (string.IsNullOrWhiteSpace(css)) return rules;

        css = StripComments(css);
        var chunks = css.Split('}', StringSplitOptions.RemoveEmptyEntries);

        foreach (var chunk in chunks)
        {
            var split = chunk.Split('{', 2);
            if (split.Length != 2) continue;

            var selectors = split[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var body = split[1];
            var properties = ParseProperties(body);

            foreach (var rawSelector in selectors)
            {
                var rule = ParseSelector(rawSelector);
                foreach (var kv in properties)
                    rule.Properties[kv.Key] = kv.Value;
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static CssRule ParseSelector(string raw)
    {
        var hover = raw.Contains(":hover", StringComparison.OrdinalIgnoreCase)
                    || raw.Contains(":pointerover", StringComparison.OrdinalIgnoreCase);
        raw = raw.Replace(":hover", "", StringComparison.OrdinalIgnoreCase)
                 .Replace(":pointerover", "", StringComparison.OrdinalIgnoreCase)
                 .Trim();

        var typeName = "";
        var className = "";
        var id = "";

        if (raw == "*")
        {
            return new CssRule { Selector = raw, IsHover = hover };
        }

        var classIdx = raw.IndexOf('.');
        var idIdx = raw.IndexOf('#');

        if (idIdx >= 0)
        {
            var rawId = raw[(idIdx + 1)..].Trim();
            // Must match XmlLayoutLoader.SanitizeIdentifier so selectors line up with controls.
            id = XmlLayoutLoader.SanitizeIdentifier(rawId);
            if (idIdx > 0) typeName = raw[..idIdx].Trim();
        }

        return new CssRule
        {
            Selector = raw,
            TypeName = typeName,
            ClassName = className,
            Id = id,
            IsHover = hover,
        };
    }

    private static Dictionary<string, string> ParseProperties(string body)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', 2);
            if (kv.Length != 2) continue;
            map[kv[0].Trim()] = kv[1].Trim();
        }
        return map;
    }

    private static string StripComments(string css)
    {
        while (true)
        {
            var start = css.IndexOf("/*", StringComparison.Ordinal);
            if (start < 0) break;
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                css = css[..start];
                break;
            }
            css = css.Remove(start, end + 2 - start);
        }
        return css;
    }

    public static bool TryParseColor(string value, out Color color)
    {
        color = default;
        value = value.Trim();
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Colors.Transparent;
            return true;
        }

        try
        {
            color = Color.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseThickness(string value, out Thickness thickness)
    {
        thickness = default;
        var parts = SplitUnits(value);
        try
        {
            thickness = parts.Length switch
            {
                1 => new Thickness(parts[0]),
                2 => new Thickness(parts[1], parts[0], parts[1], parts[0]),
                4 => new Thickness(parts[3], parts[0], parts[1], parts[2]),
                _ => default,
            };
            return parts.Length is 1 or 2 or 4;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseDouble(string value, out double number)
    {
        var cleaned = value.Replace("px", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("dip", "", StringComparison.OrdinalIgnoreCase)
                           .Trim();
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    public static bool TryParseTime(string value, out TimeSpan time)
    {
        time = TimeSpan.FromMilliseconds(150);
        var cleaned = value.Trim();
        if (cleaned.EndsWith("ms", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(cleaned[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
        {
            time = TimeSpan.FromMilliseconds(ms);
            return true;
        }
        if (cleaned.EndsWith('s')
            && double.TryParse(cleaned[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec))
        {
            time = TimeSpan.FromSeconds(sec);
            return true;
        }
        return false;
    }

    private static double[] SplitUnits(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var list = new List<double>(tokens.Length);
        foreach (var token in tokens)
        {
            if (TryParseDouble(token, out var n))
                list.Add(n);
        }
        return [.. list];
    }
}
