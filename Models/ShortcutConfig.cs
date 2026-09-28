namespace ShellOverlay.Models;

public sealed class ShortcutsFile
{
    public List<ShortcutConfig> Shortcuts { get; set; } = [];
}

public sealed class ShortcutConfig
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
}
