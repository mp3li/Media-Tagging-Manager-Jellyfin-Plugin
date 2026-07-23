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
        if (!_quota.TryReserve(2, out var quotaReason))
        {
            return new SourceLookupResult(Name, [], quotaReason);
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

    /// <summary>Gets Watchmode's complete provider and TV-network reference catalogs without matching any Jellyfin media item.</summary>
    public async Task<SourceCatalogResult> GetReferenceCatalogAsync(CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey))
        {
            return new SourceCatalogResult([], [], "Save a Watchmode API Key to load its complete provider and TV-network catalogs before scanning.");
        }

        var providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var networks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var regions = Uri.EscapeDataString(string.Join(',', GetRegions(configuration)));
            var providerNote = await AddCatalogNamesAsync($"https://api.watchmode.com/v1/sources/?regions={regions}", "name", providers, configuration.WatchmodeApiKey, cancellationToken).ConfigureAwait(false);
            var networkNote = await AddCatalogNamesAsync("https://api.watchmode.com/v1/networks/", "name", networks, configuration.WatchmodeApiKey, cancellationToken).ConfigureAwait(false);
            var note = string.Join(" ", new[] { providerNote, networkNote }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                string.IsNullOrWhiteSpace(note) ? null : note);
        }
        catch (HttpRequestException exception)
        {
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                $"Watchmode reference catalogs could not be loaded: {exception.Message}");
        }
        catch (JsonException exception)
        {
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                $"Watchmode reference catalogs returned unexpected data: {exception.Message}");
        }

    }

    private async Task<string?> AddCatalogNamesAsync(string uri, string propertyName, ISet<string> names, string apiKey, CancellationToken cancellationToken)
    {
        if (!_quota.TryReserve(1, out var quotaReason))
        {
            return quotaReason;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-API-Key", apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _quota.RecordServerUsage(TryParseHeader(response, "X-Account-Quota-Used"));
        if (!response.IsSuccessStatusCode)
        {
            return $"Watchmode reference catalog returned HTTP {(int)response.StatusCode}.";
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return "Watchmode reference catalog returned unexpected data.";
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                names.Add(name.GetString()!.Trim());
            }
        }

        return null;
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
