using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    [Route("api/a")]
    [ApiController]
    public class AuthApiController(RuntimeData runtimeData) : ControllerBase
    {
        private RuntimeData _runtimeData = runtimeData;

        [HttpGet("key")]
        public string ValidateKey([FromQuery] string key)
        {
            return key == _runtimeData.Instance.Password ? "1" : "0";
        }
    }
}
