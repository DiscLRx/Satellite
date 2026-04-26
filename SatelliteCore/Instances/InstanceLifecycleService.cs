using System.Collections.Concurrent;
using Data;
using SatelliteCore.Configuration;
using SatelliteCore.Paths;
using Server;

namespace SatelliteCore.Instances;

public class InstanceLifecycleService
{
    private readonly AppDataDocument _document;
    private readonly RuntimePaths _paths;
    private readonly Action _saveChange;

    private readonly ConcurrentDictionary<int, WebServer> _running = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    private static readonly OperationResult _ok = OperationResult.Ok;

    public InstanceLifecycleService(
        AppDataDocument document,
        RuntimePaths paths,
        Action saveChange)
    {
        _document = document;
        _paths = paths;
        _saveChange = saveChange;
    }

    public bool IsRunning(int port) => _running.ContainsKey(port);

    public void InitializeCustomPaths()
    {
        Directory.CreateDirectory(_paths.InstancesCustomRoot);
        foreach (var instance in _document.Instances)
            EnsureCustomPaths(instance);
    }

    public void EnsureCustomPaths(Instance instance)
    {
        var h = _paths.GetInstanceBackgroundHorizontal(instance.Port);
        var v = _paths.GetInstanceBackgroundVertical(instance.Port);
        Directory.CreateDirectory(h);
        Directory.CreateDirectory(v);
        instance.InstanceCustom = new InstanceCustom(h, v);
    }

    public void DeleteCustomPaths(int port)
    {
        var dir = _paths.GetInstanceCustomRoot(port);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    public async Task StartInstanceAsync(Instance instance, CancellationToken ct = default)
    {
        var lk = GetLock(instance.Port);
        await lk.WaitAsync(ct);
        try
        {
            if (_running.ContainsKey(instance.Port))
            {
                SetRunning(instance, true);
                return;
            }

            var server = new WebServer(instance, _saveChange);
            _running[instance.Port] = server;
            try
            {
                await server.StartAsync(ct);
                SetRunning(instance, true);
            }
            catch (Exception ex)
            {
                _running.TryRemove(instance.Port, out _);
                await SafeStopAsync(server);
                SetRunning(instance, false);
                throw new InvalidOperationException(
                    $"Failed to start instance on port {instance.Port}.", ex);
            }
        }
        finally
        {
            lk.Release();
        }
    }

    public async Task StopInstanceAsync(Instance instance, CancellationToken ct = default)
    {
        var lk = GetLock(instance.Port);
        await lk.WaitAsync(ct);
        try
        {
            if (!_running.TryRemove(instance.Port, out var server) || server is null)
            {
                SetRunning(instance, false);
                return;
            }
            try { await server.StopAsync(ct); }
            finally { SetRunning(instance, false); }
        }
        finally
        {
            lk.Release();
        }
    }

    public async Task RestartInstanceAsync(Instance instance, CancellationToken ct = default)
    {
        await StopInstanceAsync(instance, ct);
        await StartInstanceAsync(instance, ct);
    }

    public async Task StopAllAsync(CancellationToken ct = default)
    {
        var tasks = _document.Instances
            .Where(i => IsRunning(i.Port))
            .Select(i => StopInstanceAsync(i, ct));
        await Task.WhenAll(tasks);
    }

    private SemaphoreSlim GetLock(int port) =>
        _locks.GetOrAdd(port, _ => new SemaphoreSlim(1, 1));

    private void SetRunning(Instance instance, bool running)
    {
        if (instance.IsRunning == running) return;
        instance.IsRunning = running;
        _saveChange();
    }

    private static async Task SafeStopAsync(WebServer server)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try { await server.StopAsync(cts.Token); }
        catch { }
    }
}
