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

    public string BasePath
    {
        get;
        set
        {
            field = value;
            var path = value.Trim('/');
            if (string.IsNullOrWhiteSpace(path))
            {
                BasePathList = [];
                return;
            }

            BasePathList = path.Split('/').ToList();
        }
    } = "/";

    public string LocationName { get; set; } = "";
    public List<string> BasePathList = [];

    public List<DirectoryItem> DirectoryItems = [];

    public IActionResult OnGet(string locationBase64, string pathBase64 = "")
    {
        var locations = _runtimeData.Instance.Locations;

        // 参数校验
        try
        {
            LocationName = Base64.FromBase64UrlToString(locationBase64);
            BasePath = Base64.FromBase64UrlToString(pathBase64).TrimSlash();
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        var location = locations.FirstOrDefault(loc => loc.Name == LocationName);
        if (location == null)
        {
            return Redirect("/");
        }

        var locationRoot = location.Path;
        var localPath = Path.GetFullPath(Path.Combine(locationRoot, BasePath));
        if (!localPath.StartsWith(locationRoot) || !Directory.Exists(localPath))
        {
            return BadRequest();
        }

        DirectoryItems = FileHelper.GetDirectoryItems(localPath);
        return Page();
    }
}

public class FileHelper
{
    public static List<DirectoryItem> GetDirectoryItems(string basePath)
    {
        var dirs = Directory.GetDirectories(basePath);
        var dirItems = dirs.Select(dir => new DirectoryInfo(dir))
            .Select(dirInfo => new DirectoryItem(
                dirInfo.Name,
                GetDirectorySize(dirInfo.FullName),
                "d",
                dirInfo.LastWriteTime
            ))
            .ToList();
        var files = Directory.GetFiles(basePath);
        dirItems.AddRange(
            files
                .Select(file => new FileInfo(file))
                .Select(fileInfo => new DirectoryItem(
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
        long totalSize = 0;

        DirectoryTool.RecursiveTraversal(
            directoryPath,
            fileInfo =>
            {
                var fileSize = fileInfo.Length;
                if (long.MaxValue - totalSize < fileSize)
                {
                    totalSize = long.MaxValue;
                }
                else
                {
                    totalSize += fileSize;
                }
            }
        );

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

    // [LibraryImport("Shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    // private static partial int StrCmpLogicalW(string a, string b);


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