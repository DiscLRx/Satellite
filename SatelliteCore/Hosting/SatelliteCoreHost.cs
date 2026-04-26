using Data;
using SatelliteCore.Configuration;
using SatelliteCore.Instances;
using SatelliteCore.Paths;

namespace SatelliteCore.Hosting;

public class SatelliteCoreHost : ISatelliteCoreHost
{
    private readonly AppDataService _dataService;
    private readonly InstanceManageService _instanceManage;
    private readonly LocationManageService _locationManage;
    private readonly InstanceLifecycleService _lifecycle;

    public RuntimePaths Paths { get; }
    public AppDataDocument Document { get; }
    public IHostAppDataFacade AppData { get; }

    public SatelliteCoreHost(
        string? baseDirectory = null)
    {
        var dir = baseDirectory ?? Directory.GetCurrentDirectory();
        Paths = new RuntimePaths(dir);
        _dataService = new AppDataService(Paths.DataFilePath);
        Document = _dataService.Load();
        AppData = new HostAppDataFacade(Document, _dataService);

        _instanceManage = new InstanceManageService(Document);
        _locationManage = new LocationManageService(Document);
        _lifecycle = new InstanceLifecycleService(Document, Paths, Save);
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        _lifecycle.InitializeCustomPaths();

        var autoStartTasks = Document.Instances
            .Where(i => i.IsRunning)
            .Select(i => Task.Run(async () =>
            {
                try { await _lifecycle.StartInstanceAsync(i, ct); }
                catch (Exception ex) { Console.Error.WriteLine($"[Auto-start] Failed for {i.Port}: {ex.Message}"); }
            }, ct));

        return Task.WhenAll(autoStartTasks);
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _lifecycle.StopAllAsync(ct);
        Save();
    }

    public bool TryGetInstance(int port, out Instance? instance) =>
        _instanceManage.TryGetInstance(port, out instance);

    public OperationResult AddInstance(Instance instance)
    {
        var result = _instanceManage.AddInstance(instance);
        if (result.Success)
        {
            _lifecycle.EnsureCustomPaths(instance);
            Save();
        }
        return result;
    }

    public OperationResult RemoveInstance(int port)
    {
        var result = _instanceManage.RemoveInstance(port);
        if (result.Success)
        {
            _lifecycle.DeleteCustomPaths(port);
            Save();
        }
        return result;
    }

    public OperationResult SetLockedForAllInstances(bool isLocked)
    {
        if (Document.Instances.Count == 0)
            return new OperationResult(false, "No instances configured.");

        foreach (var instance in Document.Instances)
            instance.IsLocked = isLocked;

        Save();
        return OperationResult.Ok;
    }

    public OperationResult SetPasswordForAllInstances(string password)
    {
        if (Document.Instances.Count == 0)
            return new OperationResult(false, "No instances configured.");

        foreach (var instance in Document.Instances)
            instance.Password = password;

        Save();
        return OperationResult.Ok;
    }

    public IReadOnlyList<Location> GetLocations(int port) =>
        _locationManage.GetLocations(port);

    public bool TryGetLocation(int port, string name, out Location? location) =>
        _locationManage.TryGetLocation(port, name, out location);

    public OperationResult AddLocation(int port, Location location)
    {
        var result = _locationManage.AddLocation(port, location);
        if (result.Success) Save();
        return result;
    }

    public OperationResult RemoveLocation(int port, Location location)
    {
        var result = _locationManage.RemoveLocation(port, location);
        if (result.Success) Save();
        return result;
    }

    public bool IsRunning(int port) => _lifecycle.IsRunning(port);

    public async Task StartInstanceAsync(int port, CancellationToken ct = default)
    {
        if (!TryGetInstance(port, out var instance) || instance is null)
            throw new InvalidOperationException($"Instance with port {port} not found.");
        await _lifecycle.StartInstanceAsync(instance, ct);
    }

    public async Task StopInstanceAsync(int port, CancellationToken ct = default)
    {
        if (!TryGetInstance(port, out var instance) || instance is null)
            throw new InvalidOperationException($"Instance with port {port} not found.");
        await _lifecycle.StopInstanceAsync(instance, ct);
    }

    public async Task RestartInstanceAsync(int port, CancellationToken ct = default)
    {
        if (!TryGetInstance(port, out var instance) || instance is null)
            throw new InvalidOperationException($"Instance with port {port} not found.");
        await _lifecycle.RestartInstanceAsync(instance, ct);
    }

    public Task StopAllAsync(CancellationToken ct = default) =>
        _lifecycle.StopAllAsync(ct);

    private void Save() => _dataService.Save(Document);
}
