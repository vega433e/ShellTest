using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using ShellOverlay.Logging;
using ShellOverlay.Models;
using ShellOverlay.Scripting;
using ShellOverlay.Shell;
using ShellOverlay.Styling;

namespace ShellOverlay.Layout;

public sealed class LayoutBuildResult
{
    public required Control Root { get; init; }
    public double BarHeight { get; init; }
    public double Margin { get; init; }
    public string Edge { get; init; } = "bottom";
    public List<(Control Control, string Module)> Modules { get; } = new();
}

public sealed class XmlLayoutLoader
{
    private readonly IReadOnlyList<ShortcutConfig> _shortcuts;
    private readonly ShellApi _api;

    public XmlLayoutLoader(IReadOnlyList<ShortcutConfig> shortcuts, ShellApi api)
    {
        _shortcuts = shortcuts ?? Array.Empty<ShortcutConfig>();
        _api = api;
    }

    public static IReadOnlyList<ShortcutConfig> LoadShortcuts(string path)
    {
        if (!File.Exists(path))
        {
            OverlayLog.Warn($"shortcuts.json not found at {path}");
            return Array.Empty<ShortcutConfig>();
        }

        try
        {
            using var fs = File.OpenRead(path);
            var file = JsonSerializer.Deserialize(fs, AppJsonContext.Default.ShortcutsFile);
            return file?.Shortcuts ?? new List<ShortcutConfig>();
        }
        catch (Exception ex)
        {
            OverlayLog.Error("Failed to read shortcuts.json", ex);
            return Array.Empty<ShortcutConfig>();
        }
    }

    public LayoutBuildResult Build(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException("layout.xml not found", xmlPath);

        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlPath, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException(
                $"layout.xml parse error at line {ex.LineNumber}, col {ex.LinePosition}: {ex.Message}", ex);
        }

        var shell = doc.Root ?? throw new InvalidDataException("layout.xml is empty");
        var bar = shell.Element("bar") ?? shell;

        var root = BuildBar(bar, out var modules, out var height, out var margin, out var edge);

