using System.IO;
using System.Threading;
using System.Threading.Tasks;
using blazorBookLibrary.Client.Services;
using Microsoft.AspNetCore.Mvc;

namespace blazorBookLibrary.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TechnicalInfoController : ControllerBase
{
    private readonly ITechnicalInfoService _technicalInfoService;

    public TechnicalInfoController(ITechnicalInfoService technicalInfoService)
    {
        _technicalInfoService = technicalInfoService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? lang, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _technicalInfoService.GetTechnicalInfoMarkdownAsync(lang, cancellationToken);
            return Content(content, "text/markdown; charset=utf-8");
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
