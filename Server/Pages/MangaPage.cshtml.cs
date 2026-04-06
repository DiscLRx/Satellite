using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Tools;
using Server.Tools.Encoding;
using Server.Tools.Extensions;

namespace Server.Pages;

public partial class MangaPage(RuntimeData runtimeData) : PageModel
{
    private readonly RuntimeData _runtimeData = runtimeData;

    public string LocationName { get; private set; } = "";
    public string BasePath { get; private set; } = "/";
    public List<string> ImageUrlList { get; } = [];

    public IActionResult OnGet(string locationBase64, string pathBase64 = "")
    {
        try
        {
            LocationName = Base64.FromBase64UrlToString(locationBase64);
            BasePath = Base64.FromBase64UrlToString(pathBase64).TrimSlash();
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        var location = _runtimeData.Instance.Locations.SingleOrDefault(loc =>
            loc.Name == LocationName
        );
        if (location == null)
        {
            return Redirect("/");
        }

        var locationRoot = Path.GetFullPath(location.Path.TrimSlash());
        var localPath = Path.GetFullPath(Path.Combine(locationRoot, BasePath));

        // Security guard: ensure resolved path stays under location root.
        if (
            !localPath.StartsWith(locationRoot, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(localPath)
        )
        {
            return BadRequest();
        }

        var imagePathList = Directory
            .GetFiles(localPath)
            .Select(f => new FileInfo(f))
            .Where(info => IsImageExtension(info.Extension))
            .Select(info => info.FullName).ToList();

        imagePathList.Sort(DirectoryTool.PathCompare);
        foreach (var imageLocalPath in imagePathList)
        {
            var accessPath = imageLocalPath[locationRoot.Length..].TrimSlash();
            ImageUrlList.Add($"/api/f/{locationBase64}/{Base64.ToBase64Url(accessPath)}");
        }

        return Page();
    }

    private static bool IsImageExtension(string ext)
    {
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }

}