using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Configuration;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Lets administrators add compatible JSON sources without changing or rebuilding the plugin.</summary>
public sealed class CustomJsonAvailabilitySource : IAvailabilitySource
{
    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="CustomJsonAvailabilitySource"/> class.</summary>
    public CustomJsonAvailabilitySource(HttpClient httpClient) => _httpClient = httpClient;

    /// <inheritdoc />
    public string Name => "Custom JSON sources";

    /// <inheritdoc />
    public async Task<SourceLookupResult> LookupAsync(ExternalIds ids, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null)
        {
            return new SourceLookupResult(Name, []);
        }

        var collected = new List<SourceTag>();
        foreach (var source in configuration.CustomSources.Where(static source => source.Enabled && !string.IsNullOrWhiteSpace(source.UrlTemplate)))
        {
            var uri = Expand(source.UrlTemplate, ids, configuration.Region);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrWhiteSpace(source.Authorization))
            {
                request.Headers.TryAddWithoutValidation("Authorization", source.Authorization);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            collected.AddRange(FindStrings(document.RootElement, source.ProviderPath).Select(name => new SourceTag(TagKind.Provider, name, source.Name)));
            collected.AddRange(FindStrings(document.RootElement, source.NetworkPath).Select(name => new SourceTag(TagKind.Network, name, source.Name)));
        }

        return new SourceLookupResult(Name, collected);
    }

    private static string Expand(string template, ExternalIds ids, string region) => template
        .Replace("{tmdb}", Uri.EscapeDataString(ids.Tmdb ?? string.Empty), StringComparison.OrdinalIgnoreCase)
        .Replace("{imdb}", Uri.EscapeDataString(ids.Imdb ?? string.Empty), StringComparison.OrdinalIgnoreCase)
        .Replace("{type}", Uri.EscapeDataString(ids.MediaType), StringComparison.OrdinalIgnoreCase)
        .Replace("{region}", Uri.EscapeDataString(region), StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> FindStrings(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return [];
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String when !string.IsNullOrWhiteSpace(current.GetString()) => [current.GetString()!],
            JsonValueKind.Array => current.EnumerateArray()
                .Where(static value => value.ValueKind == JsonValueKind.String)
                .Select(static value => value.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!),
            _ => []
        };
    }
}
