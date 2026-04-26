using SatelliteCore.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SatelliteFlex.Configuration;

public class SatelliteFlexData : AppDataSectionBase
{
    public const string SectionName = "SatelliteFlex";

public int IpcTimeoutMs { get; set; } = 5000;

public int InstanceStartTimeoutMs { get; set; } = 15000;
}
