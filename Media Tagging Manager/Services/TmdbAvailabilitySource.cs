using System.Text.Json;
using System.Net.Http.Headers;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Uses TMDb's official watch-provider and TV-network metadata endpoints.</summary>
public sealed class TmdbAvailabilitySource : IAvailabilitySource
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="TmdbAvailabilitySource"/> class.</summary>
    public TmdbAvailabilitySource(HttpClient httpClient) => _httpClient = httpClient;

    /// <inheritdoc />
    public string Name => "TMDb";

    /// <summary>Gets TMDb's official list of regions with watch-provider data.</summary>
    public async Task<IReadOnlyCollection<AvailabilityRegionDto>> GetAvailableRegionsAsync(CancellationToken cancellationToken)
    {
        var token = Plugin.Instance?.Configuration.TmdbApiKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        using var request = CreateRequest("https://api.themoviedb.org/3/watch/providers/regions?language=en-US", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results.EnumerateArray()
            .Select(region => new AvailabilityRegionDto(
                region.TryGetProperty("iso_3166_1", out var code) ? code.GetString() ?? string.Empty : string.Empty,
                region.TryGetProperty("english_name", out var name) ? name.GetString() ?? string.Empty : string.Empty))
            .Where(region => region.Code.Length == 2 && !string.IsNullOrWhiteSpace(region.Name))
            .OrderBy(region => region.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<SourceLookupResult> LookupAsync(ExternalIds ids, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            return new SourceLookupResult(Name, []);
        }

        if (string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            return new SourceLookupResult(Name, [], "The item has no TMDb ID.");
        }

        var tags = new List<SourceTag>();
        var type = ids.MediaType == "Series" ? "tv" : "movie";
        var id = Uri.EscapeDataString(ids.Tmdb);
        var providersUri = $"https://api.themoviedb.org/3/{type}/{id}/watch/providers";
        string? issue = null;

        using var providersRequest = CreateRequest(providersUri, configuration.TmdbApiKey);
        using var providersResponse = await _httpClient.SendAsync(providersRequest, cancellationToken).ConfigureAwait(false);
        if (providersResponse.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(await providersResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            if (document.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var selectedRegion in GetRegions(configuration))
                {
                    if (!results.TryGetProperty(selectedRegion, out var region))
                    {
                        continue;
                    }

                    foreach (var groupName in new[] { "flatrate", "free", "ads", "rent", "buy" })
                    {
                        if (!region.TryGetProperty(groupName, out var providers) || providers.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var provider in providers.EnumerateArray())
                        {
                            if (provider.TryGetProperty("provider_name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                            {
                                tags.Add(new SourceTag(TagKind.Provider, name.GetString()!, Name));
                            }
                        }
                    }
                }
            }
        }
        else
        {
            issue = $"Watch-provider lookup returned HTTP {(int)providersResponse.StatusCode}";
        }

        // TMDb calls television broadcasters/networks "networks". Movies intentionally receive no guessed network.
        if (type == "tv")
        {
            var detailsUri = $"https://api.themoviedb.org/3/tv/{id}";
            using var detailsRequest = CreateRequest(detailsUri, configuration.TmdbApiKey);
            using var detailsResponse = await _httpClient.SendAsync(detailsRequest, cancellationToken).ConfigureAwait(false);
            if (detailsResponse.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await detailsResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var network in networks.EnumerateArray())
                    {
                        if (network.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        {
                            tags.Add(new SourceTag(TagKind.Network, name.GetString()!, Name));
                        }
                    }
                }
            }
            else
            {
                issue ??= $"TV-network lookup returned HTTP {(int)detailsResponse.StatusCode}";
            }
        }

        return new SourceLookupResult(Name, tags, issue);
    }

    private static HttpRequestMessage CreateRequest(string uri, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

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
