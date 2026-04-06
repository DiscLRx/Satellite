using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Data;
using Server;

namespace Satellite.Service;

public record OperationResult(bool Success, string Message);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppData))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

public class ServiceController
{
    private const string DataFilePath = "appdata.json";
    private string InstancesCustomRoot = "Custom/Instances";
    public AppData AppData { get; set; }

    public Dictionary<int, WebServer> RunningInstances = [];

    public ServiceController()
    {
        AppData = LoadData();
        InstancesCustomRoot = Path.GetFullPath(InstancesCustomRoot);
        CreateDirectoryIfNotExist(InstancesCustomRoot);
        foreach (var instance in AppData.Instances)
        {
            CreateInstanceCustomPathIfNotExist(instance);
            if (instance.IsRunning)
            {
                Task.Run(() => StartInstanceAsync(instance));
            }
        }
    }

    private void CreateInstanceCustomPathIfNotExist(Instance instance)
    {
        var backgroundCustomPathHorizontal = $"{InstancesCustomRoot}/{instance.Port}/Background/Horizontal";
        var backgroundCustomPathVertical = $"{InstancesCustomRoot}/{instance.Port}/Background/Vertical";
        CreateDirectoryIfNotExist(backgroundCustomPathHorizontal);
        CreateDirectoryIfNotExist(backgroundCustomPathVertical);
        instance.InstanceCustom = new InstanceCustom(backgroundCustomPathHorizontal, backgroundCustomPathVertical);
    }

    private void CreateDirectoryIfNotExist(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private readonly Lock _ioLock = new Lock();

    public AppData LoadData()
    {
        lock (_ioLock)
        {
            var dataText = File.ReadAllText(DataFilePath);
            return JsonSerializer.Deserialize<AppData>(
                    dataText,
                    SourceGenerationContext.Default.AppData
                ) ?? throw new NullReferenceException();
        }
    }

    public void SaveChange()
    {
        lock (_ioLock)
        {
            var dataText = JsonSerializer.Serialize(
                AppData,
                SourceGenerationContext.Default.AppData
            );
            File.WriteAllText(DataFilePath, dataText);
        }
    }

    public OperationResult AddInstance(Instance instance)
    {
        if (AppData.Instances.Any(inst => inst.Port == instance.Port))
            return new OperationResult(false, $"Instance with port {instance.Port} already exists");
        AppData.Instances.Add(instance);
        SaveChange();
        CreateInstanceCustomPathIfNotExist(instance);
        return new OperationResult(true, string.Empty);
    }

    public void RemoveInstance(Instance instance)
    {
        AppData.Instances.Remove(instance);
        SaveChange();
        DeleteInstanceCustomPath(instance.Port);
    }

    public void DeleteInstanceCustomPath(int port)
    {
        var instanceCustomPath = $"{InstancesCustomRoot}/{port}";
        if (Directory.Exists(instanceCustomPath))
        {
            Directory.Delete(instanceCustomPath, true);
        }
    }

    public OperationResult AddLocation(int instancePort, Location location)
    {
        var instance = AppData.Instances.Single(inst => inst.Port == instancePort);
        if (instance.Locations.Any(loc => loc.Name == location.Name))
            return new OperationResult(false, $"Location with name {location.Name} already exists");
        instance.Locations.Add(location);
        SaveChange();
        return new OperationResult(true, string.Empty);
    }

    public void RemoveLocation(int instancePort, Location location)
    {
        var instance = AppData.Instances.Single(inst => inst.Port == instancePort);
        instance.Locations.Remove(location);
        SaveChange();
    }

    public async Task StartInstanceAsync(Instance instance)
    {
        var server = new WebServer(instance);
        RunningInstances[instance.Port] = server;
        await server.StartAsync();
        instance.IsRunning = true;
        SaveChange();
    }

    public async Task StopInstanceAsync(Instance instance)
    {
        RunningInstances.TryGetValue(instance.Port, out var server);
        if (server is null)
        {
            return;
        }
        await server.StopAsync();
        instance.IsRunning = false;
        SaveChange();
    }
}
