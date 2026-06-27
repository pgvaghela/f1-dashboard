using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace F1Dashboard.Api.Ml;

/// <summary>
/// Fetches Kalshi prediction-market prices for the next F1 race winner and exposes them as
/// normalized implied win probabilities keyed by driver code (e.g. "VER"). This is the most
/// "current-aware" signal available — markets price in practice/qualifying pace, weather,
/// penalties and news that historical data can't see.
///
/// Read-only public market data (no auth). Degrades gracefully to <c>null</c> on any error,
/// and results are cached for a few minutes to avoid hammering the API.
/// </summary>
public class KalshiOddsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<KalshiOddsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private MarketOdds? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public KalshiOddsService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KalshiOddsService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>Normalized implied win probabilities for the next race's field, by driver code.</summary>
    public record MarketOdds(
        string EventTicker,
        string RaceTitle,
        DateTimeOffset? CloseTime,
        IReadOnlyDictionary<string, double> WinProbabilityByCode);

    private bool Enabled => _config.GetValue("Kalshi:Enabled", true);
    public double BlendWeight => Math.Clamp(_config.GetValue("Kalshi:BlendWeight", 0.5), 0, 1);
    private string BaseUrl => _config["Kalshi:BaseUrl"] ?? "https://api.elections.kalshi.com/trade-api/v2";
    private string SeriesTicker => _config["Kalshi:RaceSeriesTicker"] ?? "KXF1RACE";
    private int CacheMinutes => _config.GetValue("Kalshi:CacheMinutes", 10);

    /// <summary>
    /// Returns implied odds for the soonest-closing open F1 race-winner event (i.e. the next
    /// race), or <c>null</c> if disabled, unavailable, or no open market exists yet.
    /// </summary>
    public async Task<MarketOdds?> GetNextRaceOddsAsync(CancellationToken ct = default)
    {
        if (!Enabled)
        {
            return null;
        }

        if (IsCacheFresh())
        {
            return _cached;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (IsCacheFresh())
            {
                return _cached;
            }

            _cached = await FetchAsync(ct);
            _cachedAt = DateTimeOffset.UtcNow;
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsCacheFresh() =>
        _cached is not null && DateTimeOffset.UtcNow - _cachedAt < TimeSpan.FromMinutes(CacheMinutes);

    private async Task<MarketOdds?> FetchAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpFactory.CreateClient("kalshi");
            var url = $"{BaseUrl}/markets?series_ticker={SeriesTicker}&status=open&limit=200";
            var response = await client.GetFromJsonAsync<KalshiMarketsResponse>(url, JsonOptions, ct);

            var markets = response?.Markets;
            if (markets is null || markets.Count == 0)
            {
                _logger.LogInformation("Kalshi: no open {Series} markets.", SeriesTicker);
                return null;
            }

            // Each event is one Grand Prix; the next race is the soonest-closing one.
            var nextEvent = markets
                .Where(m => !string.IsNullOrEmpty(m.EventTicker))
                .GroupBy(m => m.EventTicker!)
                .Select(g => new { EventTicker = g.Key, Markets = g.ToList(), Close = g.Min(m => ParseTime(m.CloseTime)) })
                .OrderBy(g => g.Close ?? DateTimeOffset.MaxValue)
                .FirstOrDefault();

            if (nextEvent is null)
            {
                return null;
            }

            var raw = new Dictionary<string, double>();
            foreach (var m in nextEvent.Markets)
            {
                var code = CodeFromTicker(m.Ticker);
                var price = ImpliedPrice(m);
                if (code is not null && price > 0)
                {
                    raw[code] = price;
                }
            }

            if (raw.Count == 0)
            {
                return null;
            }

            // Market prices include the book's overround and don't sum to 1; normalize them.
            var sum = raw.Values.Sum();
            var normalized = raw.ToDictionary(kv => kv.Key, kv => kv.Value / sum);

            var title = nextEvent.Markets
                .Select(m => RaceNameFromTitle(m.Title))
                .FirstOrDefault(t => t is not null) ?? nextEvent.EventTicker;

            _logger.LogInformation(
                "Kalshi: loaded {Count} implied odds for {Event} ({Title}).",
                normalized.Count, nextEvent.EventTicker, title);

            return new MarketOdds(nextEvent.EventTicker, title!, nextEvent.Close, normalized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kalshi odds fetch failed; falling back to model-only predictions.");
            return null;
        }
    }

    /// <summary>Best estimate of the Yes price (0–1): mid of bid/ask, else last trade, else a side.</summary>
    private static double ImpliedPrice(KalshiMarket m)
    {
        var bid = ParsePrice(m.YesBidDollars);
        var ask = ParsePrice(m.YesAskDollars);
        if (bid > 0 && ask > 0)
        {
            return (bid + ask) / 2;
        }

        var last = ParsePrice(m.LastPriceDollars);
        if (last > 0)
        {
            return last;
        }

        return ask > 0 ? ask : bid;
    }

    /// <summary>The driver code is the segment after the last dash, e.g. KXF1RACE-BRIGP26-VER → VER.</summary>
    private static string? CodeFromTicker(string? ticker)
    {
        if (string.IsNullOrEmpty(ticker))
        {
            return null;
        }

        var idx = ticker.LastIndexOf('-');
        return idx < 0 || idx == ticker.Length - 1 ? null : ticker[(idx + 1)..].ToUpperInvariant();
    }

    private static string? RaceNameFromTitle(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var match = Regex.Match(title, @"at the (?<name>.+?)\??$");
        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    private static double ParsePrice(string? value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    private class KalshiMarketsResponse
    {
        [JsonPropertyName("markets")] public List<KalshiMarket>? Markets { get; set; }
        [JsonPropertyName("cursor")] public string? Cursor { get; set; }
    }

    private class KalshiMarket
    {
        [JsonPropertyName("ticker")] public string? Ticker { get; set; }
        [JsonPropertyName("event_ticker")] public string? EventTicker { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("close_time")] public string? CloseTime { get; set; }
        [JsonPropertyName("yes_bid_dollars")] public string? YesBidDollars { get; set; }
        [JsonPropertyName("yes_ask_dollars")] public string? YesAskDollars { get; set; }
        [JsonPropertyName("last_price_dollars")] public string? LastPriceDollars { get; set; }
    }
}
