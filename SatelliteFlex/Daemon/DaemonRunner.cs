using System.Diagnostics;
using SatelliteCore.Hosting;
using SatelliteCore.Paths;
using SatelliteFlex.Configuration;
using SatelliteFlex.Ipc;
using SatelliteFlex.Modes;

namespace SatelliteFlex.Daemon;

public static class SingletonGuard
{
    private const string GlobalMutexName = "SatelliteFlex.Daemon.Singleton";
    private const string FallbackLockFileName = "SatelliteFlex.daemon.lock";

    private static Mutex? _mutex;
    private static FileStream? _lockFile;

    public static bool TryAcquire()
    {
        if (TryAcquireGlobalMutex())
            return true;

        return TryAcquireFallbackFileLock();
    }

    private static bool TryAcquireGlobalMutex()
    {
        try
        {
            var mutexName = OperatingSystem.IsWindows()
                ? "Global\\" + GlobalMutexName
                : GlobalMutexName;

            _mutex = new Mutex(true, mutexName, out var createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                _mutex = null;
                return false;
            }

            return true;
        }
        catch
        {
            _mutex?.Dispose();
            _mutex = null;
            return false;
        }
    }

    private static bool TryAcquireFallbackFileLock()
    {
        try
        {
            var lockFilePath = Path.Combine(AppContext.BaseDirectory, FallbackLockFileName);
            _lockFile = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static void Release()
    {
        _lockFile?.Dispose();
        _lockFile = null;

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }
}

public class DaemonRunner
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WatchdogPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(15);
    private const int DefaultInstanceStartTimeoutMs = 15000;
    private const int MinInstanceStartTimeoutMs = 1000;
    private const int MaxInstanceStartTimeoutMs = 300000;

    public static bool StartDetachedProcess()
    {
        var baseDir = AppContext.BaseDirectory;

        var nativeExe = Path.Combine(baseDir,
            OperatingSystem.IsWindows() ? "SatelliteFlex.exe" : "SatelliteFlex");

        ProcessStartInfo startInfo;
        if (File.Exists(nativeExe))
        {
            startInfo = new ProcessStartInfo(nativeExe)
            {
                Arguments = "--internal-daemon",
            };
        }
        else
        {
            var dll = Path.Combine(baseDir, "SatelliteFlex.dll");
            if (!File.Exists(dll))
                return false;

            startInfo = new ProcessStartInfo("dotnet")
            {
                Arguments = $"\"{dll}\" --internal-daemon",
            };
        }

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WorkingDirectory = baseDir;
        startInfo.Environment[FlexModeParser.DaemonEnvVar] = "1";

        using var process = Process.Start(startInfo);
        if (process is null)
            return false;

        return !process.WaitForExit(1500);
    }

    public static async Task<int> RunAsync(CancellationToken ct = default)
    {
        if (!SingletonGuard.TryAcquire())
        {
            Console.Error.WriteLine("Another SatelliteFlex daemon is already running.");
            return 1;
        }

        try
        {
            return await RunCoreAsync(ct);
        }
        finally
        {
            SingletonGuard.Release();
        }
    }

    private static async Task<int> RunCoreAsync(CancellationToken externalCt)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var isShuttingDown = 0;
        long lastHeartbeatTicks = DateTime.UtcNow.Ticks;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        var host = new SatelliteCoreHost(SatelliteRuntimeRoot.GetRootDirectory());

        var flexData = host.AppData.GetOrCreateSection<SatelliteFlexData>();
        await host.AppData.SetAndSaveSection(flexData, cts.Token);

        await host.InitializeAsync(cts.Token);

        var instanceStartTimeout = ResolveInstanceStartTimeout(flexData);
        var ipcServer = new IpcServer(host, instanceStartTimeout);
        ipcServer.Start(cts.Token);

        var watchdogTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(WatchdogPollInterval);
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                if (Volatile.Read(ref isShuttingDown) != 0)
                    return;

                var fatalException = ipcServer.FatalException;
                if (fatalException is not null)
                {
                    Environment.FailFast("SatelliteFlex IPC server terminated unexpectedly.", fatalException);
                }

                var lastHeartbeatUtc = new DateTime(
                    Interlocked.Read(ref lastHeartbeatTicks),
                    DateTimeKind.Utc);

                if (DateTime.UtcNow - lastHeartbeatUtc > HeartbeatTimeout)
                {
                    Environment.FailFast("SatelliteFlex daemon heartbeat timed out.");
                }
            }
        }, CancellationToken.None);

        Console.WriteLine("SatelliteFlex daemon started. Press Ctrl+C to stop.");

        try
        {
            using var heartbeatTimer = new PeriodicTimer(HeartbeatInterval);
            while (await heartbeatTimer.WaitForNextTickAsync(cts.Token))
            {
                Interlocked.Exchange(ref lastHeartbeatTicks, DateTime.UtcNow.Ticks);
            }
        }
        catch (OperationCanceledException) { }

        Console.WriteLine("Shutting down...");
        Interlocked.Exchange(ref isShuttingDown, 1);
        ipcServer.Stop();
        cts.Cancel();

        try
        {
            await watchdogTask;
        }
        catch (OperationCanceledException)
        {
        }

        await host.ShutdownAsync(CancellationToken.None);

        return 0;
    }

    private static TimeSpan ResolveInstanceStartTimeout(SatelliteFlexData data)
    {
        var timeoutMs = data.InstanceStartTimeoutMs;
        if (timeoutMs < MinInstanceStartTimeoutMs || timeoutMs > MaxInstanceStartTimeoutMs)
            timeoutMs = DefaultInstanceStartTimeoutMs;

        return TimeSpan.FromMilliseconds(timeoutMs);
    }
}
