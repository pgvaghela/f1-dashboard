using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.DTOs;

namespace F1Dashboard.Api.Controllers;

/// <summary>
/// Serves lap telemetry ingested from FastF1 (2018+): the cascading season → race → driver →
/// lap selectors, and a single lap's track outline, sample series and sector classification.
/// </summary>
[ApiController]
[Route("api/lap-data")]
public class LapTelemetryController : ControllerBase
{
    private readonly F1DbContext _context;

    public LapTelemetryController(F1DbContext context)
    {
        _context = context;
    }

    /// <summary>Seasons that have telemetry ingested (FastF1 only covers 2018+).</summary>
    [HttpGet("seasons")]
    public async Task<ActionResult<IEnumerable<int>>> GetSeasons()
    {
        var seasons = await _context.TelemetryRaces
            .Where(r => r.Season >= 2018)
            .Select(r => r.Season)
            .Distinct()
            .OrderByDescending(s => s)
            .ToListAsync();

        return Ok(seasons);
    }

    [HttpGet("races/{season:int}")]
    public async Task<ActionResult<IEnumerable<LapRaceDto>>> GetRaces(int season)
    {
        var races = await _context.TelemetryRaces
            .Where(r => r.Season == season)
            .OrderBy(r => r.Round)
            .Select(r => new LapRaceDto(r.Id, r.Season, r.Round, r.EventName, r.CircuitShortName))
            .ToListAsync();

        return Ok(races);
    }

    [HttpGet("races/{raceId:int}/drivers")]
    public async Task<ActionResult<IEnumerable<LapDriverDto>>> GetDrivers(int raceId)
    {
        var drivers = await _context.TelemetryDrivers
            .Where(d => d.TelemetryRaceId == raceId)
            .OrderBy(d => d.Code)
            .Select(d => new LapDriverDto(d.Id, d.Code, d.FullName, d.TeamName, d.TeamColor, d.HeadshotUrl))
            .ToListAsync();

        return Ok(drivers);
    }

    [HttpGet("races/{raceId:int}/drivers/{driverId:int}/laps")]
    public async Task<ActionResult<IEnumerable<LapListItemDto>>> GetLaps(int raceId, int driverId)
    {
        var laps = await _context.TelemetryLaps
            .Where(l => l.TelemetryDriverId == driverId && l.Driver.TelemetryRaceId == raceId)
            .OrderBy(l => l.LapNumber)
            .Select(l => new LapListItemDto(l.Id, l.LapNumber, l.LapTimeSeconds, l.Compound))
            .ToListAsync();

        return Ok(laps);
    }

    /// <summary>Full detail for one lap: outline, ordered samples, sector times and colours.</summary>
    [HttpGet("laps/{lapId:int}")]
    public async Task<ActionResult<LapDetailDto>> GetLapDetail(int lapId)
    {
        var lap = await _context.TelemetryLaps
            .Where(l => l.Id == lapId)
            .Select(l => new
            {
                l.Id,
                l.LapNumber,
                l.LapTimeSeconds,
                l.Compound,
                TeamColor = l.Driver.TeamColor,
                Outline = new TrackOutlineDto(
                    l.Driver.Race.OutlinePath, l.Driver.Race.ViewWidth, l.Driver.Race.ViewHeight,
                    new List<string> { l.Driver.Race.Sector1Path, l.Driver.Race.Sector2Path, l.Driver.Race.Sector3Path }),
                l.Sector1Seconds,
                l.Sector2Seconds,
                l.Sector3Seconds,
                l.Sector1Color,
                l.Sector2Color,
                l.Sector3Color
            })
            .FirstOrDefaultAsync();

        if (lap is null)
        {
            return NotFound($"No lap found with id {lapId}");
        }

        var samples = await _context.TelemetrySamples
            .Where(s => s.TelemetryLapId == lapId)
            .OrderBy(s => s.T)
            .Select(s => new LapSampleDto(s.T, s.X, s.Y, s.Speed, s.Compound, s.Position))
            .ToListAsync();

        var sectors = new List<SectorTimeDto>
        {
            new(1, lap.Sector1Seconds, lap.Sector1Color),
            new(2, lap.Sector2Seconds, lap.Sector2Color),
            new(3, lap.Sector3Seconds, lap.Sector3Color)
        };

        return Ok(new LapDetailDto(
            lap.Id, lap.LapNumber, lap.LapTimeSeconds, lap.TeamColor, lap.Compound,
            lap.Outline, sectors, samples));
    }
}
