using Microsoft.AspNetCore.Mvc;

namespace  DotnetAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        public TestController()
        {

        }

        [HttpGet]
        public string Test()
        {
            return "Connection Successful!";
        }
    }
}