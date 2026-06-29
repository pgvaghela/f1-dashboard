using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                message = "Telemetry ingest started in the background. This can take a while for full history (2018+). " +
                          "Note: FastF1 ingest is memory-heavy and may be killed on small hosts (e.g. 512 MB) — " +
                          "for production prefer running the ingest externally against Neon (see DEPLOYMENT.md).",
                started.ProcessId,
                started.LogFile,
                seasons = started.Seasons ?? "2018–current (all)",
                started.Force
            });
        }
        catch (TelemetryIngestBusyException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Telemetry seeding status: ingested row counts and whether a background ingest is
    /// running. Lets you verify a seed (local or hosted) without database access.
    /// </summary>
    [HttpGet("telemetry/status")]
    public async Task<IActionResult> TelemetryStatus([FromServices] Data.F1DbContext db, CancellationToken ct)
    {
        if (!_env.IsDevelopment() && !_config.GetValue<bool>("AllowImport"))
        {
            return NotFound();
        }

        try
        {
            var seasons = await db.TelemetryRaces
                .Select(r => r.Season).Distinct().OrderByDescending(s => s).ToListAsync(ct);
            var races = await db.TelemetryRaces.CountAsync(ct);
            var drivers = await db.TelemetryDrivers.CountAsync(ct);
            var laps = await db.TelemetryLaps.CountAsync(ct);

            return Ok(new
            {
                ingestRunning = _telemetryImporter.IsRunning,
                seasons,
                races,
                drivers,
                laps
            });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UndefinedTable)
        {
            return Ok(new { ingestRunning = _telemetryImporter.IsRunning, seasons = Array.Empty<int>(), races = 0, drivers = 0, laps = 0, note = "telemetry tables not created yet" });
        }
    }

    /// <summary>Tail of the background telemetry ingest log (for debugging hosted imports).</summary>
    [HttpGet("telemetry/log")]
    public IActionResult TelemetryLog([FromQuery] int lines = 80)
    {
        if (!_env.IsDevelopment() && !_config.GetValue<bool>("AllowImport"))
        {
            return NotFound();
        }

        var tail = _telemetryImporter.ReadLogTail(Math.Clamp(lines, 10, 500));
        return Ok(new { log = tail ?? "(no telemetry ingest log yet)" });
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
