using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.Models;

namespace F1Dashboard.Api.Import;

/// <summary>
/// Pulls real F1 data from the public Jolpica/Ergast API and rebuilds the
/// database tables the dashboard reads from (circuits, constructors, drivers,
/// races, race_results). Replaces the static seed; safe to re-run.
/// </summary>
public class F1DataImporter
{
    private const string BaseUrl = "https://api.jolpi.ca/ergast/f1";
    private const int PageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly F1DbContext _context;
    private readonly ILogger<F1DataImporter> _logger;

    public F1DataImporter(HttpClient http, F1DbContext context, ILogger<F1DataImporter> logger)
    {
        _http = http;
        _context = context;
        _logger = logger;
    }

    public record ImportSummary(
        IReadOnlyList<int> Seasons,
        int Circuits,
        int Constructors,
        int Drivers,
        int Races,
        int RaceResults);

    public async Task<ImportSummary> ImportSeasonsAsync(
        IEnumerable<int> seasons,
        CancellationToken ct = default)
    {
        var seasonList = seasons.Distinct().OrderBy(s => s).ToList();

        // Natural-key maps so a driver/constructor/circuit shared across seasons
        // becomes a single row. Ergast string ids are the keys.
        var circuits = new Dictionary<string, Circuit>();
        var constructors = new Dictionary<string, Constructor>();
        var drivers = new Dictionary<string, Driver>();
        var races = new Dictionary<string, Race>();
        var results = new List<RaceResult>();

        // Side tables for computing gap-to-winner once all pages are in.
        var resultMillis = new List<(RaceResult Result, long Millis)>();
        var winnerMillis = new Dictionary<string, long>();

        foreach (var season in seasonList)
        {
            await ImportSeasonAsync(
                season, circuits, constructors, drivers, races, results,
                resultMillis, winnerMillis, ct);
        }

        // Second pass: gap = (this total time - winner total time) in seconds.
        foreach (var (result, millis) in resultMillis)
        {
            var raceKey = RaceKey(result.Race.Season, result.Race.Round);
            if (winnerMillis.TryGetValue(raceKey, out var winner))
            {
                result.GapToWinnerSeconds = Math.Round((millis - winner) / 1000m, 3);
            }
        }

        await ReplaceDataAsync(
            circuits.Values, constructors.Values, drivers.Values,
            races.Values, results, ct);

        var summary = new ImportSummary(
            seasonList, circuits.Count, constructors.Count, drivers.Count,
            races.Count, results.Count);
        _logger.LogInformation(
            "Imported F1 data for seasons {Seasons}: {Races} races, {Results} results",
            string.Join(", ", seasonList), summary.Races, summary.RaceResults);
        return summary;
    }

