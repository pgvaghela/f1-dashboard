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
        var rows = await _context.RaceResults
            .Where(rr => rr.Race.Season == season)
            .Select(rr => new
            {
                rr.DriverId,
                rr.Driver.FirstName,
                rr.Driver.LastName,
                rr.Driver.Code,
                rr.Driver.Nationality,
                rr.Race.Round,
                TeamName = rr.Constructor.TeamName,
                rr.Points,
                rr.SprintPoints,
                rr.FinishPosition
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return NotFound($"No race results found for season {season}");
        }

        var ranked = rows
            .GroupBy(r => new { r.DriverId, r.FirstName, r.LastName, r.Code, r.Nationality })
            .Select(g =>
            {
                var dto = new DriverStandingDto
                {
                    DriverId = g.Key.DriverId,
                    FirstName = g.Key.FirstName,
                    LastName = g.Key.LastName,
                    Code = g.Key.Code,
                    Nationality = g.Key.Nationality,
                    // The driver's most recent team this season (handles mid-season moves).
                    TeamName = g.OrderByDescending(r => r.Round).First().TeamName,
                    TotalPoints = g.Sum(r => r.Points + r.SprintPoints)
                };
                return new Ranked(dto.TotalPoints, CountFinishes(g.Select(r => r.FinishPosition)), dto);
            })
            .ToList();

        ranked.Sort(CompareRanked);

        var standings = new List<DriverStandingDto>(ranked.Count);
        for (int i = 0; i < ranked.Count; i++)
        {
            var dto = (DriverStandingDto)ranked[i].Payload;
            dto.Position = i + 1;
            standings.Add(dto);
        }

        return Ok(standings);
    }

    [HttpGet("constructors/{season}")]
    public async Task<ActionResult<IEnumerable<ConstructorStandingDto>>> GetConstructorStandings(int season)
    {
        var rows = await _context.RaceResults
            .Where(rr => rr.Race.Season == season)
            .Select(rr => new
            {
                rr.ConstructorId,
                rr.Constructor.TeamName,
                rr.Constructor.Nationality,
                rr.Points,
                rr.SprintPoints,
                rr.FinishPosition
            })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return NotFound($"No race results found for season {season}");
        }

        var ranked = rows
            .GroupBy(r => new { r.ConstructorId, r.TeamName, r.Nationality })
            .Select(g =>
            {
                var dto = new ConstructorStandingDto
                {
                    ConstructorId = g.Key.ConstructorId,
                    TeamName = g.Key.TeamName,
                    Nationality = g.Key.Nationality,
                    TotalPoints = g.Sum(r => r.Points + r.SprintPoints)
                };
                return new Ranked(dto.TotalPoints, CountFinishes(g.Select(r => r.FinishPosition)), dto);
            })
            .ToList();

        ranked.Sort(CompareRanked);

        var standings = new List<ConstructorStandingDto>(ranked.Count);
        for (int i = 0; i < ranked.Count; i++)
        {
            var dto = (ConstructorStandingDto)ranked[i].Payload;
            dto.Position = i + 1;
            standings.Add(dto);
        }

        return Ok(standings);
    }

    // A standings entry plus the data needed to order it: total points and a countback
    // vector (index 0 = number of 1st places, index 1 = 2nd places, ...).
    private sealed record Ranked(decimal TotalPoints, int[] Finishes, object Payload);

    /// <summary>
    /// Official championship order: most points first; ties broken by countback —
    /// whoever has more wins, then more 2nd places, then 3rds, and so on (FIA rules).
    /// </summary>
    private static int CompareRanked(Ranked a, Ranked b)
    {
        var byPoints = b.TotalPoints.CompareTo(a.TotalPoints);
        if (byPoints != 0)
        {
            return byPoints;
        }

        var length = Math.Max(a.Finishes.Length, b.Finishes.Length);
        for (int i = 0; i < length; i++)
        {
            var countA = i < a.Finishes.Length ? a.Finishes[i] : 0;
            var countB = i < b.Finishes.Length ? b.Finishes[i] : 0;
            if (countA != countB)
            {
                return countB.CompareTo(countA); // more of the better position ranks higher
            }
        }

        return 0;
    }

    /// <summary>Counts finishes per position into a vector (index 0 = P1, index 1 = P2, ...).</summary>
    private static int[] CountFinishes(IEnumerable<int?> finishPositions)
    {
        var counts = new Dictionary<int, int>();
        var max = 0;
        foreach (var position in finishPositions)
        {
            if (position is int p && p >= 1)
            {
                counts[p] = counts.GetValueOrDefault(p) + 1;
                if (p > max)
                {
                    max = p;
                }
            }
        }

        var vector = new int[max];
        foreach (var (position, count) in counts)
        {
            vector[position - 1] = count;
        }
        return vector;
    }
}
