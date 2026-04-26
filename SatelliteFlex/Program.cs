using SatelliteFlex.Cli;
using SatelliteFlex.Daemon;
using SatelliteFlex.Modes;

return FlexModeParser.Parse(args) switch
{
    FlexMode.Daemon => await DaemonRunner.RunAsync(),
    _               => await CliRunner.RunAsync(args),
};
