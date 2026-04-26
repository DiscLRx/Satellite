using System.Runtime.InteropServices;
using System.Text.Json;

namespace SatelliteFlex.Ipc;

public static class IpcEndpoint
{
    public const string PipeName = "SatelliteFlex";

public const string LinuxAbstractSocket = "\0satelliteflexd";

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
}

public static class NdjsonFramer
{
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var line = json + "\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<T?> ReadAsync<T>(Stream stream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        var line = await reader.ReadLineAsync(ct);
        if (line is null) return default;
        return JsonSerializer.Deserialize<T>(line);
    }
}
