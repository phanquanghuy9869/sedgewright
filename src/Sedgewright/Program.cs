using System;
using System.IO;
using Avalonia;
using Avalonia.Threading;
using System.Threading.Tasks;
using Velopack;

namespace Sedgewright;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Helper to persist exception details safely
        static void LogException(Exception? ex, string source)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff");
                string fileName = $"crash_{source}_{timestamp}.log";
                File.WriteAllText(fileName, ex?.ToString() ?? "(no exception object)");
            }
            catch
            {
                // Swallow any logging errors to avoid throwing from global handlers
            }
        }

        // Global handlers for exceptions that won't be caught by the Main try/catch
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            LogException(e.ExceptionObject as Exception, "appdomain");
            Environment.FailFast("Unhandled AppDomain exception.", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogException(e.Exception, "taskscheduler");
            Environment.FailFast("Unobserved task exception.", e.Exception);
        };

        Dispatcher.UIThread.UnhandledException += (sender, e) =>
        {
            LogException(e.Exception, "dispatcher");
            Environment.FailFast("Unhandled UI exception.", e.Exception);
        };

        try
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(true)
                .Run();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (System.Exception ex)
        {
            LogException(ex, "main-catch");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
