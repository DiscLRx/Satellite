namespace SatelliteFlex.Modes;

public enum FlexMode
{
    Daemon,
    Cli,
}

public static class FlexModeParser
{
internal const string DaemonEnvVar = "SATELLITEFLEX_INTERNAL_DAEMON";

public static FlexMode Parse(string[] args)
    {
        if (args.Length > 0
            && string.Equals(args[0], "--internal-daemon", StringComparison.Ordinal)
            && Environment.GetEnvironmentVariable(DaemonEnvVar) == "1")
            return FlexMode.Daemon;
        return FlexMode.Cli;
    }
}
