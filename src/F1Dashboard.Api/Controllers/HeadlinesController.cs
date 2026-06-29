using Microsoft.AspNetCore.Mvc;
using F1Dashboard.Api.News;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HeadlinesController : ControllerBase
{
    private readonly HeadlinesService _headlines;

    public HeadlinesController(HeadlinesService headlines)
    {
        _headlines = headlines;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HeadlineItem>>> GetLatest([FromQuery] int limit = 6, CancellationToken ct = default)
    {
        var items = await _headlines.GetLatestAsync(limit, ct);
        return Ok(items);
    }
}
