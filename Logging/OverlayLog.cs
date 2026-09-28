namespace ShellOverlay.Logging;

public static class OverlayLog
{
    private static readonly object Gate = new();
    private static string _path = Path.Combine(AppContext.BaseDirectory, "shelloverlay.log");

    public static void Initialize(string configDirectory)
    {
        var dir = string.IsNullOrWhiteSpace(configDirectory)
            ? AppContext.BaseDirectory
            : configDirectory;
        _path = Path.Combine(dir, "shelloverlay.log");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        try
        {
            lock (Gate)
                File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch
        {
            // logging must never crash the overlay
        }

        System.Diagnostics.Debug.WriteLine(line);
    }
}
