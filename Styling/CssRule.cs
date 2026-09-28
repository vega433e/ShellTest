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