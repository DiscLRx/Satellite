namespace SatelliteFlex.Ipc;

public record IpcRequest(
    string Command,
    int? Port = null,
    bool? BoolValue = null,
    string? Value = null,
    string? Name = null,
    string? Path = null);

public record IpcResponse(bool Success, string? Message = null);

public record IpcStatusResponse(bool Success, string? Message, DaemonStatusView? Status = null);

public record DaemonStatusView(bool DaemonIsRunning, string? DaemonProcessPath, string? AppDataPath, InstanceStatusView[] Instances);

public record InstanceStatusView(
    int Port,
    bool IsRunning,
    bool IsLocked,
    string[] WhiteList,
    LocationStatusView[] Locations);

public record LocationStatusView(string Name, string Path);

public static class IpcCommands
{
    public const string Status = "status";
    public const string InstanceAdd = "instance.add";
    public const string InstanceRemove = "instance.remove";
    public const string InstanceStart = "instance.start";
    public const string InstanceStop = "instance.stop";
    public const string LocationAdd = "location.add";
    public const string LocationRemove = "location.remove";
    public const string Locked = "locked";
    public const string Password = "password";
    public const string Shutdown = "shutdown";
}
