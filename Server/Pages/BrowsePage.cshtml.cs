using System.IO.Enumeration;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Tools;
using Server.Tools.Extensions;
using Base64 = Server.Tools.Encoding.Base64;

namespace Server.Pages;

public class BrowsePage(RuntimeData runtimeData) : PageModel
{
    private RuntimeData _runtimeData = runtimeData;

    public string RelativePath
    {
        get;
        set
        {
            field = value;
            var path = value.Trim('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                RelativePathList = [];
                return;
            }

            RelativePathList = path.Split('/').ToList();
        }
    } = "/";

    public string LocationName { get; set; } = "";
    public List<string> RelativePathList = [];

    public List<DirectoryItem> DirectoryItems = [];

    public IActionResult OnGet(string locationNameBase64, string relativePathBase64 = "")
    {
        var locations = _runtimeData.Instance.Locations;

        var canResolve = LocationPathResolver.TryResolve(locations, locationNameBase64, relativePathBase64, out var resolved);
        if (!canResolve)
        {
            return Redirect("/");
        }
        LocationName = resolved!.LocationName;
        RelativePath = resolved.RelativePath;

        if (!PathExtension.SafeCombine(resolved.LocationRoot, RelativePath, out var localPath) || !Directory.Exists(localPath))
        {
            return BadRequest();
        }

        DirectoryItems = FileHelper.GetDirectoryItems(localPath);
        return Page();
    }
}

public class FileHelper
{
    private static readonly EnumerationOptions RecursiveSizeEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Device,
        ReturnSpecialDirectories = false,
    };

    private static readonly EnumerationOptions TopLevelEnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
    };

    public static List<DirectoryItem> GetDirectoryItems(string basePath)
    {
        var baseInfo = new DirectoryInfo(basePath);
        var dirInfos = baseInfo.GetDirectories("*", TopLevelEnumerationOptions);
        var dirSizes = new long[dirInfos.Length];
        Parallel.For(0, dirInfos.Length, i =>
        {
            dirSizes[i] = GetDirectorySize(dirInfos[i].FullName);
        });
        var dirItems = dirInfos
            .Select((dirInfo, i) => new DirectoryItem(
                dirInfo.Name,
                dirSizes[i],
                "d",
                dirInfo.LastWriteTime
            ))
            .ToList();
        var fileInfos = baseInfo.GetFiles("*", TopLevelEnumerationOptions);
        dirItems.AddRange(
            fileInfos.Select(fileInfo => new DirectoryItem(
                fileInfo.Name,
                fileInfo.Length,
                "f",
                fileInfo.LastWriteTime
            ))
        );
        dirItems.Sort();
        return dirItems;
    }

    private static long GetDirectorySize(string directoryPath)
    {
        // 使用 FileSystemEnumerable<long> 直接从原生枚举数据读取 Length，
        // 避免构造 FileInfo / 二次 stat。
        var sizes = new FileSystemEnumerable<long>(
            directoryPath,
            (ref FileSystemEntry entry) => entry.Length,
            RecursiveSizeEnumerationOptions
        )
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory,
        };

        long totalSize = 0;
        foreach (var fileSize in sizes)
        {
            if (long.MaxValue - totalSize < fileSize)
            {
                return long.MaxValue;
            }
            totalSize += fileSize;
        }
        return totalSize;
    }
}

public partial class DirectoryItem(string name, long size, string type, DateTime lastModify)
    : IComparable<DirectoryItem>
{
    public string Name { get; set; } = name;
    public long Size { get; set; } = size;
    public string Type { get; set; } = type;
    public DateTime LastModify { get; set; } = lastModify;

    public int CompareTo(DirectoryItem? other)
    {
        if (other == null)
            return 1;

        if (this > other)
            return 1;

        if (this < other)
            return -1;

        return 0;
    }


    public static bool operator >(DirectoryItem item1, DirectoryItem item2)
    {
        switch (item1.Type)
        {
            case "d" when item2.Type == "f":
                return false;
            case "f" when item2.Type == "d":
                return true;
            default:
            {
                var i = DirectoryTool.PathCompare(item1.Name, item2.Name);
                return i > 0;
            }
        }
    }

    public static bool operator <(DirectoryItem item1, DirectoryItem item2)
    {
        switch (item1.Type)
        {
            case "d" when item2.Type == "f":
                return true;
            case "f" when item2.Type == "d":
                return false;
            default:
            {
                var i = DirectoryTool.PathCompare(item1.Name, item2.Name);
                return i < 0;
            }
        }
    }
}