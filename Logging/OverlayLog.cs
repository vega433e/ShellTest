namespace ShellOverlay.Logging;

/// <summary>
/// Thread-safe, rotation-aware file logger.
/// Never throws — logging must not break the overlay.
/// </summary>
public static class OverlayLog
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const int MaxFiles = 3;

    private static readonly object Gate = new();
    private static string _path = "";
    private static StreamWriter? _writer;
    private static long _bytesWritten;

    public static void Initialize(string configDirectory)
    {
        lock (Gate)
        {
            CloseLocked();

            var dir = string.IsNullOrWhiteSpace(configDirectory)
                ? AppContext.BaseDirectory
                : configDirectory;

            try { Directory.CreateDirectory(dir); } catch { /* ignore */ }

            _path = Path.Combine(dir, "shelloverlay.log");
            OpenLocked();
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);

        lock (Gate)
        {
            if (_writer is null) return;

            try
            {
                _writer.WriteLine(line);
                _bytesWritten += line.Length + Environment.NewLine.Length;

                if (_bytesWritten >= MaxBytes)
                {
                    RotateLocked();
                    OpenLocked();
                }
            }
            catch
            {
                // Last-resort: drop the message. Never crash the caller.
                _writer = null;
            }
        }
    }

    private static void OpenLocked()
    {
        try
        {
            if (File.Exists(_path))
            {
                var length = new FileInfo(_path).Length;
                if (length >= MaxBytes)
                    RotateLocked();
                else
                    _bytesWritten = length;
            }

            var stream = new FileStream(
                _path, FileMode.Append, FileAccess.Write, FileShare.Read,
                bufferSize: 4096, useAsync: false);

            _writer = new StreamWriter(stream) { AutoFlush = true };
            _bytesWritten = stream.Length;
        }
        catch
        {
            _writer = null;
        }
    }

    private static void RotateLocked()
    {
        CloseLocked();

        for (var i = MaxFiles - 1; i >= 1; i--)
        {
            var src = i == 1 ? _path : $"{_path}.{i - 1}";
            var dst = $"{_path}.{i}";

            try { if (File.Exists(dst)) File.Delete(dst); } catch { }
            try { if (File.Exists(src)) File.Move(src, dst); } catch { }
        }

        _bytesWritten = 0;
    }

    private static void CloseLocked()
    {
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }
}