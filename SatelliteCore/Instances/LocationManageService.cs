using Data;
using SatelliteCore.Configuration;

namespace SatelliteCore.Instances;

public class LocationManageService(AppDataDocument document)
{
    private Instance? GetInstance(int port) =>
        document.Instances.FirstOrDefault(i => i.Port == port);

    public IReadOnlyList<Location> GetLocations(int port)
    {
        var instance = GetInstance(port);
        return instance?.Locations ?? [];
    }

    public bool TryGetLocation(int port, string name, out Location? location)
    {
        var instance = GetInstance(port);
        location = instance?.Locations.FirstOrDefault(l => l.Name == name);
        return location is not null;
    }

    public OperationResult AddLocation(int port, Location location)
    {
        var instance = GetInstance(port);
        if (instance is null)
            return OperationResult.Fail($"Instance with port {port} not found.");
        if (instance.Locations.Any(l => l.Name == location.Name))
            return OperationResult.Fail($"Location '{location.Name}' already exists.");
        instance.Locations.Add(location);
        return OperationResult.Ok;
    }

    public OperationResult RemoveLocation(int port, Location location)
    {
        var instance = GetInstance(port);
        if (instance is null)
            return OperationResult.Fail($"Instance with port {port} not found.");
        if (!instance.Locations.Remove(location))
            return OperationResult.Fail($"Location '{location.Name}' not found.");
        return OperationResult.Ok;
    }
}
