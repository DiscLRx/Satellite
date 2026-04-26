using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace SatelliteUI.Tools;

internal static partial class WindowsNotificationTool
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_USER = 0x00000004;

    private const int TimeoutMs = 4500;

    public static bool TryShow(string title, string message)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return TryShowWindows(title, message);
    }

    [SupportedOSPlatform("windows")]
    private static bool TryShowWindows(string title, string message)
    {
        var windowHandle = ResolveNotificationWindowHandle();
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var appName = ResolveAppName();
        var appIcon = ResolveAppIconHandle(out var shouldDestroyAppIcon);
        var displayTitle = string.IsNullOrWhiteSpace(title) ? appName : title;

        var iconData = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = windowHandle,
            uID = (uint)Environment.ProcessId,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = 0,
            hIcon = appIcon,
            szTip = Truncate(appName, 127),
        };

        if (!ShellNotifyIconW(NIM_ADD, ref iconData))
        {
            if (shouldDestroyAppIcon && appIcon != IntPtr.Zero)
            {
                DestroyIcon(appIcon);
            }

            return false;
        }

        var notifyData = iconData;
        notifyData.uFlags = NIF_INFO;
        notifyData.dwInfoFlags = NIIF_USER;
        notifyData.szInfoTitle = Truncate(displayTitle, 63);
        notifyData.szInfo = Truncate(message, 255);
        notifyData.uTimeoutOrVersion = TimeoutMs;

        var shown = ShellNotifyIconW(NIM_MODIFY, ref notifyData);

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeoutMs + 500);
            ShellNotifyIconW(NIM_DELETE, ref iconData);
            if (shouldDestroyAppIcon && appIcon != IntPtr.Zero)
            {
                DestroyIcon(appIcon);
            }
        });

        return shown;
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr ResolveNotificationWindowHandle()
    {
        var processId = (uint)Environment.ProcessId;
        var handle = Process.GetCurrentProcess().MainWindowHandle;
        if (handle != IntPtr.Zero)
        {
            return handle;
        }

        handle = GetActiveWindow();
        if (handle != IntPtr.Zero)
        {
            GetWindowThreadProcessId(handle, out var ownerProcessId);
            if (ownerProcessId == processId)
            {
                return handle;
            }
        }

        handle = FindTopLevelWindowForCurrentProcess();
        if (handle != IntPtr.Zero)
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr FindTopLevelWindowForCurrentProcess()
    {
        IntPtr found = IntPtr.Zero;
        var processId = (uint)Environment.ProcessId;

        EnumWindows(
            (windowHandle, lParam) =>
            {
                GetWindowThreadProcessId(windowHandle, out var ownerProcessId);
                if (ownerProcessId == processId)
                {
                    found = windowHandle;
                    return false;
                }

                return true;
            },
            IntPtr.Zero
        );

        return found;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [SupportedOSPlatform("windows")]
    private static string ResolveAppName()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            try
            {
                var fileInfo = FileVersionInfo.GetVersionInfo(processPath);
                if (!string.IsNullOrWhiteSpace(fileInfo.ProductName))
                {
                    return fileInfo.ProductName;
                }
            }
            catch
            {
            }
        }

        return AppDomain.CurrentDomain.FriendlyName;
    }

    [SupportedOSPlatform("windows")]
    private static IntPtr ResolveAppIconHandle(out bool shouldDestroy)
    {
        shouldDestroy = false;
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var largeIcons = new[] { IntPtr.Zero };
            var smallIcons = new[] { IntPtr.Zero };
            var extracted = ExtractIconExW(processPath, 0, largeIcons, smallIcons, 1);
            if (extracted > 0)
            {
                if (smallIcons[0] != IntPtr.Zero)
                {
                    if (largeIcons[0] != IntPtr.Zero)
                    {
                        DestroyIcon(largeIcons[0]);
                    }

                    shouldDestroy = true;
                    return smallIcons[0];
                }

                if (largeIcons[0] != IntPtr.Zero)
                {
                    shouldDestroy = true;
                    return largeIcons[0];
                }
            }
        }

        return LoadIconW(IntPtr.Zero, (IntPtr)0x7F00); 
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport(
        "shell32.dll",
        EntryPoint = "Shell_NotifyIconW",
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIconW(uint dwMessage, ref NotifyIconData lpData);

    [DllImport(
        "user32.dll",
        EntryPoint = "LoadIconW",
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconExW(
        string lpszFile,
        int nIconIndex,
        IntPtr[] phiconLarge,
        IntPtr[] phiconSmall,
        uint nIcons
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
