using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private const string DataTemplateResourceName = "Satellite.appdata.template.json";
    private string InstancesCustomRoot = "Custom/Instances";
    public AppData AppData { get; set; }

    private static readonly OperationResult SuccessResult = new(true, string.Empty);

    public ConcurrentDictionary<int, WebServer> RunningInstances { get; } = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _instanceLocks = new();

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
                Task.Run(async () =>
                {
                    try
                    {
                        await StartInstanceAsync(instance);
                    }
                    catch
                    {
                        // Keep startup robust on app boot. Per-instance failure should not crash startup.
                    }
                });
            }
        }
    }

    private SemaphoreSlim GetInstanceLock(int port)
    {
        return _instanceLocks.GetOrAdd(port, _ => new SemaphoreSlim(1, 1));
    }

    private void SetInstanceRunningState(Instance instance, bool isRunning)
    {
        if (instance.IsRunning == isRunning)
        {
            return;
        }

        instance.IsRunning = isRunning;
        SaveChange();
    }

    private void CreateInstanceCustomPathIfNotExist(Instance instance)
    {
        var backgroundCustomPathHorizontal = $"{InstancesCustomRoot}/{instance.Port}/Background/Horizontal";
        var backgroundCustomPathVertical = $"{InstancesCustomRoot}/{instance.Port}/Background/Vertical";
        CreateDirectoryIfNotExist(backgroundCustomPathHorizontal);
        CreateDirectoryIfNotExist(backgroundCustomPathVertical);
        instance.InstanceCustom = new InstanceCustom(
            backgroundCustomPathHorizontal,
            backgroundCustomPathVertical
        );
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
            EnsureDataFileExists();
            var dataText = File.ReadAllText(DataFilePath);
            return JsonSerializer.Deserialize<AppData>(
                dataText,
                SourceGenerationContext.Default.AppData
            ) ?? throw new NullReferenceException();
        }
    }

    private void EnsureDataFileExists()
    {
        if (File.Exists(DataFilePath))
        {
            return;
        }

        using var templateStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(DataTemplateResourceName);
        if (templateStream is null)
        {
            throw new FileNotFoundException(
                $"Embedded resource '{DataTemplateResourceName}' was not found."
            );
        }

        using var reader = new StreamReader(templateStream);
        var templateText = reader.ReadToEnd();
        File.WriteAllText(DataFilePath, templateText);
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
        var instanceLock = GetInstanceLock(instance.Port);
        await instanceLock.WaitAsync();
        try
        {
            if (RunningInstances.TryGetValue(instance.Port, out _))
            {
                SetInstanceRunningState(instance, true);
                return;
            }

            var server = new WebServer(instance, SaveChange);
            RunningInstances[instance.Port] = server;

            try
            {
                await server.StartAsync();
                SetInstanceRunningState(instance, true);
            }
            catch (Exception ex)
            {
                RunningInstances.TryRemove(instance.Port, out _);
                await SafeStopAsync(server);
                SetInstanceRunningState(instance, false);
                throw new InvalidOperationException(
                    $"Failed to start instance on port {instance.Port}.",
                    ex
                );
            }
        }
        finally
        {
            instanceLock.Release();
        }
    }

    public void StartInstance(Instance instance, Action<OperationResult>? onCompleted = null)
    {
        RunInstanceOperation(() => StartInstanceAsync(instance), onCompleted);
    }

    public async Task StopInstanceAsync(Instance instance)
    {
        var instanceLock = GetInstanceLock(instance.Port);
        await instanceLock.WaitAsync();
        try
        {
            if (!RunningInstances.TryRemove(instance.Port, out var server) || server is null)
            {
                SetInstanceRunningState(instance, false);
                return;
            }

            try
            {
                await server.StopAsync();
            }
            finally
            {
                SetInstanceRunningState(instance, false);
            }
        }
        finally
        {
            instanceLock.Release();
        }
    }

    public void StopInstance(Instance instance, Action<OperationResult>? onCompleted = null)
    {
        RunInstanceOperation(() => StopInstanceAsync(instance), onCompleted);
    }

    private void RunInstanceOperation(
        Func<Task> operation,
        Action<OperationResult>? onCompleted = null
    )
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await operation();
                onCompleted?.Invoke(SuccessResult);
            }
            catch (Exception ex)
            {
                onCompleted?.Invoke(new OperationResult(false, ex.Message));
            }
        });
    }

    private static async Task SafeStopAsync(WebServer server)
    {
        try
        {
            await server.StopAsync();
        }
        catch
        {
            // Ignore cleanup errors while recovering from failed startup.
        }
    }
}
