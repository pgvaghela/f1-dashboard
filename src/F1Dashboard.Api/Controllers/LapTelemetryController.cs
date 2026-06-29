using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.DTOs;
using Npgsql;

namespace F1Dashboard.Api.Controllers;

/// <summary>
/// Serves lap telemetry ingested from FastF1 (2018+): the cascading season → race → driver →
/// lap selectors, and a single lap's track outline, sample series and sector classification.
/// </summary>
[ApiController]
[Route("api/lap-data")]
public class LapTelemetryController : ControllerBase
{
    private record LapDetailProjection(
        int Id,
        int LapNumber,
        double? LapTimeSeconds,
        string? Compound,
        string TeamColor,
        TrackOutlineDto Outline,
        double? Sector1Seconds,
        double? Sector2Seconds,
        double? Sector3Seconds,
        string? Sector1Color,
        string? Sector2Color,
        string? Sector3Color);

    private readonly F1DbContext _context;
    private readonly ILogger<LapTelemetryController> _logger;

    public LapTelemetryController(F1DbContext context, ILogger<LapTelemetryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>Seasons that have telemetry ingested (FastF1 only covers 2018+).</summary>
    [HttpGet("seasons")]
    public async Task<ActionResult<IEnumerable<int>>> GetSeasons()
    {
        var seasons = await TryOrEmptyAsync(async () => await _context.TelemetryRaces
            .Where(r => r.Season >= 2018)
            .Select(r => r.Season)
            .Distinct()
            .OrderByDescending(s => s)
            .ToListAsync());

        return Ok(seasons);
    }

    [HttpGet("races/{season:int}")]
    public async Task<ActionResult<IEnumerable<LapRaceDto>>> GetRaces(int season)
    {
        var races = await TryOrEmptyAsync(async () => await _context.TelemetryRaces
            .Where(r => r.Season == season)
            .OrderBy(r => r.Round)
            .Select(r => new LapRaceDto(r.Id, r.Season, r.Round, r.EventName, r.CircuitShortName))
            .ToListAsync());

        return Ok(races);
    }

    [HttpGet("races/{raceId:int}/drivers")]
    public async Task<ActionResult<IEnumerable<LapDriverDto>>> GetDrivers(int raceId)
    {
        var drivers = await TryOrEmptyAsync(async () => await _context.TelemetryDrivers
            .Where(d => d.TelemetryRaceId == raceId)
            .OrderBy(d => d.Code)
            .Select(d => new LapDriverDto(d.Id, d.Code, d.FullName, d.TeamName, d.TeamColor, d.HeadshotUrl))
            .ToListAsync());

        return Ok(drivers);
    }

    [HttpGet("races/{raceId:int}/drivers/{driverId:int}/laps")]
    public async Task<ActionResult<IEnumerable<LapListItemDto>>> GetLaps(int raceId, int driverId)
    {
        var laps = await TryOrEmptyAsync(async () => await _context.TelemetryLaps
            .Where(l => l.TelemetryDriverId == driverId && l.Driver.TelemetryRaceId == raceId)
            .OrderBy(l => l.LapNumber)
            .Select(l => new LapListItemDto(l.Id, l.LapNumber, l.LapTimeSeconds, l.Compound))
            .ToListAsync());

        return Ok(laps);
    }

    /// <summary>Full detail for one lap: outline, ordered samples, sector times and colours.</summary>
    [HttpGet("laps/{lapId:int}")]
    public async Task<ActionResult<LapDetailDto>> GetLapDetail(int lapId)
    {
        LapDetailProjection? lap;
        try
        {
            lap = await _context.TelemetryLaps
                .Where(l => l.Id == lapId)
                .Select(l => new LapDetailProjection(
                    l.Id,
                    l.LapNumber,
                    l.LapTimeSeconds,
                    l.Compound,
                    l.Driver.TeamColor,
                    new TrackOutlineDto(
                        l.Driver.Race.OutlinePath, l.Driver.Race.ViewWidth, l.Driver.Race.ViewHeight,
                        new List<string> { l.Driver.Race.Sector1Path, l.Driver.Race.Sector2Path, l.Driver.Race.Sector3Path }),
                    l.Sector1Seconds,
                    l.Sector2Seconds,
                    l.Sector3Seconds,
                    l.Sector1Color,
                    l.Sector2Color,
                    l.Sector3Color))
                .FirstOrDefaultAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning("Telemetry tables are missing on this deployment; returning no lap detail.");
            return NotFound("Lap telemetry is not available yet on this deployment.");
        }

        if (lap is null)
        {
            return NotFound($"No lap found with id {lapId}");
        }

        var samples = await TryOrEmptyAsync(async () => await _context.TelemetrySamples
            .Where(s => s.TelemetryLapId == lapId)
            .OrderBy(s => s.T)
            .Select(s => new LapSampleDto(s.T, s.X, s.Y, s.Speed, s.Compound, s.Position))
            .ToListAsync());

        var sectors = new List<SectorTimeDto>
        {
            new(1, lap.Sector1Seconds, lap.Sector1Color ?? string.Empty),
            new(2, lap.Sector2Seconds, lap.Sector2Color ?? string.Empty),
            new(3, lap.Sector3Seconds, lap.Sector3Color ?? string.Empty)
        };

        return Ok(new LapDetailDto(
            lap.Id, lap.LapNumber, lap.LapTimeSeconds, lap.TeamColor, lap.Compound ?? string.Empty,
            lap.Outline, sectors, samples));
    }

    private async Task<List<T>> TryOrEmptyAsync<T>(Func<Task<List<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning("Telemetry tables are missing on this deployment; returning an empty response.");
            return new List<T>();
        }
    }
}
