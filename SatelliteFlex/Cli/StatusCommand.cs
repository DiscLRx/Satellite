using System.Text.Json;
using SatelliteCore.Hosting;
using SatelliteCore.Paths;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal sealed class StatusCommand(IpcClient ipcClient) : CommandBase(ipcClient)
{
    public async Task<int> ExecuteAsync(CancellationToken ct)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromMilliseconds(300));

        IpcStatusResponse? response;
        try
        {
            response = await IpcClient.SendAsync<IpcStatusResponse>(
                new IpcRequest(IpcCommands.Status),
                probeCts.Token);
        }
        catch
        {
            using var host = new LocalHostScope();
            WriteJson(BuildLocalStatus(host.Host));
            return 0;
        }

        if (response is null)
        {
            Console.Error.WriteLine("No response from daemon.");
            return 2;
        }

        if (!response.Success)
        {
            Console.Error.WriteLine(response.Message ?? "Status failed.");
            return 1;
        }

        WriteJson(response.Status ?? new DaemonStatusView(false, null, GetDefaultAppDataPath(), []));
        return 0;
    }

    private static DaemonStatusView BuildLocalStatus(ISatelliteCoreHost host)
    {
        var instances = host.AppData.Document.Instances
            .Select(i => new InstanceStatusView(
                i.Port,
                host.IsRunning(i.Port),
                i.IsLocked,
                i.WhiteList.ToArray(),
                i.Locations.Select(l => new LocationStatusView(l.Name, l.Path)).ToArray()))
            .ToArray();

        return new DaemonStatusView(false, null, host.Paths.DataFilePath, instances);
    }

    private static string GetDefaultAppDataPath() =>
        new RuntimePaths(SatelliteRuntimeRoot.GetRootDirectory()).DataFilePath;

    private static void WriteJson(DaemonStatusView status) =>
        Console.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
}
