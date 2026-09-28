using System.Threading;
using Avalonia;

namespace ShellOverlay;

internal static class Program
{
    private const string MutexName = @"Local\ShellOverlay_SingleInstance_v1";

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteBootstrapCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
            WriteBootstrapCrash("TaskScheduler.UnobservedTaskException", e.Exception);

        try
        {
            using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (!createdNew)
            {
                WriteBootstrapCrash("SingleInstance",
                    new InvalidOperationException("Another instance is already running."));
                return;
            }

            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }
        catch (Exception ex)
        {
            WriteBootstrapCrash("Main", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .LogToTrace()
                  .WithInterFont();

    private static void WriteBootstrapCrash(string source, Exception? ex)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "bootstrap-crash.log");
            var text =
                $"[{DateTime.Now:O}] {source}\n" +
                $"Type:    {ex?.GetType().FullName}\n" +
                $"Message: {ex?.Message}\n" +
                $"Inner:   {ex?.InnerException?.GetType().FullName}: {ex?.InnerException?.Message}\n" +
                $"Stack:\n{ex?.StackTrace}\n\n";
            File.AppendAllText(path, text);
        }
        catch { }
    }
}