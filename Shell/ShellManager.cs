using System.Runtime.InteropServices;
using ShellOverlay.Config;
using ShellOverlay.Logging;

namespace ShellOverlay.Shell;

/// <summary>
/// Hides the Windows Explorer taskbar chrome and registers the overlay as an AppBar.
/// Dispose always restores the system shell.
/// </summary>
public sealed class ShellManager : IDisposable
{
    public const uint AppBarCallbackMessage = 0x8000 + 1;
    private const int ExitHotkeyId = 1;
    private const uint ABN_POSCHANGED = 1;

    private readonly List<IntPtr> _hidden = [];
    private IntPtr _appBarHwnd;
    private IntPtr _hotkeyHwnd;
    private bool _appBarRegistered;
    private bool _hotkeyRegistered;
    private bool _shellHidden;
    private bool _disposed;
    private uint _appBarEdge = NativeMethods.ABE_BOTTOM;

    public int ReservedHeight { get; set; } = 76;
    public event EventHandler? PositionChanged;
    public event EventHandler? ExitHotkeyPressed;

    public void HideSystemShell(AppConfig config)
    {
        if (_shellHidden) return;

        try
        {
            if (config.HideTaskbar)
                HideClass("Shell_TrayWnd", enumerate: false);

            if (config.HideSecondaryTaskbars)
                HideClass("Shell_SecondaryTrayWnd", enumerate: true);

            if (config.HideNotifyOverflow)
            {
                HideClass("NotifyIconOverflowWindow", enumerate: false);
                HideClass("TopLevelWindowForOverflowXamlIsland", enumerate: false);
            }

            _shellHidden = true;
            OverlayLog.Info($"System shell hidden ({_hidden.Count} windows)");
        }
        catch (Exception ex)
        {
            OverlayLog.Error("HideSystemShell failed", ex);
        }
    }

    public void RestoreSystemShell()
    {
        if (!_shellHidden && _hidden.Count == 0) return;

        foreach (var h in _hidden)
        {
            if (h != IntPtr.Zero)
                NativeMethods.ShowWindow(h, NativeMethods.SW_SHOW);
        }

        _hidden.Clear();
        _shellHidden = false;
        OverlayLog.Info("System shell restored");
    }

    public void RegisterAppBar(IntPtr hwnd, string edge)
    {
        if (_appBarRegistered || hwnd == IntPtr.Zero) return;

        _appBarHwnd = hwnd;
        _appBarEdge = edge.Equals("top", StringComparison.OrdinalIgnoreCase)
            ? NativeMethods.ABE_TOP
            : NativeMethods.ABE_BOTTOM;

        var abd = new NativeMethods.APPBARDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = AppBarCallbackMessage,
            uEdge = _appBarEdge,
        };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref abd);
        _appBarRegistered = true;
        ApplyPosition();
    }

    public void UnregisterAppBar()
    {
        if (!_appBarRegistered) return;

        var abd = new NativeMethods.APPBARDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = _appBarHwnd,
        };
        NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd);
        _appBarRegistered = false;
        _appBarHwnd = IntPtr.Zero;
    }

    public void MakeToolWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW;
        style &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    public void RegisterExitHotkey(IntPtr hwnd, string spec)
    {
        if (_hotkeyRegistered || hwnd == IntPtr.Zero) return;
        if (!TryParseHotkey(spec, out var mods, out var vk)) return;

        _hotkeyHwnd = hwnd;
        _hotkeyRegistered = NativeMethods.RegisterHotKey(hwnd, ExitHotkeyId, mods, vk);
        if (!_hotkeyRegistered)
            OverlayLog.Warn($"Could not register hotkey '{spec}'");
    }

    public void UnregisterExitHotkey()
    {
        if (!_hotkeyRegistered) return;
        NativeMethods.UnregisterHotKey(_hotkeyHwnd, ExitHotkeyId);
        _hotkeyRegistered = false;
        _hotkeyHwnd = IntPtr.Zero;
    }

    public bool HandleMessage(uint msg, uint wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam == ExitHotkeyId)
        {
            ExitHotkeyPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (msg != AppBarCallbackMessage) return false;

        if (wParam == ABN_POSCHANGED)
        {
            ApplyPosition();
            PositionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    private void ApplyPosition()
    {
        if (!_appBarRegistered || _appBarHwnd == IntPtr.Zero) return;
        if (ReservedHeight <= 0) ReservedHeight = 76;

        var monitor = NativeMethods.MonitorFromWindow(_appBarHwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref mi)) return;

        NativeMethods.RECT rc;
        if (_appBarEdge == NativeMethods.ABE_TOP)
        {
            rc = new NativeMethods.RECT
            {
                left = mi.rcMonitor.left,
                top = mi.rcMonitor.top,
                right = mi.rcMonitor.right,
                bottom = mi.rcMonitor.top + ReservedHeight,
            };
        }
        else
        {
            rc = new NativeMethods.RECT
            {
                left = mi.rcMonitor.left,
                top = mi.rcMonitor.bottom - ReservedHeight,
                right = mi.rcMonitor.right,
                bottom = mi.rcMonitor.bottom,
            };
        }

        var abd = new NativeMethods.APPBARDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            hWnd = _appBarHwnd,
            uEdge = _appBarEdge,
            rc = rc,
        };

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref abd);
        if (_appBarEdge == NativeMethods.ABE_TOP)
            abd.rc.bottom = abd.rc.top + ReservedHeight;
        else
            abd.rc.top = abd.rc.bottom - ReservedHeight;

        NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref abd);

        NativeMethods.SetWindowPos(
            _appBarHwnd, NativeMethods.HWND_TOPMOST,
            abd.rc.left, abd.rc.top,
            abd.rc.right - abd.rc.left,
            Math.Max(abd.rc.bottom - abd.rc.top, ReservedHeight),
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void HideClass(string className, bool enumerate)
    {
        if (!enumerate)
        {
            var hwnd = NativeMethods.FindWindow(className, null);
            Hide(hwnd);
            return;
        }

        IntPtr hwndEx = IntPtr.Zero;
        while ((hwndEx = NativeMethods.FindWindowEx(IntPtr.Zero, hwndEx, className, null)) != IntPtr.Zero)
            Hide(hwndEx);
    }

    private void Hide(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        _hidden.Add(hwnd);
    }

    public static void SendWinKey()
    {
        NativeMethods.keybd_event(NativeMethods.VK_LWIN, 0, 0, 0);
        NativeMethods.keybd_event(NativeMethods.VK_LWIN, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
    }

    public static bool TryParseHotkey(string spec, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        var parts = spec.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt" => NativeMethods.MOD_ALT,
                "shift" => NativeMethods.MOD_SHIFT,
                "win" or "windows" => NativeMethods.MOD_WIN,
                _ => 0u,
            };
        }

        var key = parts[^1];
        if (key.Length == 1)
        {
            vk = char.ToUpperInvariant(key[0]);
            return vk is >= 'A' and <= 'Z' or >= '0' and <= '9';
        }

        if (key.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + fn - 1);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { UnregisterExitHotkey(); } catch { }
        try { UnregisterAppBar(); } catch { }
        try { RestoreSystemShell(); } catch { }
    }
}
