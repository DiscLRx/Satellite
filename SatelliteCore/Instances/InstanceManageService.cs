using Data;
using SatelliteCore.Configuration;

namespace SatelliteCore.Instances;

public record OperationResult(bool Success, string Message)
{
    public static readonly OperationResult Ok = new(true, string.Empty);
    public static OperationResult Fail(string message) => new(false, message);
}

public class InstanceManageService(AppDataDocument document)
{
    public bool TryGetInstance(int port, out Instance? instance)
    {
        instance = document.Instances.FirstOrDefault(i => i.Port == port);
        return instance is not null;
    }

    public OperationResult AddInstance(Instance instance)
    {
        if (document.Instances.Any(i => i.Port == instance.Port))
            return OperationResult.Fail($"Instance with port {instance.Port} already exists.");
        document.Instances.Add(instance);
        return OperationResult.Ok;
    }

    public OperationResult RemoveInstance(int port)
    {
        var instance = document.Instances.FirstOrDefault(i => i.Port == port);
        if (instance is null)
            return OperationResult.Fail($"Instance with port {port} not found.");
        document.Instances.Remove(instance);
        return OperationResult.Ok;
    }
}
