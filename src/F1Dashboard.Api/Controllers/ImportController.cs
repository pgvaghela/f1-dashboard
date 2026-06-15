using Microsoft.AspNetCore.Mvc;
using F1Dashboard.Api.Import;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    // Seasons imported when no explicit ?seasons= is given.
    private static readonly int[] DefaultSeasons = { 2023, 2024, 2025, 2026 };

    private readonly F1DataImporter _importer;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ImportController(F1DataImporter importer, IWebHostEnvironment env, IConfiguration config)
    {
        _importer = importer;
        _env = env;
        _config = config;
    }

    /// <summary>
    /// Rebuilds the database from the public Jolpica/Ergast F1 API.
    /// Destructive (wipes existing rows). Enabled in Development, or in any
    /// environment when AllowImport=true (set AllowImport__=true to seed a
    /// freshly deployed database once, then turn it back off).
    /// Example: POST /api/import?seasons=2024,2025
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Import(
        [FromQuery] string? seasons, CancellationToken ct)
    {
        if (!_env.IsDevelopment() && !_config.GetValue<bool>("AllowImport"))
        {
            return NotFound();
        }

        var requested = ParseSeasons(seasons);
        if (requested.Count == 0)
        {
            return BadRequest("Provide seasons as a comma-separated list of years, e.g. ?seasons=2024,2025");
        }

        var summary = await _importer.ImportSeasonsAsync(requested, ct);
        return Ok(summary);
    }

    private static List<int> ParseSeasons(string? seasons)
    {
        if (string.IsNullOrWhiteSpace(seasons))
        {
            return DefaultSeasons.ToList();
        }

        return seasons
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var year) ? year : (int?)null)
            .Where(y => y is not null)
            .Select(y => y!.Value)
            .ToList();
    }
}
