using SatelliteCore.Configuration;
using SatelliteCore.Paths;
using SatelliteFlex.Configuration;
using SatelliteFlex.Ipc;

namespace SatelliteFlex.Cli;

public enum FlexCommandKind
{
    Invalid,
    Status,
    Shutdown,
    Locked,
    Password,
    InstanceAdd,
    InstanceRemove,
    InstanceStart,
    InstanceStop,
    LocationAdd,
    LocationRemove,
}

public sealed record FlexCommand(
    FlexCommandKind Kind,
    int? Port = null,
    bool? BoolValue = null,
    string? Value = null,
    string? Name = null,
    string? Path = null,
    string? Error = null);

public static class FlexCommandParser
{
    public static FlexCommand Parse(string[] args)
    {
        if (args.Length == 0)
            return Invalid("Command is required.");

        var head = args[0].ToLowerInvariant();
        return head switch
        {
            "status" when args.Length == 1 => new(FlexCommandKind.Status),
            "shutdown" when args.Length == 1 => new(FlexCommandKind.Shutdown),
            "locked" => ParseLocked(args),
            "password" => ParsePassword(args),
            "instance" => ParseInstance(args),
            "location" => ParseLocation(args),
            _ => Invalid($"Unknown command: {string.Join(' ', args)}"),
        };
    }

    private static FlexCommand ParseLocked(string[] args)
    {
        if (args.Length != 2)
            return Invalid("Usage: locked <true|false>");
        if (!bool.TryParse(args[1], out var value))
            return Invalid("Usage: locked <true|false>");

        return new(FlexCommandKind.Locked, BoolValue: value);
    }

    private static FlexCommand ParsePassword(string[] args)
    {
        if (args.Length != 2)
            return Invalid("Usage: password <password>");

        return new(FlexCommandKind.Password, Value: args[1]);
    }

    private static FlexCommand ParseInstance(string[] args)
    {
        if (args.Length != 3)
            return Invalid("Usage: instance <add|remove|start|stop> <port>");
        if (!int.TryParse(args[2], out var port))
            return Invalid("Port must be a valid number.");

        return args[1].ToLowerInvariant() switch
        {
            "add" => new(FlexCommandKind.InstanceAdd, port),
            "remove" => new(FlexCommandKind.InstanceRemove, port),
            "start" => new(FlexCommandKind.InstanceStart, port),
            "stop" => new(FlexCommandKind.InstanceStop, port),
            _ => Invalid("Usage: instance <add|remove|start|stop> <port>"),
        };
    }

    private static FlexCommand ParseLocation(string[] args)
    {
        if (args.Length < 4)
            return Invalid("Usage: location <add|remove> <port> <name> [path]");
        if (!int.TryParse(args[2], out var port))
            return Invalid("Port must be a valid number.");

        var action = args[1].ToLowerInvariant();
        if (action == "remove")
        {
            if (args.Length != 4)
                return Invalid("Usage: location remove <port> <name>");
            return new(FlexCommandKind.LocationRemove, Port: port, Name: args[3]);
        }

        if (action == "add")
        {
            if (args.Length < 5)
                return Invalid("Usage: location add <port> <name> <path>");
            var path = string.Join(' ', args.Skip(4));
            return new(FlexCommandKind.LocationAdd, Port: port, Name: args[3], Path: path);
        }

        return Invalid("Usage: location <add|remove> <port> <name> [path]");
    }

    private static FlexCommand Invalid(string message) => new(FlexCommandKind.Invalid, Error: message);
}

public sealed class FlexCommandExecutor
{
    private const int DefaultIpcTimeoutMs = 5000;

    private readonly IpcClient _ipcClient;
    private readonly StatusCommand _status;
    private readonly ShutdownCommand _shutdown;
    private readonly SecurityCommand _security;
    private readonly InstanceCommand _instance;
    private readonly LocationCommand _location;

    public FlexCommandExecutor(AppDataDocument appData)
    {
        _ipcClient = new IpcClient(ResolveIpcTimeoutMs(appData));
        _status = new StatusCommand(_ipcClient);
        _shutdown = new ShutdownCommand(_ipcClient);
        _security = new SecurityCommand(_ipcClient);
        _instance = new InstanceCommand(_ipcClient);
        _location = new LocationCommand(_ipcClient);
    }

    public async Task<int> ExecuteAsync(FlexCommand command, CancellationToken ct = default)
    {
        try
        {
            return command.Kind switch
            {
                FlexCommandKind.Status => await _status.ExecuteAsync(ct),
                FlexCommandKind.Shutdown => await _shutdown.ExecuteAsync(ct),
                FlexCommandKind.Locked => await _security.SetLockedAsync(command.BoolValue!.Value, ct),
                FlexCommandKind.Password => await _security.SetPasswordAsync(command.Value!, ct),
                FlexCommandKind.InstanceAdd => await _instance.AddAsync(command.Port!.Value, ct),
                FlexCommandKind.InstanceRemove => await _instance.RemoveAsync(command.Port!.Value, ct),
                FlexCommandKind.InstanceStart => await _instance.StartAsync(command.Port!.Value, ct),
                FlexCommandKind.InstanceStop => await _instance.StopAsync(command.Port!.Value, ct),
                FlexCommandKind.LocationAdd => await _location.AddAsync(command.Port!.Value, command.Name!, command.Path!, ct),
                FlexCommandKind.LocationRemove => await _location.RemoveAsync(command.Port!.Value, command.Name!, ct),
                _ => 1,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int ResolveIpcTimeoutMs(AppDataDocument appData)
    {
        var timeoutMs = appData.TryGetSection<SatelliteFlexData>(out var flexData) && flexData is not null
            ? flexData.IpcTimeoutMs
            : DefaultIpcTimeoutMs;

        return timeoutMs > 0 ? timeoutMs : DefaultIpcTimeoutMs;
    }
}
