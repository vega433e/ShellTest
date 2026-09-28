using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ShellOverlay.Config;
using ShellOverlay.Logging;
using ShellOverlay.Shell;
using ShellOverlay.Views;

namespace ShellOverlay;

public partial class App : Application
{
    private ShellManager? _shell;
    private int _restored;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = AppConfig.Load();
            OverlayLog.Initialize(config.ConfigDirectory);
            OverlayLog.Info("Starting ShellOverlay");

            _shell = new ShellManager();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreShell();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreShell();
            desktop.Exit += (_, _) => RestoreShell();

            desktop.MainWindow = new OverlayWindow(config, _shell);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RestoreShell()
    {
        // Safe from any thread, at most once.
        if (Interlocked.Exchange(ref _restored, 1) != 0) return;

        try { _shell?.Dispose(); }
        catch (Exception ex) { OverlayLog.Error("Failed to restore system shell", ex); }
        _shell = null;
    }
}