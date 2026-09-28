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
    private bool _tickRunning;
    private bool _attached;
    private bool _shuttingDown;

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
            try { _shell.RestoreSystemShell(); } catch { }
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
        _modules = new CsxModuleHost(api, _config.ModulesPath);
        api.Modules = _modules;

        var loader = new XmlLayoutLoader(shortcuts, api);
        _layout = loader.Build(_config.LayoutPath);

        RootHost.Children.Clear();
        RootHost.Children.Add(_layout.Root);

        ApplyCss();

        var dipHeight = Math.Max(8, _layout.BarHeight + _layout.Margin * 1.5);
        Height = dipHeight;
        UpdateReservedHeight();

        Win32Properties.AddWndProcHookCallback(this, WndProcHook);
        _shell.MakeToolWindow(hwnd);
        _shell.ExitHotkeyPressed += OnExitHotkey;
        _shell.RegisterExitHotkey(hwnd, _config.ExitHotkey);

        _shell.HideSystemShell(_config);
        if (_config.RegisterAppBar)
            _shell.RegisterAppBar(hwnd, _layout.Edge);

        ScalingChanged += (_, _) => UpdateReservedHeight();

        await _modules.LoadDirectoryAsync();
        await _modules.InitAsync();
        StartModuleTicks();
    }

    private void UpdateReservedHeight()
    {
        var scale = RenderScaling <= 0 ? 1 : RenderScaling;
        _shell.ReservedHeight = Math.Max(8, (int)Math.Round(Height * scale));
        _shell.RefreshPosition();
    }

    private void ApplyCss()
    {
        OverlayLog.Info($"ApplyCss: path = {_config.StylesPath}");

        if (!File.Exists(_config.StylesPath))
        {
            OverlayLog.Warn($"styles.css not found at {_config.StylesPath}");
            return;
        }

        try
        {
            var css = File.ReadAllText(_config.StylesPath);
            OverlayLog.Info($"CSS loaded: {css.Length} chars");

            var rules = CssParser.Parse(css);
            OverlayLog.Info($"CSS parsed: {rules.Count} rule(s)");

            var applied = 0;
            foreach (var style in CssStyleBuilder.Build(rules))
            {
                Styles.Add(style);
                applied++;
            }
            OverlayLog.Info($"CSS applied: {applied} style(s) to window");
        }
        catch (Exception ex)
        {
            OverlayLog.Error("CSS load failed", ex);
        }
    }

    private void StartModuleTicks()
    {
        if (_layout is null || _modules is null) return;

        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += OnTick;
        _tick.Start();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_tickRunning || _shuttingDown) return;
        if (_layout is null || _modules is null) return;

        _tickRunning = true;
        try
        {
            foreach (var (control, module) in _layout.Modules)
            {
                string? text;
                try { text = await _modules.RenderAsync(module); }
                catch (Exception ex)
                {
                    OverlayLog.Error($"Render '{module}' threw", ex);
                    continue;
                }

                if (text is null) continue;

                switch (control)
                {
                    case TextBlock label:
                        label.Text = text;
                        break;

                    case Border { Child: ContentControl inner }:
                        inner.Content = text;
                        break;

                    case ContentControl content:
                        content.Content = text;
                        break;
                }
            }
        }
        finally
        {
            _tickRunning = false;
        }
    }

    private IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            if (_shell.HandleMessage(msg, unchecked((uint)wParam.ToInt64()), lParam))
                handled = true;
        }
        catch (Exception ex)
        {
            OverlayLog.Error("WndProcHook failed", ex);
        }
        return IntPtr.Zero;
    }

    private void OnExitHotkey(object? sender, EventArgs e) => ShutdownOverlay();

    private void ShowFallback(string error)
    {
        RootHost.Children.Clear();

        var exit = new Button { Content = "Exit", Classes = { "overlay" } };
        exit.Click += (_, _) => ShutdownOverlay();

        RootHost.Children.Add(new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = "ShellOverlay failed to load layout.\n" + error,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                exit,
            },
        });

        Width = 480;
        Height = 96;
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is not null)
        {
            var wa = screen.WorkingArea;
            Position = new PixelPoint(wa.X + (wa.Width - (int)Width) / 2, wa.Y + 80);
        }
    }

    private void ShutdownOverlay()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;

        try { _shell.ExitHotkeyPressed -= OnExitHotkey; } catch { }
        try { _tick?.Stop(); } catch { }
        try { _modules?.Dispose(); } catch { }
        try { _shell.Dispose(); } catch { }

        try { Close(); } catch { }

        if (Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            try { desktop.Shutdown(); } catch { }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _shuttingDown = true;
        try { _shell.ExitHotkeyPressed -= OnExitHotkey; } catch { }
        try { _tick?.Stop(); } catch { }
        try { _modules?.Dispose(); } catch { }
        try { _shell.Dispose(); } catch { }
        base.OnClosed(e);
    }
}