using System.Runtime.InteropServices;
using ShellOverlay.Config;
using ShellOverlay.Logging;

namespace ShellOverlay.Shell;

/// <summary>
/// Hides the Explorer taskbar chrome and registers the overlay as an AppBar.
/// All public members are thread-safe. Dispose always restores the shell.
/// </summary>
public sealed class ShellManager : IDisposable
{
    public const uint AppBarCallbackMessage = 0x8000 + 1;
    private const int ExitHotkeyId = 1;
    private const uint ABN_POSCHANGED = 1;
    private const int OffScreenY = -10000;

    private sealed class HiddenWindow
    {
        public IntPtr Hwnd;
        public NativeMethods.RECT OriginalRect;
    }

    private readonly object _gate = new();
    private readonly List<HiddenWindow> _hidden = new();
    private IntPtr _appBarHwnd;
    private IntPtr _hotkeyHwnd;
    private bool _appBarRegistered;
    private bool _hotkeyRegistered;
    private bool _shellHidden;
    private int _disposed;
    private uint _appBarEdge = NativeMethods.ABE_BOTTOM;

    public int ReservedHeight { get; set; } = 76;

    public event EventHandler? PositionChanged;
    public event EventHandler? ExitHotkeyPressed;

    // ---------------------------------------------------------------- shell

    public void HideSystemShell(AppConfig config)
    {
        lock (_gate)
        {
            if (_shellHidden || _disposed != 0) return;

            try
            {
                if (config.HideTaskbar)
                    HideClassLocked("Shell_TrayWnd", enumerate: false);

                if (config.HideSecondaryTaskbars)
                    HideClassLocked("Shell_SecondaryTrayWnd", enumerate: true);

                if (config.HideNotifyOverflow)
                {
                    HideClassLocked("NotifyIconOverflowWindow", enumerate: false);
                    HideClassLocked("TopLevelWindowForOverflowXamlIsland", enumerate: false);
                }

                _shellHidden = true;
                OverlayLog.Info($"System shell hidden ({_hidden.Count} window(s))");
            }
            catch (Exception ex)
            {
                OverlayLog.Error("HideSystemShell failed", ex);
            }
        }
    }

    public void RestoreSystemShell()
    {
        lock (_gate)
        {
            if (!_shellHidden && _hidden.Count == 0) return;

            foreach (var entry in _hidden)
            {
                if (entry.Hwnd == IntPtr.Zero) continue;
                try
                {
                    var r = entry.OriginalRect;

                    // Put the window back where we found it.
                    NativeMethods.SetWindowPos(
                        entry.Hwnd, IntPtr.Zero,
                        r.left, r.top,
                        r.right - r.left, r.bottom - r.top,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

                    NativeMethods.ShowWindow(entry.Hwnd, NativeMethods.SW_SHOW);
                }
                catch (Exception ex)
                {
                    OverlayLog.Warn($"Restore window 0x{entry.Hwnd:X} failed: {ex.Message}");
                }
            }

            _hidden.Clear();
            _shellHidden = false;
            OverlayLog.Info("System shell restored");
        }
    }

    private void HideClassLocked(string className, bool enumerate)
    {
        if (!enumerate)
        {
            HideLocked(NativeMethods.FindWindow(className, null));
            return;
        }

        IntPtr cursor = IntPtr.Zero;
        while ((cursor = NativeMethods.FindWindowEx(IntPtr.Zero, cursor, className, null)) != IntPtr.Zero)
            HideLocked(cursor);
    }

    private void HideLocked(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            // Remember where it was so we can put it back on restore.
            if (!NativeMethods.GetWindowRect(hwnd, out var rect))
                rect = default;

            // Move off-screen instead of SW_HIDE: Windows re-shows a hidden
            // taskbar as soon as the cursor touches the screen edge.
            NativeMethods.SetWindowPos(
                hwnd, IntPtr.Zero,
                0, OffScreenY, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);

            _hidden.Add(new HiddenWindow { Hwnd = hwnd, OriginalRect = rect });
        }
        catch (Exception ex)
        {
            OverlayLog.Warn($"Hide window 0x{hwnd:X} failed: {ex.Message}");
        }
    }

    // --------------------------------------------------------------- appbar

