using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal sealed class ShutdownCommand(IpcClient ipcClient) : CommandBase(ipcClient)
{
    public Task<int> ExecuteAsync(CancellationToken ct) =>
        SendIpcAsync(new IpcRequest(IpcCommands.Shutdown), ct);
}
