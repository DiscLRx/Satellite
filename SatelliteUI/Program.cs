using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Avalonia;
using ReactiveUI.Avalonia;

namespace SatelliteUI;

internal sealed class Program
{
    [STAThread]
    [SupportedOSPlatform("windows")]
    public static void Main(string[] args)
    {
        var instanceKey = GetInstanceKey();

        using var singleInstanceMutex = new Mutex(
            true,
            $@"Local\{instanceKey}.mutex",
            out var createdNew
        );

        if (!createdNew)
        {
            try
            {
                using var showWindowEvent = EventWaitHandle.OpenExisting(
                    $@"Local\{instanceKey}.show"
                );

                showWindowEvent.Set();
            }
            catch
            {
            }

            return;
        }

        App.ShowWindowEventName = $@"Local\{instanceKey}.show";

        Environment.CurrentDirectory = AppContext.BaseDirectory;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();
    }

    private static string GetInstanceKey()
    {
        var processPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var fullPath = Path.GetFullPath(processPath).ToUpperInvariant();

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath));
        return Convert.ToHexString(hashBytes);
    }
}
