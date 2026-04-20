using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Server.Tools;
using Server.Tools.Encoding;
using Server.Tools.Extensions;
using TagLib;

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

    [HttpGet("videoListApi/{locationNameBase64}/{relativePathBase64?}")]
    public IActionResult VideoListApi(string locationNameBase64, string relativePathBase64 = "")
    {
        if (
            !LocationPathResolver.TryResolve(
                _runtimeData.Instance.Locations,
                locationNameBase64,
                relativePathBase64,
                out var resolvedPath
            )
        )
        {
            return BadRequest();
        }

        var candidateFiles = GetVideoFiles(resolvedPath!.FullPath);

        var videos = new List<VideoListItemResponse>();
        for (var index = 0; index < candidateFiles.Count; index += 1)
        {
            var fileInfo = candidateFiles[index];
            var relativePath = fileInfo.FullName[resolvedPath.LocationRoot.Length..].TrimSlash();
            var durationSeconds = TryGetDurationSeconds(fileInfo.FullName);
            videos.Add(
                new VideoListItemResponse(
                    index,
                    fileInfo.Name,
                    relativePath,
                    fileInfo.Extension.ToLowerInvariant(),
                    fileInfo.Length,
                    $"/api/f/{locationNameBase64}/{Base64.ToBase64Url(relativePath)}",
                    durationSeconds
                )
            );
        }

        return Ok(new { total = candidateFiles.Count, videos });
    }

    [HttpPost("videoFilterScript")]
    public IActionResult SaveVideoFilterScript([FromBody] SaveVideoFilterScriptRequest request)
    {
        var scriptName = request.ScriptName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            return BadRequest();
        }

        var scriptBody = request.ScriptBody?.Trim() ?? string.Empty;

        if (
            _runtimeData.Instance.VideoFilterScript is not null
            && _runtimeData.Instance.VideoFilterScript.ContainsKey(scriptName)
        )
        {
            if (string.IsNullOrWhiteSpace(scriptBody))
            {
                _runtimeData.Instance.VideoFilterScript.Remove(scriptName, out var _);
            }
            else
            {
                _runtimeData.Instance.VideoFilterScript[scriptName] = scriptBody;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(scriptBody))
            {
                return BadRequest();
            }

            _runtimeData.Instance.VideoFilterScript ??= new ConcurrentDictionary<string, string>();
            _runtimeData.Instance.VideoFilterScript[scriptName] = scriptBody;
        }
        if (_runtimeData.Instance.VideoFilterScript.Count == 0)
        {
            _runtimeData.Instance.VideoFilterScript = null;
        }

        _runtimeData.SaveChange();
        return Ok(_runtimeData.Instance.VideoFilterScript);
    }

    private static List<FileInfo> GetVideoFiles(string rootPath)
    {
        var candidateFiles = new List<FileInfo>();
        DirectoryTool.RecursiveTraversal(
            rootPath,
            fileInfo =>
            {
                if (IsSupportedVideo(fileInfo))
                {
                    candidateFiles.Add(fileInfo);
                }
            }
        );

        candidateFiles.Sort(
            (left, right) => DirectoryTool.PathCompare(left.FullName, right.FullName)
        );
        return candidateFiles;
    }

    private static bool IsSupportedVideo(FileInfo fileInfo)
    {
        return SupportedVideoExtensions.Contains(fileInfo.Extension.ToLowerInvariant());
    }

    private static double? TryGetDurationSeconds(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return null;
        }

        return TryReadDuration(() => TagLib.File.Create(filePath))
            ?? TryReadDuration(() =>
                TagLib.File.Create(new TagLib.File.LocalFileAbstraction(filePath))
            );
    }

    private static double? TryReadDuration(Func<TagLib.File> fileFactory)
    {
        try
        {
            using var file = fileFactory();
            var duration = file.Properties.Duration;
            return duration > TimeSpan.Zero ? duration.TotalSeconds : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetDetailedErrorMessage(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" -> ", messages);
    }

    public sealed record VideoListItemResponse(
        int Order,
        string Name,
        string RelativePath,
        string Extension,
        long SizeBytes,
        string Url,
        double? DurationSeconds
    );

    public sealed record SaveVideoFilterScriptRequest(string ScriptName, string ScriptBody);
}
