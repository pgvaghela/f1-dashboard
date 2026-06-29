using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using F1Dashboard.Api.Data;
using Npgsql;

namespace F1Dashboard.Api.Ml;

/// <summary>
/// Trains and serves an ML.NET race-winner predictor. Registered as a singleton.
///
/// Completed races are scored <b>walk-forward (causal)</b>: each is predicted by a
/// FastTree model trained only on races that happened strictly before it, which avoids
/// the in-sample leakage that would otherwise make the model look artificially perfect.
///
/// Upcoming races have no qualifying yet, so grid position is unknown. They are scored by
/// a separate model trained <b>without</b> the grid/pole features, over the current
/// driver lineup — a deliberately less confident, form-driven estimate.
///
/// Every race's prediction is computed once on first use and cached. Thread-safe.
/// </summary>
public class RaceWinnerModel
{
    /// <summary>A finishing position to stand in for a DNF / unclassified result.</summary>
    private const float DnfFinish = 20f;

    /// <summary>Neutral mid-grid default when a driver/team has no prior history.</summary>
    private const float NeutralFinish = 11f;

    /// <summary>Minimum prior examples before we trust a trained model over the grid-based fallback.</summary>
    private const int MinTrainingRows = 150;

    /// <summary>
    /// Recency half-life in days for training weights. A race this old counts half as much
    /// as today's; ~2 half-lives back counts a quarter. Tuned so the current season dominates.
    /// </summary>
    private const double RecencyHalfLifeDays = 210.0;

    /// <summary>Driver win-rate is measured over this many most-recent prior races (not all-time).</summary>
    private const int WinRateWindow = 20;

    /// <summary>Circuit history older than this many years is ignored (cars change too much).</summary>
    private const int CircuitHistoryYears = 2;

    /// <summary>Fallback qualifying gap-to-pole (%) when a driver has no qualifying time for a race.</summary>
    private const float NeutralGapPercent = 1.5f;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KalshiOddsService _kalshi;
    private readonly ILogger<RaceWinnerModel> _logger;
    private readonly MLContext _ml = new(seed: 1);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _ready;
    private double _accuracy;

    // The next race to be run (earliest-dated race without results); the one Kalshi covers.
    private int? _nextRaceId;

    // Model trained without grid/pole, used to score races that haven't run yet.
    private ITransformer? _futureModel;

    // In-memory snapshot, indexed for fast feature lookups.
    private List<ResultRow> _results = new();
    private Dictionary<int, List<ResultRow>> _byDriver = new();
    private Dictionary<int, List<ResultRow>> _byConstructor = new();
    private Dictionary<int, RaceInfo> _raceById = new();
    private Dictionary<int, int> _raceCircuit = new();

    // Qualifying pace as a percentage gap to pole, per (race, driver).
    private Dictionary<(int RaceId, int DriverId), float> _qualGap = new();

    // The most recent result date in the dataset; the reference point for recency weighting.
    private DateOnly _maxResultDate = DateOnly.FromDateTime(DateTime.UtcNow);

    // Precomputed prediction per race id (completed + upcoming).
    private Dictionary<int, RacePredictionResult> _predictionsByRace = new();

    public RaceWinnerModel(IServiceScopeFactory scopeFactory, KalshiOddsService kalshi, ILogger<RaceWinnerModel> logger)
    {
        _scopeFactory = scopeFactory;
        _kalshi = kalshi;
        _logger = logger;
    }

    /// <summary>A single imported race result, flattened for feature building.</summary>
    private record ResultRow(
        int RaceId, int Season, int Round, DateOnly Date, int CircuitId,
        int DriverId, string DriverFirst, string DriverLast, string DriverCode,
        int ConstructorId, string ConstructorName, int Grid, int? Finish, decimal Points);

    /// <summary>A driver in the current lineup, used to build the field for an upcoming race.</summary>
    private record FieldEntry(
        int DriverId, string DriverFirst, string DriverLast, string DriverCode,
        int ConstructorId, string ConstructorName);
    private record QualifyingGapRow(int RaceId, int DriverId, decimal? Q1Time, decimal? Q2Time, decimal? Q3Time);

