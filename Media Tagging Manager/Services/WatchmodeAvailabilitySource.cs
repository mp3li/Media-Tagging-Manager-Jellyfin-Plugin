using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Uses Watchmode's title details and availability endpoints for optional fallback coverage.</summary>
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

        // Watchmode documents an IMDb-ID details request as two credits and the
        // appended streaming-source data as one additional credit.
        if (!_quota.TryReserve(3, out var quotaReason))
        {
            return new SourceLookupResult(Name, [], quotaReason);
        }

        var requestedRegion = Uri.EscapeDataString(string.Join(',', GetRegions(configuration)));
        var uri = $"https://api.watchmode.com/v1/title/{Uri.EscapeDataString(ids.Imdb)}/details/?append_to_response=sources&regions={requestedRegion}";
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
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new SourceLookupResult(Name, tags);
        }

        if (document.RootElement.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
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

                var isTvNetworkApp = source.TryGetProperty("type", out var type)
                    && string.Equals(type.GetString(), "tve", StringComparison.OrdinalIgnoreCase);
                tags.Add(new SourceTag(TagKind.Provider, name.GetString()!, Name, isTvNetworkApp));
            }
        }

        if (document.RootElement.TryGetProperty("network_names", out var networks) && networks.ValueKind == JsonValueKind.Array)
        {
            foreach (var network in networks.EnumerateArray())
            {
                if (network.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(network.GetString()))
                {
                    tags.Add(new SourceTag(TagKind.Network, network.GetString()!, Name));
                }
            }
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
        var providerLogos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var networkTmdbIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var regions = Uri.EscapeDataString(string.Join(',', GetRegions(configuration)));
            var providerNote = await AddCatalogNamesAsync($"https://api.watchmode.com/v1/sources/?regions={regions}", "name", providers, providerLogos, null, configuration.WatchmodeApiKey, cancellationToken).ConfigureAwait(false);
            var networkNote = await AddCatalogNamesAsync("https://api.watchmode.com/v1/networks/", "name", networks, null, networkTmdbIds, configuration.WatchmodeApiKey, cancellationToken, GetRegions(configuration)).ConfigureAwait(false);
            var note = string.Join(" ", new[] { providerNote, networkNote }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                string.IsNullOrWhiteSpace(note) ? null : note,
                providerLogos,
                networkTmdbIds);
        }
        catch (HttpRequestException exception)
        {
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                $"Watchmode reference catalogs could not be loaded: {exception.Message}",
                providerLogos,
                networkTmdbIds);
        }
        catch (JsonException exception)
        {
            return new SourceCatalogResult(
                providers.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                networks.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                $"Watchmode reference catalogs returned unexpected data: {exception.Message}",
                providerLogos,
                networkTmdbIds);
        }

    }

    private async Task<string?> AddCatalogNamesAsync(
        string uri,
        string propertyName,
        ISet<string> names,
        IDictionary<string, string>? logos,
        IDictionary<string, int>? networkTmdbIds,
        string apiKey,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? allowedOriginCountries = null)
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
            if (allowedOriginCountries is not null
                && (!item.TryGetProperty("origin_country", out var originCountry)
                    || string.IsNullOrWhiteSpace(originCountry.GetString())
                    || !allowedOriginCountries.Contains(originCountry.GetString()!, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (item.TryGetProperty(propertyName, out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
            {
                var sourceName = name.GetString()!.Trim();
                names.Add(sourceName);
                if (logos is not null
                    && item.TryGetProperty("logo_100px", out var logo)
                    && !string.IsNullOrWhiteSpace(logo.GetString()))
                {
                    logos.TryAdd(sourceName, logo.GetString()!);
                }

                if (networkTmdbIds is not null
                    && item.TryGetProperty("tmdb_id", out var tmdbId)
                    && tmdbId.TryGetInt32(out var parsedTmdbId)
                    && parsedTmdbId > 0)
                {
                    networkTmdbIds.TryAdd(sourceName, parsedTmdbId);
                }
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
