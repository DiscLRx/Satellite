using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Data;
using SatelliteCore.Hosting;

namespace SatelliteFlex.Ipc;

public class IpcServer(ISatelliteCoreHost host, TimeSpan instanceStartTimeout)
{
    private readonly TimeSpan _instanceStartTimeout = instanceStartTimeout;

    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private Exception? _fatalException;

    public Exception? FatalException => Volatile.Read(ref _fatalException);

    public void Start(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token), CancellationToken.None);
    }

    public void Stop() => _cts?.Cancel();

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            if (IpcEndpoint.IsWindows)
                await WindowsAcceptLoopAsync(ct);
            else
                await UnixAcceptLoopAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _fatalException, ex);
            throw;
        }
    }

    private async Task WindowsAcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                IpcEndpoint.PipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(ct);
                _ = Task.Run(() => HandleClientAsync(pipe, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { pipe.Dispose(); }
        }
    }

    private async Task UnixAcceptLoopAsync(CancellationToken ct)
    {
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(IpcEndpoint.LinuxAbstractSocket));
        listener.Listen();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await listener.AcceptAsync(ct);
                var stream = new NetworkStream(clientSocket, ownsSocket: true);
                _ = Task.Run(() => HandleClientAsync(stream, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Volatile.Write(ref _fatalException, ex); break; }
        }
        listener.Dispose();
    }

    private async Task HandleClientAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var request = await NdjsonFramer.ReadAsync<IpcRequest>(stream, ct);
            if (request is null) return;

            var response = await DispatchAsync(request, ct);
            await NdjsonFramer.WriteAsync(stream, response, ct);
        }
        catch { }
        finally { stream.Dispose(); }
    }

    private async Task<object> DispatchAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            switch (request.Command)
            {
                case IpcCommands.Status:
                    return new IpcStatusResponse(true, null, BuildStatus());

                case IpcCommands.InstanceStart when request.Port.HasValue:
                    using (var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        startTimeout.CancelAfter(_instanceStartTimeout);
                        try
                        {
                            await host.StartInstanceAsync(request.Port.Value, startTimeout.Token);
                            return new IpcResponse(true);
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                        {
                            return new IpcResponse(false, $"Start timed out after {(int)_instanceStartTimeout.TotalSeconds} seconds.");
                        }
                    }

                case IpcCommands.InstanceStop when request.Port.HasValue:
                    await host.StopInstanceAsync(request.Port.Value, ct);
                    if (!host.AppData.Document.Instances.Any(i => host.IsRunning(i.Port)))
                    {
                        TriggerDaemonShutdown(ct);
                        return new IpcResponse(true, "instance stopped; daemon shutting down");
                    }
                    return new IpcResponse(true);

                case IpcCommands.InstanceAdd when request.Port.HasValue:
                    var addResult = host.AddInstance(new Instance(request.Port.Value, false, []));
                    return new IpcResponse(addResult.Success, addResult.Message);

                case IpcCommands.InstanceRemove when request.Port.HasValue:
                    return await RemoveInstanceWithStopAsync(request.Port.Value, ct);

                case IpcCommands.LocationAdd when request.Port.HasValue && !string.IsNullOrWhiteSpace(request.Name):
                    var addLocation = host.AddLocation(request.Port.Value, new Location(request.Name, request.Path ?? string.Empty));
                    return new IpcResponse(addLocation.Success, addLocation.Message);

                case IpcCommands.LocationRemove when request.Port.HasValue && !string.IsNullOrWhiteSpace(request.Name):
                    if (!host.TryGetLocation(request.Port.Value, request.Name, out var location) || location is null)
                        return new IpcResponse(false, $"Location '{request.Name}' not found.");
                    var removeLocation = host.RemoveLocation(request.Port.Value, location);
                    return new IpcResponse(removeLocation.Success, removeLocation.Message);

                case IpcCommands.Locked when request.BoolValue.HasValue:
                    var lockedResult = host.SetLockedForAllInstances(request.BoolValue.Value);
                    return new IpcResponse(lockedResult.Success, lockedResult.Message);

                case IpcCommands.Password when request.Value is not null:
                    var passwordResult = host.SetPasswordForAllInstances(request.Value);
                    return new IpcResponse(passwordResult.Success, passwordResult.Message);

                case IpcCommands.Shutdown:
                    TriggerDaemonShutdown(ct);
                    return new IpcResponse(true);

                default:
                    return new IpcResponse(false, $"Unknown command: {request.Command}");
            }
        }
        catch (Exception ex)
        {
            return new IpcResponse(false, ex.Message);
        }
    }

    private DaemonStatusView BuildStatus()
    {
        var instances = host.AppData.Document.Instances
            .Select(instance => new InstanceStatusView(
                instance.Port,
                host.IsRunning(instance.Port),
                instance.IsLocked,
                instance.WhiteList.ToArray(),
                instance.Locations.Select(location => new LocationStatusView(location.Name, location.Path)).ToArray()))
            .ToArray();

        return new DaemonStatusView(true, Environment.ProcessPath, host.Paths.DataFilePath, instances);
    }

    private async Task<IpcResponse> RemoveInstanceWithStopAsync(int port, CancellationToken ct)
    {
        if (!host.TryGetInstance(port, out var instance) || instance is null)
            return new IpcResponse(false, $"Instance with port {port} not found.");

        if (host.IsRunning(port))
        {
            using var stopTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stopTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            try
            {
                await host.StopInstanceAsync(port, stopTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                return new IpcResponse(false, "Stop timed out after 10 seconds; remove aborted.");
            }
            catch (Exception ex)
            {
                return new IpcResponse(false, $"Stop failed: {ex.Message}; remove aborted.");
            }
        }

        var removeResult = host.RemoveInstance(port);
        return new IpcResponse(removeResult.Success, removeResult.Message);
    }

    private static void TriggerDaemonShutdown(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120, ct);
            }
            catch
            {
            }

            Environment.Exit(0);
        }, ct);
    }
}