    public record RaceInfo(
        int RaceId, int Season, int Round, DateOnly Date, string Name, string Circuit, bool HasResult);

    public record DriverPrediction(
        int DriverId, string DriverName, string Code, string ConstructorName,
        int? Grid, double WinProbability, bool IsPredictedWinner,
        int? ActualFinish, bool IsActualWinner);

    public record RacePredictionResult(
        RaceInfo Race, double ModelAccuracy, string Basis, bool IsUpcoming,
        IReadOnlyList<DriverPrediction> Drivers);

    public record TrainingSummary(int Examples, int Races, double ModelAccuracy);

    /// <summary>Races (newest round first) the model can predict for a season, completed or upcoming.</summary>
    public async Task<IReadOnlyList<RaceInfo>> GetRacesAsync(int season)
    {
        await EnsureReadyAsync();
        return _raceById.Values
            .Where(r => r.Season == season)
            .OrderByDescending(r => r.Round)
            .ToList();
    }

    public async Task<RacePredictionResult?> PredictRaceAsync(int raceId)
    {
        await EnsureReadyAsync();
        var result = _predictionsByRace.GetValueOrDefault(raceId);
        if (result is null)
        {
            return null;
        }

        // Stamp the (loop-wide) accuracy, which is only known after every race is built.
        result = result with { ModelAccuracy = _accuracy };

        // For the next race, blend in live prediction-market odds (current-form, news-aware).
        if (result.IsUpcoming && raceId == _nextRaceId)
        {
            result = await BlendWithMarketAsync(result);
        }

        return result;
    }

