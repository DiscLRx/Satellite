using Microsoft.AspNetCore.Mvc;
using Server.Tools.Encoding;
using Server.Tools.Extensions;
using Server.Tools;

namespace Server.Controllers;

[Route("api/ex")]
[ApiController]
public class ExtraFunctionApiController(RuntimeData runtimeData) : ControllerBase
{
    private readonly RuntimeData _runtimeData = runtimeData;
    private static readonly HashSet<string> SupportedVideoExtensions =
    [
        ".mp4",
        ".mov",
        ".webm",
        ".m4v",
    ];

    [HttpGet("shuffle-play/{locationBase64}/{pathBase64?}")]
    public IActionResult RandomPlay(string locationBase64, string pathBase64 = "")
    {
        var locationName = Base64.FromBase64UrlToString(locationBase64);
        var path = Base64.FromBase64UrlToString(pathBase64).TrimSlash();

        var locations = _runtimeData.Instance.Locations;
        var location = locations.SingleOrDefault(loc => loc.Name == locationName);
        if (location == null)
        {
            return BadRequest();
        }

        var locationRoot = location.Path.TrimSlash();
        var localPath = Path.Combine(locationRoot, path);

        var videoPathList = new List<string>();
        DirectoryTool.RecursiveTraversal(localPath, fileInfo =>
            {
                if (SupportedVideoExtensions.Contains(fileInfo.Extension))
                {
                    videoPathList.Add(fileInfo.FullName);
                }
            });
        if (videoPathList.Count == 0)
        {
            return Redirect($"/b/{locationBase64}/{pathBase64}");
        }

        var index = Random.Shared.Next(0, videoPathList.Count);
        var videoLocalPath = videoPathList[index];
        var videoAccessPath = videoLocalPath[locationRoot.Length .. ].TrimSlash();

        var redirectRequestPath = $"/api/f/{locationBase64}/{Base64.ToBase64Url(videoAccessPath)}";
        return Redirect(redirectRequestPath);
    }

}