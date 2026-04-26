using System.Text.RegularExpressions;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

internal sealed partial class SecurityCommand(IpcClient ipcClient) : CommandBase(ipcClient)
{
    public async Task<int> SetLockedAsync(bool isLocked, CancellationToken ct)
    {
        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.Locked, BoolValue: isLocked), ct);

        using var host = new LocalHostScope();

        if (isLocked && !HasValidPasswords(host))
        {
            Console.Error.WriteLine("Password is required and must be alphanumeric.");
            return 1;
        }

        var result = host.Host.SetLockedForAllInstances(isLocked);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    public async Task<int> SetPasswordAsync(string password, CancellationToken ct)
    {
        if (!PasswordRegex().IsMatch(password))
        {
            Console.Error.WriteLine("Password is required and must be alphanumeric.");
            return 1;
        }

        if (await IpcClient.IsDaemonRunningAsync(ct))
            return await SendIpcAsync(new IpcRequest(IpcCommands.Password, Value: password), ct);

        using var host = new LocalHostScope();
        var result = host.Host.SetPasswordForAllInstances(password);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    private static bool HasValidPasswords(LocalHostScope host) =>
        host.Host.AppData.Document.Instances.All(instance => PasswordRegex().IsMatch(instance.Password));

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex PasswordRegex();
}