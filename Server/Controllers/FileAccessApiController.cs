using Microsoft.AspNetCore.Mvc;
using Server.Tools;
using Server.Tools.Extensions;
using SysFile = System.IO.File;

namespace Server.Controllers;

[Route("api/f")]
[ApiController]
public class FileAccessApiController(RuntimeData runtimeData) : ControllerBase
{
    private readonly RuntimeData _runtimeData = runtimeData;

    [HttpPost("upload/{locationNameBase64}/{relativePathBase64?}")]
    public async Task<IActionResult> UploadFiles(string locationNameBase64, string relativePathBase64 = "")
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

        if (!Request.HasFormContentType)
        {
            return BadRequest();
        }

        var form = await Request.ReadFormAsync();
        var files = form.Files;
        if (files.Count == 0)
        {
            return BadRequest();
        }

        var uploaded = 0;
        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            if (!PathExtension.SafeCombine(resolved!.FullPath, fileName, out var targetFilePath))
            {
                continue;
            }

            await using var fs = new FileStream(
                targetFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true
            );
            await file.CopyToAsync(fs);
            uploaded++;
        }

        return Ok(new { uploaded, total = files.Count });
    }

    [HttpGet("{locationNameBase64}/{relativePathBase64}")]
    public async Task GetFileRange(string locationNameBase64, string relativePathBase64)
    {
        if (
            !LocationPathResolver.TryResolve(
                _runtimeData.Instance.Locations,
                locationNameBase64,
                relativePathBase64,
                out var resolved
            )
            || !SysFile.Exists(resolved!.FullPath)
        )
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        var fileFullPath = resolved.FullPath;

        await using var fs = SysFile.OpenRead(fileFullPath);
        ParseRangeHeader(in fs, out long begin, out long end, out int statusCode);
        var response = HttpContext.Response;
        response.StatusCode = statusCode;
        response.Headers.AcceptRanges = "bytes";
        response.ContentType = MimeMapper.GetMimeType(fileFullPath);
        response.ContentLength = end - begin + 1;
        if (statusCode == 206)
            response.Headers.ContentRange = $"bytes {begin}-{end}/{fs.Length}";

        var encodedFileName = Uri.EscapeDataString(Path.GetFileName(fileFullPath));
        var mimeType = response.ContentType ?? "";
        var disposition = mimeType.StartsWith("image/") || mimeType.StartsWith("video/") ||
                          mimeType.StartsWith("audio/") || mimeType == "application/pdf"
            ? "inline"
            : "attachment";
        response.Headers.ContentDisposition = $"{disposition}; filename*=UTF-8''{encodedFileName}";

        await WriteRangeToResponse(fs, begin, end);
    }

    private void ParseRangeHeader(in FileStream stream, out long begin, out long end, out int statusCode)
    {
        var range = HttpContext.Request.Headers.Range;
        if (range.Count == 0)
        {
            
            statusCode = 200;
            begin = 0;
            end = stream.Length - 1;
        }
        else
        {
            statusCode = 206;
            var rangeValues = range[0]!.Split("=")[1].Split("-");
            begin = Convert.ToInt64(rangeValues[0]);
            if (begin > stream.Length || begin < 0)
            {
                begin = 0;
            }

            if (string.IsNullOrWhiteSpace(rangeValues[1]))
            {
                end = stream.Length - 1;
            }
            else
            {
                end = Convert.ToInt64(rangeValues[1]);
                if (end > stream.Length - 1 || end < begin)
                {
                    end = stream.Length - 1;
                }
            }
        }
    }

    private async Task WriteRangeToResponse(FileStream fs, long begin, long end)
    {
        fs.Position = begin;
        var totalSize = end - begin + 1;
        var bufferSize = 1024 * 1024;
        var buffer = new byte[bufferSize];
        long currentPosition = 0;
        var body = HttpContext.Response.Body;
        while (currentPosition < totalSize)
        {
            var readSize = (int)Math.Min(bufferSize, totalSize - currentPosition);
            await fs.ReadExactlyAsync(buffer, 0, readSize);
            await body.WriteAsync(buffer.AsMemory(0, readSize));
            currentPosition += readSize;
        }
    }
}
