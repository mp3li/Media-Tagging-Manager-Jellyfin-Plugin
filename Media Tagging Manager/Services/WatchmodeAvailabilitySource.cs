using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Uses Watchmode's title-source endpoint for optional additional availability coverage.</summary>
public sealed class WatchmodeAvailabilitySource : IAvailabilitySource
{
    private readonly HttpClient _httpClient;
    private readonly WatchmodeQuotaTracker _quota;

    /// <summary>Initializes a new instance of the <see cref="WatchmodeAvailabilitySource"/> class.</summary>
    public WatchmodeAvailabilitySource(HttpClient httpClient, WatchmodeQuotaTracker quota)
    {
        _httpClient = httpClient;
        _quota = quota;
    }

    /// <inheritdoc />
    public string Name => "Watchmode";

    /// <inheritdoc />
    public async Task<SourceLookupResult> LookupAsync(ExternalIds ids, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey))
        {
            return new SourceLookupResult(Name, []);
        }

        if (string.IsNullOrWhiteSpace(ids.Imdb))
        {
            return new SourceLookupResult(Name, [], "The item has no IMDb ID.");
        }

        // Watchmode documents IMDb-ID title-source requests as costing two credits.
        if (!_quota.TryReserve(2))
        {
            return new SourceLookupResult(Name, [], "The configured Watchmode monthly limit has been reached.");
        }

        var requestedRegion = Uri.EscapeDataString(string.Join(',', GetRegions(configuration)));
        var uri = $"https://api.watchmode.com/v1/title/{Uri.EscapeDataString(ids.Imdb)}/sources/?regions={requestedRegion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-API-Key", configuration.WatchmodeApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _quota.RecordServerUsage(TryParseHeader(response, "X-Account-Quota-Used"));
        if (!response.IsSuccessStatusCode)
        {
            return new SourceLookupResult(Name, [], $"HTTP {(int)response.StatusCode}");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var tags = new List<SourceTag>();
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return new SourceLookupResult(Name, tags);
        }

        foreach (var source in document.RootElement.EnumerateArray())
        {
            if (!source.TryGetProperty("name", out var name) || string.IsNullOrWhiteSpace(name.GetString()))
            {
                continue;
            }

            if (source.TryGetProperty("region", out var region)
                && !GetRegions(configuration).Contains(region.GetString() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            tags.Add(new SourceTag(TagKind.Provider, name.GetString()!, Name));
        }

        return new SourceLookupResult(Name, tags);
    }

    private static int? TryParseHeader(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values) && int.TryParse(values.FirstOrDefault(), out var parsed) ? parsed : null;

    private static IReadOnlyCollection<string> GetRegions(Configuration.PluginConfiguration configuration)
    {
        var configured = configuration.Regions
            .Where(region => !string.IsNullOrWhiteSpace(region))
            .Select(region => region.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        return configured.Length > 0 ? configured : [configuration.Region.Trim().ToUpperInvariant()];
    }
}
