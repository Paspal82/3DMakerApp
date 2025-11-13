using Microsoft.AspNetCore.Mvc;

namespace _3DMakerApp.Server.Controllers
{
    [ApiController]
    public class WelcomeController : ControllerBase
    {
        [HttpGet("/")]
        [HttpGet("/welcome")]
        public IActionResult Get()
        {
            var html = @"<!doctype html>
<html lang=""it"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>Benvenuto - 3DMakerApp</title>
</head>
<body>
  <main>
    <h1>Benvenuto in 3DMakerApp</h1>
    <p>Questa è la pagina di benvenuto del server.</p>
  </main>
</body>
</html>";

            return Content(html, "text/html; charset=utf-8");
        }
    }
}
