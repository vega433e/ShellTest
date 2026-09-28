using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShellOverlay.Shell;

/// <summary>
/// Извлекает иконку из .exe / .lnk / любого файла через Win32 SHGetFileInfo.
/// Возвращает Avalonia Bitmap или null, если не удалось.
/// </summary>
public static class IconExtractor
{
    private const uint SHGFI_ICON = 0x00000100;
    private const uint SHGFI_LARGEICON = 0x00000000;
    private const uint SHGFI_SMALLICON = 0x00000001;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x00000010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static Avalonia.Media.Imaging.Bitmap? Extract(string path, bool small = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(path);

        var info = new SHFILEINFO();
        uint flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);

        IntPtr result;
        if (File.Exists(expanded))
        {
            result = SHGetFileInfoW(expanded, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        }
        else
        {
            // Fallback для путей вроде "wt.exe" — берём иконку по расширению.
            var ext = Path.GetExtension(expanded);
            if (string.IsNullOrEmpty(ext)) return null;
            result = SHGetFileInfoW(ext, FILE_ATTRIBUTE_NORMAL, ref info,
                (uint)Marshal.SizeOf<SHFILEINFO>(), flags | SHGFI_USEFILEATTRIBUTES);
        }

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

        try { return ConvertIcon(info.hIcon); }
        catch { return null; }
        finally { DestroyIcon(info.hIcon); }
    }

    private static Avalonia.Media.Imaging.Bitmap? ConvertIcon(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var ii) || ii.hbmColor == IntPtr.Zero) return null;

        try
        {
            using var gdiBmp = System.Drawing.Bitmap.FromHbitmap(ii.hbmColor);
            using var ms = new MemoryStream();
            gdiBmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return new Avalonia.Media.Imaging.Bitmap(ms);
        }
        finally
        {
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
        }
    }
}