    public void RegisterAppBar(IntPtr hwnd, string edge)
    {
        lock (_gate)
        {
            if (_appBarRegistered || _disposed != 0 || hwnd == IntPtr.Zero) return;

            _appBarHwnd = hwnd;
            _appBarEdge = string.Equals(edge, "top", StringComparison.OrdinalIgnoreCase)
                ? NativeMethods.ABE_TOP
                : NativeMethods.ABE_BOTTOM;

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = AppBarCallbackMessage,
                uEdge = _appBarEdge,
            };

            var result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref abd);
            if (result == 0)
            {
                OverlayLog.Warn("SHAppBarMessage(ABM_NEW) returned 0 — AppBar not registered");
                _appBarHwnd = IntPtr.Zero;
                return;
            }

            _appBarRegistered = true;
            ApplyPositionLocked();
        }
    }

    public void UnregisterAppBar()
    {
        lock (_gate)
        {
            if (!_appBarRegistered) return;

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = _appBarHwnd,
            };
            try { NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd); } catch { }

            _appBarRegistered = false;
            _appBarHwnd = IntPtr.Zero;
        }
    }

    public void MakeToolWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            style |= NativeMethods.WS_EX_TOOLWINDOW;
            style &= ~NativeMethods.WS_EX_APPWINDOW;
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, style);
        }
        catch (Exception ex)
        {
            OverlayLog.Warn($"MakeToolWindow failed: {ex.Message}");
        }
    }

    public void RefreshPosition()
    {
        lock (_gate) ApplyPositionLocked();
    }

    private void ApplyPositionLocked()
    {
        if (!_appBarRegistered || _appBarHwnd == IntPtr.Zero) return;
        if (ReservedHeight <= 0) ReservedHeight = 76;

        try
        {
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
        catch (Exception ex)
        {
            OverlayLog.Warn($"ApplyPosition failed: {ex.Message}");
        }
    }

    // -------------------------------------------------------------- hotkey

    public void RegisterExitHotkey(IntPtr hwnd, string spec)
    {
        lock (_gate)
        {
            if (_hotkeyRegistered || _disposed != 0 || hwnd == IntPtr.Zero) return;

            if (!TryParseHotkey(spec, out var mods, out var vk))
            {
                OverlayLog.Warn($"Invalid hotkey spec: '{spec}'");
                return;
            }

            _hotkeyHwnd = hwnd;
            _hotkeyRegistered = NativeMethods.RegisterHotKey(hwnd, ExitHotkeyId, mods, vk);
            if (!_hotkeyRegistered)
                OverlayLog.Warn($"RegisterHotKey failed for '{spec}'");
        }
    }

    public void UnregisterExitHotkey()
    {
        lock (_gate)
        {
            if (!_hotkeyRegistered) return;
            try { NativeMethods.UnregisterHotKey(_hotkeyHwnd, ExitHotkeyId); } catch { }
            _hotkeyRegistered = false;
            _hotkeyHwnd = IntPtr.Zero;
        }
    }

    public bool HandleMessage(uint msg, uint wParam, IntPtr lParam)
    {
        if (_disposed != 0) return false;

        if (msg == NativeMethods.WM_HOTKEY && wParam == ExitHotkeyId)
        {
            try { ExitHotkeyPressed?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { OverlayLog.Error("ExitHotkeyPressed handler failed", ex); }
            return true;
        }

        if (msg != AppBarCallbackMessage) return false;

        if (wParam == ABN_POSCHANGED)
        {
            lock (_gate) ApplyPositionLocked();
            try { PositionChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { OverlayLog.Error("PositionChanged handler failed", ex); }
            return true;
        }

        return false;
    }

    // --------------------------------------------------------------- utils

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
        if (parts.Length < 2) return false;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var mod = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt" => NativeMethods.MOD_ALT,
                "shift" => NativeMethods.MOD_SHIFT,
                "win" or "windows" => NativeMethods.MOD_WIN,
                _ => 0u,
            };

            if (mod == 0) return false;
            modifiers |= mod;
        }

        var key = parts[^1];
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = c;
                return true;
            }
            return false;
        }

        if (key.StartsWith('F') || key.StartsWith('f'))
        {
            if (int.TryParse(key.AsSpan(1), out var fn) && fn is >= 1 and <= 24)
            {
                vk = (uint)(0x70 + fn - 1);
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        try { UnregisterExitHotkey(); } catch { }
        try { UnregisterAppBar(); } catch { }
        try { RestoreSystemShell(); } catch { }
    }
}