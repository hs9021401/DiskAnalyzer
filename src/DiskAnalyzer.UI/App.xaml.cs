using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DiskAnalyzer.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogAndShowException(ex, "AppDomain Unhandled Exception");
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogAndShowException(args.Exception, "Dispatcher Unhandled Exception");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogAndShowException(args.Exception, "Unobserved Task Exception");
            args.SetObserved();
        };
    }

    private static void LogAndShowException(Exception ex, string context)
    {
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiskAnalyzer");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "error.log");

            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 60)}{Environment.NewLine}";
            File.AppendAllText(logFile, logMessage);

            MessageBox.Show(
                $"An unexpected error occurred:\n\n{ex.Message}\n\nDetails have been logged to:\n{logFile}",
                "DiskAnalyzer Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}
