using System.Text.Json;
using System.Net.Http.Headers;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Uses TMDb's official watch-provider and TV-network metadata endpoints.</summary>
public sealed class TmdbAvailabilitySource : IAvailabilitySource
{
    private readonly HttpClient _httpClient;
    private readonly TmdbRequestGate _requestGate;

    /// <summary>Initializes a new instance of the <see cref="TmdbAvailabilitySource"/> class.</summary>
    public TmdbAvailabilitySource(HttpClient httpClient, TmdbRequestGate requestGate)
    {
        _httpClient = httpClient;
        _requestGate = requestGate;
    }

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

        using var response = await SendAsync("https://api.themoviedb.org/3/watch/providers/regions?language=en-US", token, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Gets TMDb's official combined movie and TV genre catalog.</summary>
    public async Task<IReadOnlyCollection<GenreDto>> GetGenresAsync(CancellationToken cancellationToken)
    {
        var token = Plugin.Instance?.Configuration.TmdbApiKey;
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        var genres = new Dictionary<int, string>();
        foreach (var type in new[] { "movie", "tv" })
        {
            using var response = await SendAsync($"https://api.themoviedb.org/3/genre/{type}/list?language=en-US", token, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            if (!document.RootElement.TryGetProperty("genres", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var genre in values.EnumerateArray())
            {
                if (genre.TryGetProperty("id", out var id) && id.TryGetInt32(out var value)
                    && genre.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    genres[value] = name.GetString()!.Trim();
                }
            }
        }

        return genres.Select(pair => new GenreDto(pair.Key, pair.Value)).OrderBy(genre => genre.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Gets optional direct TMDb genre and keyword classifications for one title.</summary>
    public async Task<SourceLookupResult> LookupClassificationsAsync(ExternalIds ids, bool includeGenres, bool includeKeywords, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.TmdbApiKey) || string.IsNullOrWhiteSpace(ids.Tmdb) || (!includeGenres && !includeKeywords))
        {
            return new SourceLookupResult(Name, []);
        }

        var type = ids.MediaType == "Series" ? "tv" : "movie";
        var id = Uri.EscapeDataString(ids.Tmdb);
        var tags = new List<SourceTag>();
        string? issue = null;
        if (includeGenres)
        {
            using var response = await SendAsync($"https://api.themoviedb.org/3/{type}/{id}?language=en-US", configuration.TmdbApiKey, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("genres", out var genres) && genres.ValueKind == JsonValueKind.Array)
                {
                    tags.AddRange(genres.EnumerateArray()
                        .Where(genre => genre.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        .Select(genre => new SourceTag(TagKind.Genre, genre.GetProperty("name").GetString()!, Name)));
                }
            }
            else
            {
                issue = $"Genre lookup returned HTTP {(int)response.StatusCode}";
            }
        }

        if (includeKeywords)
        {
            using var response = await SendAsync($"https://api.themoviedb.org/3/{type}/{id}/keywords", configuration.TmdbApiKey, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                var property = type == "tv" ? "results" : "keywords";
                if (document.RootElement.TryGetProperty(property, out var keywords) && keywords.ValueKind == JsonValueKind.Array)
                {
                    tags.AddRange(keywords.EnumerateArray()
                        .Where(keyword => keyword.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        .Select(keyword => new SourceTag(TagKind.Keyword, keyword.GetProperty("name").GetString()!, Name)));
                }
            }
            else
            {
                issue ??= $"Keyword lookup returned HTTP {(int)response.StatusCode}";
            }
        }

        return new SourceLookupResult(Name, tags, issue);
    }

    /// <summary>Gets a direct TMDb movie collection membership without inferring membership.</summary>
    public async Task<string?> GetCollectionAsync(ExternalIds ids, CancellationToken cancellationToken)
    {
        var token = Plugin.Instance?.Configuration.TmdbApiKey;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(ids.Tmdb) || ids.MediaType == "Series")
        {
            return null;
        }

        using var response = await SendAsync($"https://api.themoviedb.org/3/movie/{Uri.EscapeDataString(ids.Tmdb)}?language=en-US", token, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("belongs_to_collection", out var collection)
            && collection.ValueKind == JsonValueKind.Object
            && collection.TryGetProperty("name", out var name)
            && !string.IsNullOrWhiteSpace(name.GetString())
            ? name.GetString()!.Trim()
            : null;
    }

    /// <summary>Gets TMDb's complete movie and TV watch-provider catalogs for the configured availability countries.</summary>
    public async Task<SourceCatalogResult> GetReferenceCatalogAsync(CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            return new SourceCatalogResult([], [], "Save a TMDb API Read Access Token to load TMDb's provider catalog.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var region in GetRegions(configuration))
            {
                foreach (var type in new[] { "movie", "tv" })
                {
                    var uri = $"https://api.themoviedb.org/3/watch/providers/{type}?language=en-US&watch_region={Uri.EscapeDataString(region)}";
                    using var response = await SendAsync(uri, configuration.TmdbApiKey, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                    if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var provider in results.EnumerateArray())
                    {
                        if (provider.TryGetProperty("provider_name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        {
                            var providerName = name.GetString()!.Trim();
                            names.Add(providerName);
                            if (provider.TryGetProperty("logo_path", out var logoPath) && !string.IsNullOrWhiteSpace(logoPath.GetString()))
                            {
                                logos.TryAdd(providerName, TmdbLogoUrl(logoPath.GetString()!));
                            }
                        }
                    }
                }
            }
        }
        catch (HttpRequestException exception)
        {
            return new SourceCatalogResult(names.ToArray(), [], $"TMDb provider catalog could not be loaded: {exception.Message}", logos);
        }
        catch (JsonException exception)
        {
            return new SourceCatalogResult(names.ToArray(), [], $"TMDb provider catalog returned unexpected data: {exception.Message}", logos);
        }

        return new SourceCatalogResult(names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(), [], null, logos);
    }

    /// <summary>Gets TMDb network-logo URLs for known network IDs without looking up any Jellyfin media item.</summary>
    public async Task<IReadOnlyCollection<SourceTag>> GetNetworkLogoTagsAsync(IReadOnlyDictionary<string, int> networkTmdbIds, CancellationToken cancellationToken)
    {
        var token = Plugin.Instance?.Configuration.TmdbApiKey;
        if (string.IsNullOrWhiteSpace(token) || networkTmdbIds.Count == 0)
        {
            return [];
        }

        var tags = new List<SourceTag>();
        foreach (var pair in networkTmdbIds.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var response = await SendAsync($"https://api.themoviedb.org/3/network/{pair.Value}", token, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("logo_path", out var logoPath) && !string.IsNullOrWhiteSpace(logoPath.GetString()))
                {
                    tags.Add(new SourceTag(TagKind.Network, pair.Key, Name, false, TmdbLogoUrl(logoPath.GetString()!)));
                }
            }
            catch (HttpRequestException)
            {
                // A missing optional logo must not stop the rest of the catalog preload.
            }
            catch (JsonException)
            {
                // A malformed optional logo response must not stop the rest of the catalog preload.
            }
        }

        return tags;
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

        using var providersResponse = await SendAsync(providersUri, configuration.TmdbApiKey, cancellationToken).ConfigureAwait(false);
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
                                var logoUrl = provider.TryGetProperty("logo_path", out var logoPath) && !string.IsNullOrWhiteSpace(logoPath.GetString())
                                    ? TmdbLogoUrl(logoPath.GetString()!)
                                    : null;
                                tags.Add(new SourceTag(TagKind.Provider, name.GetString()!, Name, false, logoUrl));
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
            using var detailsResponse = await SendAsync(detailsUri, configuration.TmdbApiKey, cancellationToken).ConfigureAwait(false);
            if (detailsResponse.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(await detailsResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("networks", out var networks) && networks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var network in networks.EnumerateArray())
                    {
                        if (network.TryGetProperty("name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                        {
                            var logoUrl = network.TryGetProperty("logo_path", out var logoPath) && !string.IsNullOrWhiteSpace(logoPath.GetString())
                                ? TmdbLogoUrl(logoPath.GetString()!)
                                : null;
                            tags.Add(new SourceTag(TagKind.Network, name.GetString()!, Name, false, logoUrl));
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

    private Task<HttpResponseMessage> SendAsync(string uri, string token, CancellationToken cancellationToken) =>
        _requestGate.SendAsync(_httpClient, () => CreateRequest(uri, token), cancellationToken);

    private static string TmdbLogoUrl(string logoPath) => $"https://image.tmdb.org/t/p/w92{logoPath}";

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