    private async Task ImportSeasonAsync(
        int season,
        Dictionary<string, Circuit> circuits,
        Dictionary<string, Constructor> constructors,
        Dictionary<string, Driver> drivers,
        Dictionary<string, Race> races,
        List<RaceResult> results,
        List<(RaceResult, long)> resultMillis,
        Dictionary<string, long> winnerMillis,
        CancellationToken ct)
    {
        var offset = 0;
        int total;
        do
        {
            var url = $"{BaseUrl}/{season}/results.json?limit={PageSize}&offset={offset}";
            var response = await _http.GetFromJsonAsync<ErgastResponse>(url, JsonOptions, ct)
                ?? throw new InvalidOperationException($"Empty response from {url}");
            total = ParseInt(response.MRData.Total);

            foreach (var race in response.MRData.RaceTable.Races)
            {
                var circuit = GetOrAdd(circuits, race.Circuit.CircuitId, () => new Circuit
                {
                    CircuitName = race.Circuit.CircuitName,
                    Country = race.Circuit.Location.Country,
                    Locality = race.Circuit.Location.Locality
                });

                var raceKey = RaceKey(ParseInt(race.Season), ParseInt(race.Round));
                var raceEntity = GetOrAdd(races, raceKey, () => new Race
                {
                    Season = ParseInt(race.Season),
                    Round = ParseInt(race.Round),
                    Name = race.RaceName,
                    Date = ParseDate(race.Date),
                    Circuit = circuit
                });

                foreach (var r in race.Results)
                {
                    var constructor = GetOrAdd(constructors, r.Constructor.ConstructorId, () => new Constructor
                    {
                        TeamName = r.Constructor.Name,
                        Nationality = r.Constructor.Nationality
                    });

                    var driver = GetOrAdd(drivers, r.Driver.DriverId, () => new Driver
                    {
                        FirstName = r.Driver.GivenName,
                        LastName = r.Driver.FamilyName,
                        DateOfBirth = ParseDate(r.Driver.DateOfBirth),
                        // Ergast 3-letter code; fall back to the id for older entries.
                        Code = string.IsNullOrWhiteSpace(r.Driver.Code) ? r.Driver.DriverId : r.Driver.Code,
                        Nationality = r.Driver.Nationality
                    });

                    var result = new RaceResult
                    {
                        Race = raceEntity,
                        Driver = driver,
                        Constructor = constructor,
                        GridPosition = ParseInt(r.Grid),
                        FinishPosition = ParseIntOrNull(r.Position),
                        Points = ParseDecimal(r.Points),
                        Laps = ParseInt(r.Laps),
                        Status = r.Status,
                        FastestLapNumber = r.FastestLap is null ? null : ParseIntOrNull(r.FastestLap.Lap),
                        FastestLapTime = r.FastestLap is null ? null : ParseLapTimeSeconds(r.FastestLap.Time.Time)
                    };
                    results.Add(result);

                    var millis = ParseLongOrNull(r.Time?.Millis);
                    if (millis is not null)
                    {
                        resultMillis.Add((result, millis.Value));
                        if (result.FinishPosition == 1)
                        {
                            winnerMillis[raceKey] = millis.Value;
                        }
                    }
                }
            }

            offset += PageSize;
            await Task.Delay(250, ct); // be polite to the public API
        } while (offset < total);
    }

    private async Task ReplaceDataAsync(
        IEnumerable<Circuit> circuits,
        IEnumerable<Constructor> constructors,
        IEnumerable<Driver> drivers,
        IEnumerable<Race> races,
        IEnumerable<RaceResult> results,
        CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        // Wipe in FK-safe order (children before parents).
        await _context.PitStops.ExecuteDeleteAsync(ct);
        await _context.QualifyingResults.ExecuteDeleteAsync(ct);
        await _context.RaceResults.ExecuteDeleteAsync(ct);
        await _context.Races.ExecuteDeleteAsync(ct);
        await _context.Drivers.ExecuteDeleteAsync(ct);
        await _context.Constructors.ExecuteDeleteAsync(ct);
        await _context.Circuits.ExecuteDeleteAsync(ct);

        // Add the full graph; EF inserts in dependency order and resolves FKs
        // from the navigation properties.
        _context.Circuits.AddRange(circuits);
        _context.Constructors.AddRange(constructors);
        _context.Drivers.AddRange(drivers);
        _context.Races.AddRange(races);
        _context.RaceResults.AddRange(results);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static TEntity GetOrAdd<TEntity>(
        Dictionary<string, TEntity> map, string key, Func<TEntity> factory)
    {
        if (!map.TryGetValue(key, out var entity))
        {
            entity = factory();
            map[key] = entity;
        }
        return entity;
    }

    private static string RaceKey(int season, int round) => $"{season}-{round}";

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static int? ParseIntOrNull(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static long? ParseLongOrNull(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : default;

    /// <summary>Converts a lap time like "1:32.608" or "59.123" to total seconds.</summary>
    private static decimal? ParseLapTimeSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(':');
        try
        {
            if (parts.Length == 2)
            {
                var minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
                var seconds = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                return minutes * 60 + seconds;
            }
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
