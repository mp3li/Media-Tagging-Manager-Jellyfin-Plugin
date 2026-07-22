using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Uses Watchmode's title-source endpoint for optional additional availability coverage.</summary>
public sealed class WatchmodeAvailabilitySource : IAvailabilitySource
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="WatchmodeAvailabilitySource"/> class.</summary>
    public WatchmodeAvailabilitySource(HttpClient httpClient) => _httpClient = httpClient;

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

        var requestedRegion = Uri.EscapeDataString(configuration.Region.ToUpperInvariant());
        var uri = $"https://api.watchmode.com/v1/title/{Uri.EscapeDataString(ids.Imdb)}/sources/?regions={requestedRegion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-API-Key", configuration.WatchmodeApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
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
                && !string.Equals(region.GetString(), configuration.Region, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tags.Add(new SourceTag(TagKind.Provider, name.GetString()!, Name));
        }

        return new SourceLookupResult(Name, tags);
    }
}
