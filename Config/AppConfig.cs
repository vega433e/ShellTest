using System.Text.Json;
using System.Text.Json.Serialization;
using ShellOverlay.Models;   

namespace ShellOverlay.Config;

public sealed class AppConfig
{
    public bool HideTaskbar { get; set; } = true;
    public bool HideSecondaryTaskbars { get; set; } = true;
    public bool HideNotifyOverflow { get; set; } = true;
    public bool RegisterAppBar { get; set; } = true;
    public string ExitHotkey { get; set; } = "Ctrl+Alt+Q";
    public string LayoutFile { get; set; } = "layout.xml";
    public string StylesFile { get; set; } = "styles.css";
    public string ShortcutsFile { get; set; } = "shortcuts.json";
    public string ModulesDirectory { get; set; } = "modules";

    [JsonIgnore]
    public string ConfigDirectory { get; private set; } = "";

    public string LayoutPath => Path.Combine(ConfigDirectory, LayoutFile);
    public string StylesPath => Path.Combine(ConfigDirectory, StylesFile);
    public string ShortcutsPath => Path.Combine(ConfigDirectory, ShortcutsFile);
    public string ModulesPath => Path.Combine(ConfigDirectory, ModulesDirectory);

    public static AppConfig Load()
    {
        var dir = ResolveConfigDirectory();
        var path = Path.Combine(dir, "app.json");
        AppConfig config;

        if (File.Exists(path))
        {
            try
            {
                using var fs = File.OpenRead(path);
                config = JsonSerializer.Deserialize(fs, AppJsonContext.Default.AppConfig) ?? new AppConfig();
            }
            catch
            {
                config = new AppConfig();
            }
        }
        else
        {
            config = new AppConfig();
        }

        config.ConfigDirectory = dir;
        return config;
    }

    private static string ResolveConfigDirectory()
    {
        var nextToExe = Path.Combine(AppContext.BaseDirectory, "config");
        if (Directory.Exists(nextToExe)) return nextToExe;

        var probe = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(probe, "config");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(probe);
            if (parent is null) break;
            probe = parent.FullName;
        }

        Directory.CreateDirectory(nextToExe);
        return nextToExe;
    }
}
