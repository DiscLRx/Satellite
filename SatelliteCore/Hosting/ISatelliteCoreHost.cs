using Data;
using SatelliteCore.Configuration;
using SatelliteCore.Instances;
using SatelliteCore.Paths;

namespace SatelliteCore.Hosting;

public interface ISatelliteCoreHost
{
    RuntimePaths Paths { get; }
    IHostAppDataFacade AppData { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);

    bool TryGetInstance(int port, out Instance? instance);
    OperationResult AddInstance(Instance instance);
    OperationResult RemoveInstance(int port);
    OperationResult SetLockedForAllInstances(bool isLocked);
    OperationResult SetPasswordForAllInstances(string password);

    IReadOnlyList<Location> GetLocations(int port);
    bool TryGetLocation(int port, string name, out Location? location);
    OperationResult AddLocation(int port, Location location);
    OperationResult RemoveLocation(int port, Location location);

    bool IsRunning(int port);
    Task StartInstanceAsync(int port, CancellationToken ct = default);
    Task StopInstanceAsync(int port, CancellationToken ct = default);
    Task RestartInstanceAsync(int port, CancellationToken ct = default);
    Task StopAllAsync(CancellationToken ct = default);

}
