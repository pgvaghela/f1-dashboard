using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.DTOs;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RacesController : ControllerBase
{
    private readonly F1DbContext _context;

    public RacesController(F1DbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RaceDto>>> GetRaces([FromQuery] int? season)
    {
        var query = _context.Races.AsQueryable();

        if (season.HasValue)
        {
            query = query.Where(r => r.Season == season.Value);
        }

        var races = await query
            .OrderBy(r => r.Season)
            .ThenBy(r => r.Round)
            .Select(r => new RaceDto
            {
                Id = r.Id,
                Season = r.Season,
                Round = r.Round,
                Name = r.Name,
                Date = r.Date,
                CircuitId = r.CircuitId,
                CircuitName = r.Circuit.CircuitName,
                Country = r.Circuit.Country,
                Locality = r.Circuit.Locality,
                LengthKm = r.Circuit.LengthKm
            })
            .ToListAsync();

        return Ok(races);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RaceDto>> GetRace(int id)
    {
        var race = await _context.Races
            .Where(r => r.Id == id)
            .Select(r => new RaceDto
            {
                Id = r.Id,
                Season = r.Season,
                Round = r.Round,
                Name = r.Name,
                Date = r.Date,
                CircuitId = r.CircuitId,
                CircuitName = r.Circuit.CircuitName,
                Country = r.Circuit.Country,
                Locality = r.Circuit.Locality,
                LengthKm = r.Circuit.LengthKm
            })
            .FirstOrDefaultAsync();

        if (race == null)
        {
            return NotFound();
        }

        return Ok(race);
    }

    [HttpGet("{id}/results")]
    public async Task<ActionResult<IEnumerable<RaceResultDto>>> GetRaceResults(int id)
    {
        var raceExists = await _context.Races.AnyAsync(r => r.Id == id);
        if (!raceExists)
        {
            return NotFound($"No race found with id {id}");
        }

        var results = await _context.RaceResults
            .Where(rr => rr.RaceId == id)
            .OrderBy(rr => rr.FinishPosition == null)
            .ThenBy(rr => rr.FinishPosition)
            .Select(rr => new RaceResultDto
            {
                Id = rr.Id,
                RaceId = rr.RaceId,
                DriverId = rr.DriverId,
                FirstName = rr.Driver.FirstName,
                LastName = rr.Driver.LastName,
                Code = rr.Driver.Code,
                ConstructorId = rr.ConstructorId,
                TeamName = rr.Constructor.TeamName,
                GridPosition = rr.GridPosition,
                FinishPosition = rr.FinishPosition,
                Points = rr.Points,
                Laps = rr.Laps,
                FastestLapTime = rr.FastestLapTime,
                GapToWinnerSeconds = rr.GapToWinnerSeconds,
                Status = rr.Status
            })
            .ToListAsync();

        return Ok(results);
    }
}
