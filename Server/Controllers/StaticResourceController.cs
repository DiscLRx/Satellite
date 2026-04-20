using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Server.Tools;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/res")]
    public class StaticResourceController : ControllerBase
    {
        [HttpGet("{*filepath}")]
        public async Task<IActionResult> GetResource(string filepath)
        {
            if (!TryBuildResourceName(filepath, out var resourceName, out var normalizedPath))
            {
                return Forbid();
            }

            var assembly = typeof(StaticResourceController).Assembly;
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return NotFound();
            }

            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var bytes = memory.ToArray();
            var contentType = MimeMapper.GetMimeType(normalizedPath) ?? "application/octet-stream";

            return File(bytes, contentType);
        }

        private static bool TryBuildResourceName(
            string filepath,
            out string resourceName,
            out string normalizedPath
        )
        {
            resourceName = string.Empty;
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(filepath))
            {
                return false;
            }

            var segments = filepath
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
            {
                return false;
            }

            normalizedPath = string.Join('/', segments);
            var assemblyName = typeof(StaticResourceController).Assembly.GetName().Name;
            resourceName = $"{assemblyName}.resources.{string.Join('.', segments)}";
            return true;
        }
    }
}
