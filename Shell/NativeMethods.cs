using System.Runtime.InteropServices;

namespace ShellOverlay.Shell;

internal static partial class NativeMethods
{
    // ---------------------------------------------------------- ShowWindow
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    // -------------------------------------------------- SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    // ------------------------------------------------------- AppBar (shell)
    public const uint ABM_NEW = 0x0000;
    public const uint ABM_REMOVE = 0x0001;
    public const uint ABM_QUERYPOS = 0x0002;
    public const uint ABM_SETPOS = 0x0003;
    public const uint ABE_BOTTOM = 3;
    public const uint ABE_TOP = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    // ----------------------------------------------------- Window long bits
    public const int GWL_EXSTYLE = -20;
    public const nint WS_EX_TOOLWINDOW = 0x00000080;
    public const nint WS_EX_APPWINDOW = 0x00040000;

    // ------------------------------------------------------------- Hotkeys
    public const uint WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // -------------------------------------------------------- keybd_event
    public const byte VK_LWIN = 0x5B;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // --------------------------------------------------------------- RECT
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // -------------------------------------------------------------- user32

    [LibraryImport("user32.dll", EntryPoint = "FindWindowW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW",
        StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter,
        string? cls, string? win);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    public static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll")]
    public static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    /// <summary>
    /// Retrieves the screen coordinates of the specified window.
    /// Used by ShellManager to remember where the taskbar was before hiding it,
    /// so it can be restored to its original spot.
    /// </summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // ------------------------------------------------------------- shell32

    [LibraryImport("shell32.dll")]
    public static partial uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
}