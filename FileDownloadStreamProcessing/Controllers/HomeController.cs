using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileDownloadStreamProcessing.Controllers
{
    [Route("/")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Greet()
        {
            return new JsonResult(new {
                message = "Hello, World!"
            });
        }
    }
}