    /// <summary>Forces a fresh reload of the data and retraining, returning a summary.</summary>
    public async Task<TrainingSummary> RetrainAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await BuildAsync();
        }
        finally
        {
            _gate.Release();
        }

        return new TrainingSummary(_results.Count, _raceById.Count, _accuracy);
    }

    private async Task EnsureReadyAsync()
    {
        if (_ready)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_ready)
            {
                return;
            }

            await BuildAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task BuildAsync()
    {
        await LoadSnapshotAsync();
        BuildWalkForwardPredictions();
        BuildUpcomingPredictions();

        // The next race to run = earliest-dated race that has no results yet.
        _nextRaceId = _raceById.Values
            .Where(r => !r.HasResult)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Round)
            .Select(r => (int?)r.RaceId)
            .FirstOrDefault();

        _ready = true;
    }

    private async Task LoadSnapshotAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<F1DbContext>();

        _results = await db.RaceResults
            .Select(rr => new ResultRow(
                rr.RaceId, rr.Race.Season, rr.Race.Round, rr.Race.Date, rr.Race.CircuitId,
                rr.DriverId, rr.Driver.FirstName, rr.Driver.LastName, rr.Driver.Code,
                rr.ConstructorId, rr.Constructor.TeamName,
                rr.GridPosition, rr.FinishPosition, rr.Points))
            .ToListAsync();

        // Load races from the schedule table so upcoming (result-less) rounds are included.
        var raceRows = await db.Races
            .Select(r => new { r.Id, r.Season, r.Round, r.Date, r.CircuitId, r.Name, Circuit = r.Circuit.CircuitName })
            .ToListAsync();

        _maxResultDate = _results.Count == 0
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : _results.Max(r => r.Date);

        var racesWithResults = _results.Select(r => r.RaceId).ToHashSet();
        _raceById = raceRows.ToDictionary(
            r => r.Id,
            r => new RaceInfo(r.Id, r.Season, r.Round, r.Date, r.Name, r.Circuit, racesWithResults.Contains(r.Id)));
        _raceCircuit = raceRows.ToDictionary(r => r.Id, r => r.CircuitId);

        _byDriver = _results
            .GroupBy(r => r.DriverId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Date).ToList());

        _byConstructor = _results
            .GroupBy(r => r.ConstructorId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Date).ToList());

        await LoadQualifyingGapsAsync(db);
    }

    /// <summary>
    /// Computes each driver's qualifying pace as a percentage gap to the session's fastest
    /// lap, per race. A driver's time is their best of Q3/Q2/Q1; pole is the field minimum.
    /// </summary>
    private async Task LoadQualifyingGapsAsync(F1DbContext db)
    {
        List<QualifyingGapRow> qualRows;
        try
        {
            qualRows = await db.QualifyingResults
                .Select(q => new QualifyingGapRow(q.RaceId, q.DriverId, q.Q1Time, q.Q2Time, q.Q3Time))
                .ToListAsync();
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            // Older hosted DBs can miss this table or use legacy column names (q1time vs q1_time).
            // In either case, fall back to neutral qualifying gaps instead of failing requests.
            _logger.LogWarning(
                "qualifying_results schema is incompatible ({SqlState}); using neutral qualifying gap fallback for predictions.",
                ex.SqlState);
            _qualGap = new Dictionary<(int, int), float>();
            return;
        }

        _qualGap = new Dictionary<(int, int), float>();
        foreach (var byRace in qualRows.GroupBy(q => q.RaceId))
        {
            var best = byRace
                .Select(q => new { q.DriverId, Time = BestQualTime(q.Q1Time, q.Q2Time, q.Q3Time) })
                .Where(x => x.Time is > 0m)
                .ToList();
            if (best.Count == 0)
            {
                continue;
            }

            var pole = best.Min(x => x.Time!.Value);
            foreach (var x in best)
            {
                _qualGap[(byRace.Key, x.DriverId)] = (float)((x.Time!.Value - pole) / pole * 100m);
            }
        }
    }

    private static decimal? BestQualTime(decimal? q1, decimal? q2, decimal? q3)
    {
        var times = new[] { q1, q2, q3 }.Where(t => t is > 0m).Select(t => t!.Value).ToList();
        return times.Count == 0 ? null : times.Min();
    }

    /// <summary>
    /// For every completed race, train a FastTree model on all results dated strictly
    /// earlier (grid included) and score that race's field. Also trains the no-grid model
    /// used for upcoming races. Computes walk-forward top-1 accuracy.
    /// </summary>
    private void BuildWalkForwardPredictions()
    {
        _predictionsByRace = new Dictionary<int, RacePredictionResult>();
        _futureModel = null;

        if (_results.Count == 0)
        {
            _accuracy = 0;
            _logger.LogWarning("RaceWinnerModel: no results found; nothing to predict.");
            return;
        }

        // Compute each result's leak-free features once (each row only sees its own past).
        // Each row is also given a recency weight relative to the latest race in the dataset.
        var featureByResult = _results.ToDictionary(
            r => r,
            r =>
            {
                var features = BuildFeatures(r.RaceId, r.DriverId, r.ConstructorId, r.CircuitId, r.Season, r.Date, r.Grid, r.Finish == 1);
                features.Weight = RecencyWeight(r.Date);
                return features;
            });

        // The no-grid model learns from every completed result (grid/pole withheld).
        if (featureByResult.Count >= MinTrainingRows)
        {
            _futureModel = Train(featureByResult.Values.ToList(), includeGrid: false);
        }

        var completedRaces = _raceById.Values
            .Where(r => r.HasResult)
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Round)
            .ToList();

        int hits = 0, scored = 0;
        foreach (var race in completedRaces)
        {
            var field = _results.Where(r => r.RaceId == race.RaceId).ToList();
            var trainingRows = _results
                .Where(r => r.Date < race.Date)
                .Select(r => featureByResult[r])
                .ToList();

            var hasWinner = field.Any(f => f.Finish == 1);
            var useModel = trainingRows.Count >= MinTrainingRows && trainingRows.Any(t => t.Won);

            double[] probabilities;
            string basis;
            if (useModel)
            {
                var model = Train(trainingRows, includeGrid: true);
                probabilities = ScoreField(model, field.Select(f => featureByResult[f]));
                basis = "model";
            }
            else
            {
                // Cold start (earliest races): fall back to a transparent grid-based prior.
                probabilities = GridPrior(field.Select(f => f.Grid));
                basis = "grid-prior";
            }

            var drivers = field
                .Select((e, i) => new DriverPrediction(
                    DriverId: e.DriverId,
                    DriverName: $"{e.DriverFirst} {e.DriverLast}",
                    Code: e.DriverCode,
                    ConstructorName: e.ConstructorName,
                    Grid: e.Grid,
                    WinProbability: 0,
                    IsPredictedWinner: false,
                    ActualFinish: e.Finish,
                    IsActualWinner: e.Finish == 1))
                .ToList();

            var result = Rank(race, drivers, probabilities, basis, isUpcoming: false);
            _predictionsByRace[race.RaceId] = result;

            if (useModel && hasWinner)
            {
                scored++;
                if (result.Drivers[0].IsActualWinner)
                {
                    hits++;
                }
            }
        }

        _accuracy = scored == 0 ? 0 : (double)hits / scored;
        _logger.LogInformation(
            "RaceWinnerModel built {Races} completed-race predictions; walk-forward top-1 accuracy {Accuracy:P0} over {Scored} model-scored races.",
            _predictionsByRace.Count, _accuracy, scored);
    }

    /// <summary>Scores every race that hasn't run yet using the no-grid model and current lineup.</summary>
    private void BuildUpcomingPredictions()
    {
        if (_futureModel is null)
        {
            return;
        }

        var upcoming = _raceById.Values.Where(r => !r.HasResult).ToList();
        foreach (var race in upcoming)
        {
            var field = CurrentField(race.Season);
            if (field.Count == 0)
            {
                continue;
            }

            var circuitId = _raceCircuit.GetValueOrDefault(race.RaceId);
            var features = field
                .Select(f => BuildFeatures(race.RaceId, f.DriverId, f.ConstructorId, circuitId, race.Season, race.Date, grid: 0, won: false))
                .ToList();
            var probabilities = ScoreField(_futureModel, features);

            var drivers = field
                .Select((f, i) => new DriverPrediction(
                    DriverId: f.DriverId,
                    DriverName: $"{f.DriverFirst} {f.DriverLast}",
                    Code: f.DriverCode,
                    ConstructorName: f.ConstructorName,
                    Grid: null, // qualifying hasn't happened
                    WinProbability: 0,
                    IsPredictedWinner: false,
                    ActualFinish: null,
                    IsActualWinner: false))
                .ToList();

            _predictionsByRace[race.RaceId] = Rank(race, drivers, probabilities, "upcoming", isUpcoming: true);
        }

        _logger.LogInformation(
            "RaceWinnerModel built {Count} upcoming-race predictions.",
            _raceById.Values.Count(r => !r.HasResult));
    }

    /// <summary>
    /// Blends the model's win probabilities with Kalshi's market-implied odds for the next race:
    /// <c>final = (1 − α)·model + α·market</c>, matched by driver code, then renormalized. Returns
    /// the model-only result unchanged if the market is unavailable.
    /// </summary>
    private async Task<RacePredictionResult> BlendWithMarketAsync(RacePredictionResult result)
    {
        var odds = await _kalshi.GetNextRaceOddsAsync();
        if (odds is null || odds.WinProbabilityByCode.Count == 0)
        {
            return result;
        }

        var alpha = _kalshi.BlendWeight;
        var blended = result.Drivers
            .Select(d =>
            {
                var market = odds.WinProbabilityByCode.GetValueOrDefault(d.Code.ToUpperInvariant(), 0);
                var score = (1 - alpha) * d.WinProbability + alpha * market;
                return (Driver: d, Score: score);
            })
            .ToList();

        var total = blended.Sum(x => x.Score);
        if (total <= 0)
        {
            return result;
        }

        var ranked = blended
            .Select(x => x.Driver with { WinProbability = x.Score / total, IsPredictedWinner = false })
            .OrderByDescending(d => d.WinProbability)
            .Select((d, i) => d with { IsPredictedWinner = i == 0 })
            .ToList();

        var basis = $"model {1 - alpha:P0} + Kalshi market {alpha:P0} ({odds.RaceTitle})";
        return result with { Drivers = ranked, Basis = basis };
    }

    /// <summary>The current driver lineup for a season: each driver with their most recent team.</summary>
    private List<FieldEntry> CurrentField(int season)
    {
        var entries = _results.Where(r => r.Season == season).ToList();
        if (entries.Count == 0)
        {
            // Season hasn't started — fall back to the latest season that has results.
            var latest = _results.Select(r => r.Season).DefaultIfEmpty(season).Max();
            entries = _results.Where(r => r.Season == latest).ToList();
        }

        return entries
            .GroupBy(r => r.DriverId)
            .Select(g =>
            {
                var last = g.OrderBy(r => r.Date).Last();
                return new FieldEntry(
                    last.DriverId, last.DriverFirst, last.DriverLast, last.DriverCode,
                    last.ConstructorId, last.ConstructorName);
            })
            .ToList();
    }

    /// <summary>Normalizes probabilities across the field to sum to 1, sorts, and flags the top pick.</summary>
    private static RacePredictionResult Rank(
        RaceInfo race, List<DriverPrediction> drivers, double[] probabilities,
        string basis, bool isUpcoming)
    {
        var total = probabilities.Sum();
        var maxIndex = Array.IndexOf(probabilities, probabilities.Max());

        var ranked = drivers
            .Select((d, i) => d with
            {
                WinProbability = total > 0 ? probabilities[i] / total : 0,
                IsPredictedWinner = i == maxIndex
            })
            .OrderByDescending(d => d.WinProbability)
            .ToList();

        // ModelAccuracy is stamped on read (PredictRaceAsync); it is loop-wide and not yet known here.
        return new RacePredictionResult(race, 0, basis, isUpcoming, ranked);
    }

    private ITransformer Train(IReadOnlyList<RaceFeatureRow> trainingRows, bool includeGrid)
    {
        var featureColumns = new List<string>();
        if (includeGrid)
        {
            featureColumns.Add(nameof(RaceFeatureRow.GridPosition));
            featureColumns.Add(nameof(RaceFeatureRow.IsPole));
            featureColumns.Add(nameof(RaceFeatureRow.QualifyingGapToPole));
        }
        featureColumns.Add(nameof(RaceFeatureRow.DriverRecentFinish));
        featureColumns.Add(nameof(RaceFeatureRow.DriverWinRate));
        featureColumns.Add(nameof(RaceFeatureRow.DriverSeasonPointsPerRace));
        featureColumns.Add(nameof(RaceFeatureRow.ConstructorRecentFinish));
        featureColumns.Add(nameof(RaceFeatureRow.ConstructorSeasonPointsPerRace));
        featureColumns.Add(nameof(RaceFeatureRow.DriverCircuitAvgFinish));

        var data = _ml.Data.LoadFromEnumerable(trainingRows);
        var pipeline = _ml.Transforms.Concatenate("Features", featureColumns.ToArray())
            .Append(_ml.BinaryClassification.Trainers.FastTree(
                labelColumnName: nameof(RaceFeatureRow.Won),
                featureColumnName: "Features",
                exampleWeightColumnName: nameof(RaceFeatureRow.Weight),
                numberOfLeaves: 16,
                numberOfTrees: 60,
                minimumExampleCountPerLeaf: 10,
                learningRate: 0.2));

        return pipeline.Fit(data);
    }

    private double[] ScoreField(ITransformer model, IEnumerable<RaceFeatureRow> fieldFeatures)
    {
        var scored = model.Transform(_ml.Data.LoadFromEnumerable(fieldFeatures));
        return _ml.Data
            .CreateEnumerable<ScoredRow>(scored, reuseRowObject: false)
            .Select(r => (double)r.Probability)
            .ToArray();
    }

    /// <summary>Transparent fallback for cold-start races: weight by grid position only.</summary>
    private static double[] GridPrior(IEnumerable<int> grids) =>
        grids.Select(g => Math.Exp(-((g == 0 ? 21 : g) - 1) / 3.0)).ToArray();

    /// <summary>The calibrated win probability column produced by the classifier.</summary>
    private sealed class ScoredRow
    {
        public float Probability { get; set; }
    }

    /// <summary>
    /// Builds the feature vector for one driver going into a race, using only results dated
    /// strictly before <paramref name="before"/> (no leakage). <paramref name="grid"/> is
    /// ignored by the no-grid model used for upcoming races.
    /// </summary>
    private RaceFeatureRow BuildFeatures(
        int raceId, int driverId, int constructorId, int circuitId, int season, DateOnly before, int grid, bool won)
    {
        var driverHistory = _byDriver.GetValueOrDefault(driverId);
        var constructorHistory = _byConstructor.GetValueOrDefault(constructorId);

        return new RaceFeatureRow
        {
            GridPosition = grid == 0 ? 21f : grid,
            IsPole = grid == 1 ? 1f : 0f,
            QualifyingGapToPole = _qualGap.GetValueOrDefault((raceId, driverId), NeutralGapPercent),
            DriverRecentFinish = RecentFinish(driverHistory, before),
            DriverWinRate = WinRate(driverHistory, before),
            DriverSeasonPointsPerRace = SeasonPointsPerRace(driverHistory, season, before),
            ConstructorRecentFinish = RecentFinish(constructorHistory, before),
            ConstructorSeasonPointsPerRace = SeasonPointsPerRace(constructorHistory, season, before),
            DriverCircuitAvgFinish = CircuitAvgFinish(driverHistory, circuitId, before),
            Won = won
        };
    }

    private static float FinishOf(ResultRow r) => r.Finish ?? DnfFinish;

    private static float RecentFinish(List<ResultRow>? history, DateOnly before)
    {
        if (history is null)
        {
            return NeutralFinish;
        }

        var recent = history.Where(r => r.Date < before).TakeLast(5).ToList();
        return recent.Count == 0 ? NeutralFinish : recent.Average(FinishOf);
    }

    private static float WinRate(List<ResultRow>? history, DateOnly before)
    {
        if (history is null)
        {
            return 0f;
        }

        // Rolling window: a driver's win rate over their most recent races, not their whole career,
        // so a fast car two seasons ago doesn't permanently inflate the signal.
        var prior = history.Where(r => r.Date < before).TakeLast(WinRateWindow).ToList();
        return prior.Count == 0 ? 0f : (float)prior.Count(r => r.Finish == 1) / prior.Count;
    }

    private static float SeasonPointsPerRace(List<ResultRow>? history, int season, DateOnly before)
    {
        if (history is null)
        {
            return 0f;
        }

        var prior = history.Where(r => r.Season == season && r.Date < before).ToList();
        return prior.Count == 0 ? 0f : (float)prior.Average(r => (double)r.Points);
    }

    private static float CircuitAvgFinish(List<ResultRow>? history, int circuitId, DateOnly before)
    {
        if (history is null)
        {
            return NeutralFinish;
        }

        // Only count visits within the last couple of seasons; older circuit form reflects a
        // different car and is misleading. Fall back to overall recent form when there's nothing.
        var cutoff = before.AddYears(-CircuitHistoryYears);
        var atCircuit = history
            .Where(r => r.CircuitId == circuitId && r.Date < before && r.Date >= cutoff)
            .ToList();
        return atCircuit.Count == 0 ? RecentFinish(history, before) : atCircuit.Average(FinishOf);
    }

    /// <summary>
    /// Exponential time-decay weight for a training row, relative to the latest race in the
    /// dataset. Half-life is <see cref="RecencyHalfLifeDays"/> days, so recent races dominate.
    /// </summary>
    private float RecencyWeight(DateOnly date)
    {
        var ageDays = Math.Max(0, _maxResultDate.DayNumber - date.DayNumber);
        return (float)Math.Pow(0.5, ageDays / RecencyHalfLifeDays);
    }
}
