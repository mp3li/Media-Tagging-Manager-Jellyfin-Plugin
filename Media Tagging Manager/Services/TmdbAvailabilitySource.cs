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

    /// <inheritdoc />
    public async Task<SourceLookupResult> LookupAsync(ExternalIds ids, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.TmdbApiKey) || string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            return new SourceLookupResult(Name, []);
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
            if (document.RootElement.TryGetProperty("results", out var results)
                && results.TryGetProperty(configuration.Region.ToUpperInvariant(), out var region))
            {
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
}
