using Data;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal sealed class LocationCommand(IpcClient ipcClient) : CommandBase(ipcClient)
{
    public async Task<int> AddAsync(int port, string name, string path, CancellationToken ct)
    {
        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.LocationAdd, Port: port, Name: name, Path: path), ct);

        using var host = new LocalHostScope();
        var result = host.Host.AddLocation(port, new Location(name, path));
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    public async Task<int> RemoveAsync(int port, string name, CancellationToken ct)
    {
        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.LocationRemove, Port: port, Name: name), ct);

        using var host = new LocalHostScope();
        if (!host.Host.TryGetLocation(port, name, out var location) || location is null)
        {
            Console.Error.WriteLine($"Location '{name}' not found.");
            return 1;
        }

        var result = host.Host.RemoveLocation(port, location);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }
}
