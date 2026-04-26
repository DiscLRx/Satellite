using SatelliteCore.Configuration;
using SatelliteCore.Paths;

namespace SatelliteFlex.Cli;

public static class CliRunner
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct = default)
    {
        var command = FlexCommandParser.Parse(args);
        if (command.Kind == FlexCommandKind.Invalid)
        {
            Console.Error.WriteLine(command.Error ?? "Invalid command.");
            PrintUsage();
            return 1;
        }

        var dataFilePath = new RuntimePaths(SatelliteRuntimeRoot.GetRootDirectory()).DataFilePath;
        var appDataService = new AppDataService(dataFilePath);
        var appData = appDataService.Load();

        var executor = new FlexCommandExecutor(appData);
        return await executor.ExecuteAsync(command, ct);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: SatelliteFlex <command>");
        Console.WriteLine("Commands:");
        Console.WriteLine("  status");
        Console.WriteLine("  shutdown");
        Console.WriteLine("  locked <true|false>");
        Console.WriteLine("  password <password>");
        Console.WriteLine("  instance add <port>");
        Console.WriteLine("  instance remove <port>");
        Console.WriteLine("  instance start <port>");
        Console.WriteLine("  instance stop <port>");
        Console.WriteLine("  location add <port> <name> <path>");
        Console.WriteLine("  location remove <port> <name>");
    }
}
