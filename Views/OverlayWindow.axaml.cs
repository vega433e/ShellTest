using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Win32;
using ShellOverlay.Config;
using ShellOverlay.Layout;
using ShellOverlay.Logging;
using ShellOverlay.Scripting;
using ShellOverlay.Shell;
using ShellOverlay.Styling;

namespace ShellOverlay.Views;

public partial class OverlayWindow : Window
{
    private readonly AppConfig _config;
    private readonly ShellManager _shell;
    private CsxModuleHost? _modules;
    private LayoutBuildResult? _layout;
    private DispatcherTimer? _tick;
    private bool _attached;

    public OverlayWindow(AppConfig config, ShellManager shell)
    {
        _config = config;
        _shell = shell;
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_attached) return;
        _attached = true;

        try
        {
            await BootstrapAsync();
        }
        catch (Exception ex)
        {
            OverlayLog.Error("Overlay bootstrap failed", ex);
            _shell.RestoreSystemShell();
            ShowFallback(ex.Message);
        }
    }

    private async Task BootstrapAsync()
    {
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Win32 window handle is not ready");

        var shortcuts = XmlLayoutLoader.LoadShortcuts(_config.ShortcutsPath);
        var api = new ShellApi(shortcuts, ShutdownOverlay);
        _modules = new CsxModuleHost(api);
        api.Modules = _modules;

        var loader = new XmlLayoutLoader(shortcuts, api);
        _layout = loader.Build(_config.LayoutPath);
        RootHost.Children.Clear();
        RootHost.Children.Add(_layout.Root);

        ApplyCss();

        var dipHeight = Math.Max(8, _layout.BarHeight + _layout.Margin * 1.5);
        Height = dipHeight;
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        _shell.ReservedHeight = Math.Max(8, (int)Math.Round(dipHeight * scale));

        Win32Properties.AddWndProcHookCallback(this, WndProcHook);
        _shell.MakeToolWindow(hwnd);
        _shell.ExitHotkeyPressed += (_, _) => ShutdownOverlay();
        _shell.RegisterExitHotkey(hwnd, _config.ExitHotkey);

        _shell.HideSystemShell(_config);
        if (_config.RegisterAppBar)
            _shell.RegisterAppBar(hwnd, _layout.Edge);

        await _modules.LoadDirectoryAsync(_config.ModulesPath);
        await _modules.InitAsync();
        StartModuleTicks();
    }

    private void ApplyCss()
    {
        if (!File.Exists(_config.StylesPath))
        {
            OverlayLog.Warn("styles.css not found");
            return;
        }

        var css = File.ReadAllText(_config.StylesPath);
        var rules = CssParser.Parse(css);
        foreach (var style in CssStyleBuilder.Build(rules))
            Styles.Add(style);
    }

    private void StartModuleTicks()
    {
        if (_layout is null || _modules is null) return;

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += async (_, _) =>
        {
            foreach (var (control, module) in _layout.Modules)
            {
                var text = await _modules.RenderAsync(module);
                if (text is null) continue;
                switch (control)
                {
                    case TextBlock label:
                        label.Text = text;
                        break;
                    case ContentControl content:
                        content.Content = text;
                        break;
                    case Border { Child: ContentControl inner }:
                        inner.Content = text;
                        break;
                    case Button button:
                        button.Content = text;
                        break;
                }
            }
        };
        _tick.Start();
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_shell.HandleMessage(msg, unchecked((uint)wParam.ToInt64()), lParam))
            handled = true;
        return IntPtr.Zero;
    }

    private void ShowFallback(string error)
    {
        RootHost.Children.Clear();
        var exit = new Button { Content = "Exit", Classes = { "overlay" } };
        exit.Click += (_, _) => ShutdownOverlay();
        RootHost.Children.Add(new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Children =
            {
                new TextBlock { Text = "ShellOverlay failed to load layout. " + error },
                exit,
            },
        });
        Height = 72;
        Width = 480;
    }

    private void ShutdownOverlay()
    {
        _tick?.Stop();
        _modules?.Dispose();
        _shell.Dispose();
        Close();
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tick?.Stop();
        _modules?.Dispose();
        _shell.Dispose();
        base.OnClosed(e);
    }
}
