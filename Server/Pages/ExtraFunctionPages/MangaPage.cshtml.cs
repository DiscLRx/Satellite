using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Tools;
using Server.Tools.Encoding;
using Server.Tools.Extensions;

namespace Server.Pages.ExtraFunctionPages;

public class MangaPage(RuntimeData runtimeData) : PageModel
{
    private readonly RuntimeData _runtimeData = runtimeData;

    public string LocationName { get; private set; } = "";
    public string RelativePath { get; private set; } = "/";
    public List<string> ImageUrlList { get; } = [];

    public IActionResult OnGet(string locationNameBase64, string relativePathBase64 = "")
    {
        if (
            !LocationPathResolver.TryResolve(
                _runtimeData.Instance.Locations,
                locationNameBase64,
                relativePathBase64,
                out var resolved
            )
        )
        {
            return BadRequest();
        }

        LocationName = resolved!.LocationName;
        RelativePath = resolved.RelativePath;

        var imagePathList = Directory
            .GetFiles(resolved.FullPath)
            .Select(f => new FileInfo(f))
            .Where(info => IsImageExtension(info.Extension))
            .Select(info => info.FullName).ToList();

        imagePathList.Sort(DirectoryTool.PathCompare);
        foreach (var imageLocalPath in imagePathList)
        {
            var accessPath = imageLocalPath[resolved.LocationRoot.Length..].TrimSlash();
            ImageUrlList.Add($"/api/f/{Base64.ToBase64Url(LocationName)}/{Base64.ToBase64Url(accessPath)}");
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