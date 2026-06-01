using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.DTOs;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StandingsController : ControllerBase
{
    private readonly F1DbContext _context;

    public StandingsController(F1DbContext context)
    {
        _context = context;
    }

    [HttpGet("drivers/{season}")]
    public async Task<ActionResult<IEnumerable<DriverStandingDto>>> GetDriverStandings(int season)
    {
        var standings = await _context.RaceResults
            .Where(rr => rr.Race.Season == season)
            .GroupBy(rr => new
            {
                rr.DriverId,
                rr.Driver.FirstName,
                rr.Driver.LastName
            })
            .Select(g => new DriverStandingDto
            {
                DriverId = g.Key.DriverId,
                FirstName = g.Key.FirstName,
                LastName = g.Key.LastName,
                TotalPoints = g.Sum(rr => rr.Points)
            })
            .OrderByDescending(s => s.TotalPoints)
            .ToListAsync();

        if (standings.Count == 0)
        {
            return NotFound($"No race results found for season {season}");
        }

        // Assign positions after sorting
        for (int i = 0; i < standings.Count; i++)
        {
            standings[i].Position = i + 1;
        }

        return Ok(standings);
    }
}