using System.IO.Pipes;
using System.Net.Sockets;

namespace SatelliteFlex.Ipc;

public class IpcClient(int timeoutMs = 5000)
{
    private const int ProbeTimeoutMs = 300;
    private const int ProbeRetries = 3;
    private const int ProbeDelayMs = 100;

    public async Task<T?> SendAsync<T>(IpcRequest request, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sendTask = SendCoreAsync<T>(request, timeoutCts.Token);
        var timeoutTask = Task.Delay(timeoutMs, CancellationToken.None);

        var completed = await Task.WhenAny(sendTask, timeoutTask);
        if (completed == sendTask)
            return await sendTask;

        timeoutCts.Cancel();
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException(ct);

        throw new TimeoutException($"IPC request '{request.Command}' timed out after {timeoutMs} ms.");
    }

    private async Task<T?> SendCoreAsync<T>(IpcRequest request, CancellationToken ct)
    {
        Stream stream = IpcEndpoint.IsWindows
            ? await ConnectNamedPipeAsync(ct)
            : await ConnectUnixSocketAsync(ct);

        await using (stream)
        {
            await NdjsonFramer.WriteAsync(stream, request, ct);
            return await NdjsonFramer.ReadAsync<T>(stream, ct);
        }
    }

    public async Task<bool> IsDaemonRunningAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < ProbeRetries; attempt++)
        {
            try
            {
                using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                probeCts.CancelAfter(ProbeTimeoutMs);

                var response = await SendAsync<IpcStatusResponse>(
                    new IpcRequest(IpcCommands.Status),
                    probeCts.Token);

                if (response is { Success: true })
                    return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
            }
            catch
            {
            }

            if (attempt == ProbeRetries - 1)
                break;

            try
            {
                await Task.Delay(ProbeDelayMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
    }

    public async Task<bool> WaitForDaemonReadyAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (!cts.IsCancellationRequested)
        {
            if (await IsDaemonRunningAsync(cts.Token))
                return true;

            try
            {
                await Task.Delay(150, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return false;
    }

    private static async Task<Stream> ConnectNamedPipeAsync(CancellationToken ct)
    {
        var pipe = new NamedPipeClientStream(
            ".", IpcEndpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct);
        return pipe;
    }

    private static async Task<Stream> ConnectUnixSocketAsync(CancellationToken ct)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(IpcEndpoint.LinuxAbstractSocket), ct);
        return new NetworkStream(socket, ownsSocket: true);
    }
}
