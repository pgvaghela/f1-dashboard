using System.Globalization;
using System.Xml.Linq;

namespace F1Dashboard.Api.News;

public class HeadlinesService
{
    private static readonly (string Source, string Url)[] Feeds =
    [
        ("Formula 1", "https://www.formula1.com/en/latest/all.xml"),
        ("BBC Sport", "https://feeds.bbci.co.uk/sport/formula1/rss.xml")
    ];

    private readonly HttpClient _http;
    private readonly ILogger<HeadlinesService> _logger;

    public HeadlinesService(HttpClient http, ILogger<HeadlinesService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HeadlineItem>> GetLatestAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var items = new List<HeadlineItem>();

        foreach (var (source, url) in Feeds)
        {
            try
            {
                using var response = await _http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var xml = await response.Content.ReadAsStringAsync(ct);
                items.AddRange(ParseFeed(xml, source));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch feed {FeedUrl}", url);
            }
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Href))
            .GroupBy(item => item.Href, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.PublishedAt).First())
            .OrderByDescending(item => item.PublishedAt)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<HeadlineItem> ParseFeed(string xml, string fallbackSource)
    {
        var doc = XDocument.Parse(xml);
        var rssItems = doc.Descendants("item");
        foreach (var item in rssItems)
        {
            var title = item.Element("title")?.Value?.Trim();
            var href = item.Element("link")?.Value?.Trim();
            var source = item.Element("source")?.Value?.Trim();
            var pubDate = item.Element("pubDate")?.Value?.Trim();

            yield return new HeadlineItem(
                title ?? string.Empty,
                href ?? string.Empty,
                string.IsNullOrWhiteSpace(source) ? fallbackSource : source,
                ParseDate(pubDate));
        }
    }

    private static DateTimeOffset ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.MinValue;
    }
}

public record HeadlineItem(string Title, string Href, string Source, DateTimeOffset PublishedAt);