        var result = new LayoutBuildResult
        {
            Root = root,
            BarHeight = height,
            Margin = margin,
            Edge = edge,
        };
        result.Modules.AddRange(modules);
        return result;
    }

    private Border BuildBar(XElement bar, out List<(Control, string)> modules,
        out double height, out double margin, out string edge)
    {
        modules = new List<(Control, string)>();
        height = ParseDouble(bar.Attribute("height")?.Value, 64);
        margin = ParseDouble(bar.Attribute("margin")?.Value, 10);

        edge = bar.Attribute("edge")?.Value?.Trim().ToLowerInvariant() ?? "bottom";
        if (edge != "top" && edge != "bottom")
        {
            OverlayLog.Warn($"Unknown bar edge '{edge}', defaulting to 'bottom'");
            edge = "bottom";
        }

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var onRight = false;

        foreach (var child in bar.Elements())
        {
            if (child.Name.LocalName.Equals("filler", StringComparison.OrdinalIgnoreCase))
            {
                onRight = true;
                continue;
            }

            Control? control;
            try
            {
                control = BuildNode(child, modules);
            }
            catch (Exception ex)
            {
                OverlayLog.Error($"Failed to build <{child.Name.LocalName}>: {ex.Message}", ex);
                continue;
            }

            if (control is null) continue;
            if (onRight) right.Children.Add(control);
            else left.Children.Add(control);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);

        var border = new Border
        {
            Child = grid,
            Margin = new Avalonia.Thickness(margin, margin / 2, margin, margin),
        };
        ApplyCommon(bar, border);
        if (!border.Classes.Contains("bar"))
            border.Classes.Add("bar");

        return border;
    }

    private Control? BuildNode(XElement el, List<(Control, string)> modules)
    {
        var name = el.Name.LocalName.ToLowerInvariant();
        Control? control = name switch
        {
            "button" => BuildButton(el),
            "label" or "clock" or "text" => BuildLabel(el),
            "separator" => BuildSeparator(),
            "pins" => BuildPins(),
            "panel" => BuildPanel(el, modules),
            "spacer" => new Border { Width = ParseDouble(el.Attribute("width")?.Value, 8) },
            _ => BuildWidget(el),
        };

        if (control is null) return null;
        ApplyCommon(el, control);

        var module = el.Attribute("module")?.Value;
        if (!string.IsNullOrWhiteSpace(module))
            modules.Add((control, module!));

        return control;
    }

    private Button BuildButton(XElement el)
    {
        var content = string.IsNullOrWhiteSpace(el.Value)
            ? (el.Attribute("id")?.Value ?? "•")
            : el.Value.Trim();

        var button = new Button { Content = content };
        button.Classes.Add("overlay");

        var module = el.Attribute("module")?.Value;
        var launch = el.Attribute("launch")?.Value;

        button.Click += (_, _) =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(launch))
                    _api.Launch(launch!);
                else if (!string.IsNullOrWhiteSpace(module))
                    _ = _api.ClickModuleAsync(module!);
            }
            catch (Exception ex)
            {
                OverlayLog.Error("Button click handler failed", ex);
            }
        };
        return button;
    }

    private static TextBlock BuildLabel(XElement el) =>
        new()
        {
            Text = el.Value.Trim(),
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static Border BuildSeparator()
    {
        var sep = new Border { Width = 1, Margin = new Avalonia.Thickness(6, 10) };
        sep.Classes.Add("separator");
        return sep;
    }

    private StackPanel BuildPins()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        panel.Classes.Add("pins");

        foreach (var shortcut in _shortcuts)
        {
            if (string.IsNullOrWhiteSpace(shortcut.Name) && string.IsNullOrWhiteSpace(shortcut.Path))
                continue;

            var btn = new Button();
            btn.Classes.Add("overlay");
            btn.Classes.Add("icon-btn");
            btn.Classes.Add("pin");
            ToolTip.SetTip(btn, shortcut.Name);

            var icon = IconExtractor.Extract(shortcut.Path, small: false);
            if (icon is not null)
            {
                btn.Content = new Image
                {
                    Source = icon,
                    Width = 20,
                    Height = 20,
                    Stretch = Avalonia.Media.Stretch.Uniform,
                };
            }
            else
            {
                var initial = string.IsNullOrWhiteSpace(shortcut.Name)
                    ? "?"
                    : shortcut.Name.Trim()[..1].ToUpperInvariant();
                btn.Content = initial;
            }

            var cfg = shortcut;
            btn.Click += (_, _) =>
            {
                try { _api.Launch(cfg.Path, cfg.Arguments, cfg.WorkingDirectory); }
                catch (Exception ex) { OverlayLog.Error("Pin launch failed", ex); }
            };
            panel.Children.Add(btn);
        }

        return panel;
    }

    private StackPanel BuildPanel(XElement el, List<(Control, string)> modules)
    {
        var panel = new StackPanel
        {
            Orientation = string.Equals(el.Attribute("orientation")?.Value, "vertical", StringComparison.OrdinalIgnoreCase)
                ? Orientation.Vertical
                : Orientation.Horizontal,
            Spacing = ParseDouble(el.Attribute("spacing")?.Value, 8),
        };

        foreach (var child in el.Elements())
        {
            Control? childControl;
            try { childControl = BuildNode(child, modules); }
            catch (Exception ex)
            {
                OverlayLog.Error($"Panel child <{child.Name.LocalName}> failed: {ex.Message}");
                continue;
            }
            if (childControl is not null) panel.Children.Add(childControl);
        }
        return panel;
    }

    private static Border BuildWidget(XElement el)
    {
        var inner = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (!string.IsNullOrWhiteSpace(el.Value))
            inner.Content = new TextBlock { Text = el.Value.Trim(), VerticalAlignment = VerticalAlignment.Center };

        var border = new Border { Child = inner };
        border.Classes.Add("widget");
        return border;
    }

    private static void ApplyCommon(XElement el, Control control)
    {
        var id = el.Attribute("id")?.Value;
        if (!string.IsNullOrWhiteSpace(id))
            control.Name = SanitizeIdentifier(id!);

        var classes = el.Attribute("class")?.Value;
        if (string.IsNullOrWhiteSpace(classes)) return;

        foreach (var cls in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!control.Classes.Contains(cls))
                control.Classes.Add(cls);
        }
    }

    /// <summary>
    /// Avalonia's <c>Style(x => x.Name(...))</c> requires valid C# identifiers.
    /// We sanitize so that ids like "exit-btn" don't blow up layout loading.
    /// Must match <see cref="CssParser"/> id sanitization.
    /// </summary>
    public static string SanitizeIdentifier(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;

        var sb = new StringBuilder(id.Length);
        for (var i = 0; i < id.Length; i++)
        {
            var c = id[i];
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else sb.Append('_');
        }

        if (sb.Length > 0 && char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback;
}