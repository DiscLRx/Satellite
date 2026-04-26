using SatelliteCore.Hosting;
using SatelliteCore.Paths;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal abstract class CommandBase(IpcClient ipcClient)
{
    protected IpcClient IpcClient { get; } = ipcClient;

    protected async Task<int> SendIpcAsync(IpcRequest request, CancellationToken ct)
    {
        var response = await IpcClient.SendAsync<IpcResponse>(request, ct);
        if (response is null)
        {
            Console.Error.WriteLine("No response from daemon.");
            return 2;
        }

        if (!response.Success)
        {
            Console.Error.WriteLine(response.Message ?? "Error");
            return 1;
        }

        Console.WriteLine(string.IsNullOrWhiteSpace(response.Message) ? "OK" : response.Message);
        return 0;
    }

    protected sealed class LocalHostScope : IDisposable
    {
        public ISatelliteCoreHost Host { get; } = new SatelliteCoreHost(SatelliteRuntimeRoot.GetRootDirectory());

        public void Dispose()
        {
            if (Host is IDisposable d)
                d.Dispose();
        }
    }
}
