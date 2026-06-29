namespace F1Dashboard.Api.Ml;

/// <summary>
/// Trains and caches the race-winner model once, in the background, right after the
/// app starts — so the first user request doesn't pay the ~30s walk-forward training
/// cost (which is especially slow on small/free hosts). Runs after the server is
/// already listening, and never throws back into the host.
/// </summary>
public sealed class ModelWarmupService : BackgroundService
{
    private readonly RaceWinnerModel _model;
    private readonly ILogger<ModelWarmupService> _logger;

    public ModelWarmupService(RaceWinnerModel model, ILogger<ModelWarmupService> logger)
    {
        _model = model;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so warm-up never blocks the port bind / health check.
        await Task.Yield();

        try
        {
            var started = DateTimeOffset.UtcNow;
            _logger.LogInformation("Pre-warming race-winner model…");
            await _model.WarmUpAsync();
            _logger.LogInformation(
                "Race-winner model warm in {Seconds:F1}s.",
                (DateTimeOffset.UtcNow - started).TotalSeconds);
        }
        catch (Exception ex)
        {
            // Warm-up is best-effort; the model still trains lazily on first request.
            _logger.LogWarning(ex, "Model pre-warm failed; will train lazily on first request.");
        }
    }
}
