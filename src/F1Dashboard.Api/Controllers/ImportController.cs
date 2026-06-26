using Microsoft.AspNetCore.Mvc;
using F1Dashboard.Api.Import;
using F1Dashboard.Api.Ml;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    // Seasons imported when no explicit ?seasons= is given.
    private static readonly int[] DefaultSeasons = { 2023, 2024, 2025, 2026 };

    private readonly F1DataImporter _importer;
    private readonly TelemetryImporter _telemetryImporter;
    private readonly RaceWinnerModel _model;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ImportController(
        F1DataImporter importer,
        TelemetryImporter telemetryImporter,
        RaceWinnerModel model,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _importer = importer;
        _telemetryImporter = telemetryImporter;
        _model = model;
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

        // Predictions are cached from the data snapshot, so retrain on the freshly imported
        // data; otherwise the model keeps serving stale predictions until the process restarts.
        var training = await _model.RetrainAsync();

        return Ok(new { import = summary, training });
    }

    /// <summary>
    /// Starts a background FastF1 telemetry ingest (2018+, all drivers per race).
    /// Non-destructive — upserts races/drivers/laps. Enabled in Development, or when
    /// AllowImport=true. Example: POST /api/import/telemetry?seasons=2024,2025,2026
    /// </summary>
    [HttpPost("telemetry")]
    public IActionResult ImportTelemetry([FromQuery] string? seasons, [FromQuery] bool force = false)
    {
        if (!_env.IsDevelopment() && !_config.GetValue<bool>("AllowImport"))
        {
            return NotFound();
        }

        try
        {
            var started = _telemetryImporter.StartBatchImport(seasons, force);
            return Accepted(new
            {
                message = "Telemetry ingest started in the background. This can take a while for full history (2018+).",
                started.ProcessId,
                started.LogFile,
                seasons = started.Seasons ?? "2018–current (all)",
                started.Force
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
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
