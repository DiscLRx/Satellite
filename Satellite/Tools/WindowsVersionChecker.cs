using System;
using System.Runtime.InteropServices;

namespace Satellite.Tools;

public static class WindowsVersionChecker
{
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    public static bool IsWindowsVersionAtLeast(int major, int minor, int build)
    {
        return IsWindows() &&
               OperatingSystem.IsWindowsVersionAtLeast(major, minor, build);
    }
}