using Data;
using SatelliteFlex.Daemon;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal sealed class InstanceCommand(IpcClient ipcClient) : CommandBase(ipcClient)
{
    public async Task<int> AddAsync(int port, CancellationToken ct)
    {
        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.InstanceAdd, port), ct);

        using var host = new LocalHostScope();
        var result = host.Host.AddInstance(new Instance(port, false, []));
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    public async Task<int> RemoveAsync(int port, CancellationToken ct)
    {
        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.InstanceRemove, port), ct);

        using var host = new LocalHostScope();
        if (!host.Host.TryGetInstance(port, out var instance) || instance is null)
        {
            Console.Error.WriteLine($"Instance with port {port} not found.");
            return 1;
        }

        if (host.Host.IsRunning(port))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await host.Host.StopInstanceAsync(port, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Stop timed out after 10 seconds; remove aborted.");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Stop failed: {ex.Message}; remove aborted.");
                return 1;
            }
        }

        var removeResult = host.Host.RemoveInstance(port);
        if (!removeResult.Success)
        {
            Console.Error.WriteLine(removeResult.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    public async Task<int> StartAsync(int port, CancellationToken ct)
    {
        if (!await IpcClient.IsDaemonRunningAsync(ct))
        {
            if (!DaemonRunner.StartDetachedProcess())
            {
                Console.Error.WriteLine("Failed to start daemon process.");
                return 2;
            }

            if (!await IpcClient.WaitForDaemonReadyAsync(TimeSpan.FromSeconds(15), ct))
            {
                Console.Error.WriteLine("Daemon started but did not become ready in time.");
                return 2;
            }
        }

        return await SendIpcAsync(new IpcRequest(IpcCommands.InstanceStart, port), ct);
    }

    public Task<int> StopAsync(int port, CancellationToken ct) =>
        SendIpcAsync(new IpcRequest(IpcCommands.InstanceStop, port), ct);
}
