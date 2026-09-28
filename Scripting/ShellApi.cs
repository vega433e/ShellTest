using System.Diagnostics;
using ShellOverlay.Logging;
using ShellOverlay.Models;
using ShellOverlay.Shell;

namespace ShellOverlay.Scripting;

public sealed class ShellApi
{
    private readonly IReadOnlyList<ShortcutConfig> _shortcuts;
    private readonly Action _exit;

    public ShellApi(IReadOnlyList<ShortcutConfig> shortcuts, Action exit)
    {
        _shortcuts = shortcuts ?? Array.Empty<ShortcutConfig>();
        _exit = exit ?? (() => { });
    }

    internal CsxModuleHost? Modules { get; set; }

    public IReadOnlyList<ShortcutConfig> Shortcuts => _shortcuts;

    public void Log(string message) => OverlayLog.Info("[csx] " + message);

    public void Exit() => _exit();

    public void SendWinKey() => ShellManager.SendWinKey();

    public void Launch(string path, string? arguments = null, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            OverlayLog.Warn("Launch called with empty path");
            return;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            var psi = new ProcessStartInfo
            {
                FileName = expanded,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
                psi.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDirectory);

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            OverlayLog.Error($"Launch failed: {path}", ex);
        }
    }

    public Task ClickModuleAsync(string module) =>
        Modules?.ClickAsync(module) ?? Task.CompletedTask;
}