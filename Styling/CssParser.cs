using ShellOverlay.Layout;

namespace ShellOverlay.Styling;

/// <summary>
/// Тонкий токенизатор CSS: разбивает файл на правила (селектор + свойства).
/// Парсинг значений — в <see cref="CssValues"/>.
/// </summary>
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

            var selectors = split[0].Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var properties = ParseProperties(split[1]);
            if (properties.Count == 0) continue;

            foreach (var rawSelector in selectors)
            {
                var rule = ParseSelector(rawSelector);
                if (rule is null) continue;

                foreach (var kv in properties)
                    rule.Properties[kv.Key] = kv.Value;

                rules.Add(rule);
            }
        }

        return rules;
    }

    // ────────────────────────────────────────────────────────────
    //  Selectors
    // ────────────────────────────────────────────────────────────

    private static CssRule? ParseSelector(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0) return null;

        var hover = raw.Contains(":hover", StringComparison.OrdinalIgnoreCase)
                 || raw.Contains(":pointerover", StringComparison.OrdinalIgnoreCase)
                 || raw.Contains(":active", StringComparison.OrdinalIgnoreCase);

        var clean = StripPseudoClasses(raw);

        if (clean == "*" || clean.Length == 0)
            return new CssRule { Selector = raw, IsHover = hover };

        var typeName = "";
        var className = "";
        var id = "";

        var idIdx = clean.IndexOf('#');
        var classIdx = clean.IndexOf('.');

        if (idIdx >= 0)
        {
            var rawId = clean[(idIdx + 1)..].Trim();
            // Must match XmlLayoutLoader.SanitizeIdentifier.
            id = XmlLayoutLoader.SanitizeIdentifier(rawId);
            if (idIdx > 0) typeName = clean[..idIdx].Trim();
        }
        else if (classIdx >= 0)
        {
            className = clean[(classIdx + 1)..].Trim();
            if (classIdx > 0) typeName = clean[..classIdx].Trim();
        }
        else
        {
            typeName = clean.Trim();
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

    private static string StripPseudoClasses(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == ':')
            {
                while (i < s.Length && s[i] != ' ' && s[i] != '.' && s[i] != '#')
                    i++;
                i--;
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────
    //  Properties
    // ────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseProperties(string body)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in body.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split(':', 2);
            if (kv.Length != 2) continue;

            var key = kv[0].Trim();
            var value = kv[1].Trim();
            if (key.Length == 0 || value.Length == 0) continue;

            map[key] = value;
        }

        return map;
    }

    // ────────────────────────────────────────────────────────────
    //  Comments
    // ────────────────────────────────────────────────────────────

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
}