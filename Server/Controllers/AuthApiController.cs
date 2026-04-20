using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    [Route("api/a")]
    [ApiController]
    public class AuthApiController(RuntimeData runtimeData) : ControllerBase
    {
        private readonly RuntimeData _runtimeData = runtimeData;

        [HttpPost("key")]
        public string ValidateKey([FromForm] string key)
        {
            return key == _runtimeData.Instance.Password ? "1" : "0";
        }
    }
}
