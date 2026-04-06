using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Server.Tools;
using SysFile = System.IO.File;

namespace Server.Controllers
{
    [Route("api/bg")]
    [ApiController]
    public class PageBackgroundApiController(RuntimeData runtimeData) : ControllerBase
    {
        private readonly RuntimeData _runtimeData = runtimeData;

        [HttpGet]
        public IActionResult GetBackground([FromQuery] string imageType)
        {
            var instanceCustom = _runtimeData.Instance.InstanceCustom;
            if (instanceCustom is null)
            {
                return BadRequest();
            }
            var bgImageDir = imageType switch
            {
                "horizontal" => instanceCustom.BackgroundCustomPathHorizontal,
                "vertical" => instanceCustom.BackgroundCustomPathVertical,
                _ => null,
            };
            if (bgImageDir is null)
            {
                return BadRequest();
            }

            var bgImages = Directory.GetFiles(bgImageDir);
            if (bgImages.Length == 0)
            {
                return NotFound();
            }
            var bgImage = bgImages[Random.Shared.Next(bgImages.Length)];
            var fs = SysFile.OpenRead(bgImage);
            var contentType = MimeMapper.GetMimeType(bgImage) ?? "application/octet-stream";
            return File(fs, contentType);
        }
    }
}
