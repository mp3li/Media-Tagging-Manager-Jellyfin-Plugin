using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Caches one source-supplied logo for each normalized Provider or Network name in plugin data.</summary>
public sealed class ProviderNetworkLogoCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkLogoCache"/> class.</summary>
    public ProviderNetworkLogoCache(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    /// <summary>Caches each supplied logo once. Existing cached files are never duplicated or replaced by later scans.</summary>
    public async Task CacheAsync(IEnumerable<SourceTag> tags, CancellationToken cancellationToken)
    {
        foreach (var tag in tags.Where(static tag => !string.IsNullOrWhiteSpace(tag.LogoUrl)))
        {
            await CacheAsync(tag.Kind, tag.Name, tag.LogoUrl!, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Caches one approved source logo for a normalized tag name.</summary>
    public async Task CacheAsync(TagKind kind, string name, string logoUrl, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(name) || !IsApprovedLogoUrl(logoUrl))
        {
            return;
        }

        var normalizedName = TagNameNormalizer.Normalize(kind, name);
        var key = Key(kind, normalizedName);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.TryGetValue(key, out var existing) && File.Exists(Path.Combine(CacheDirectory, existing.FileName)))
            {
                return;
            }

            using var response = await _httpClientFactory.CreateClient().GetAsync(logoUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not "image/png" and not "image/jpeg" and not "image/svg+xml")
            {
                return;
            }

            Directory.CreateDirectory(CacheDirectory);
            var fileName = $"{Hash(key)}{Extension(contentType)}";
            await using (var output = File.Create(Path.Combine(CacheDirectory, fileName)))
            {
                await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            index[key] = new LogoCacheEntry(fileName, contentType);
            await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // A logo is optional metadata. A temporary image-host failure must not affect tagging.
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Saves an administrator-uploaded PNG, JPEG, or SVG as the one logo for a normalized tag name.</summary>
    public async Task SaveUploadedAsync(TagKind kind, string name, Stream content, string? contentType, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Enable logo saving in Logo Settings before uploading a logo.");
        }

        if (string.IsNullOrWhiteSpace(name) || !IsApprovedContentType(contentType))
        {
            throw new InvalidOperationException("Upload a PNG, JPEG, or SVG logo file.");
        }

        var normalizedName = TagNameNormalizer.Normalize(kind, name);
        var key = Key(kind, normalizedName);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var index = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.TryGetValue(key, out var prior))
            {
                var priorPath = Path.Combine(CacheDirectory, prior.FileName);
                if (File.Exists(priorPath))
                {
                    File.Delete(priorPath);
                }
            }

            var safeContentType = contentType!;
            var fileName = $"{Hash(key)}{Extension(safeContentType)}";
            await using (var output = File.Create(Path.Combine(CacheDirectory, fileName)))
            {
                await content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            index[key] = new LogoCacheEntry(fileName, safeContentType);
            await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Opens the one cached logo for a normalized Provider or Network name, if available.</summary>
    public async Task<LogoFile?> OpenAsync(TagKind kind, string name, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (!index.TryGetValue(Key(kind, TagNameNormalizer.Normalize(kind, name)), out var entry))
            {
                return null;
            }

            var path = Path.Combine(CacheDirectory, entry.FileName);
            return File.Exists(path) ? new LogoFile(File.OpenRead(path), entry.ContentType) : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Deletes every cached or administrator-uploaded logo without changing any Jellyfin tags.</summary>
    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, recursive: true);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string CacheDirectory => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "provider-network-logos");

    private static string IndexPath => Path.Combine(CacheDirectory, "index.json");

    private static bool IsEnabled => Plugin.Instance?.Configuration.EnableLogoCaching == true;

    private static string Key(TagKind kind, string name) => $"{kind}:{name.Trim()}";

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/svg+xml" => ".svg",
        _ => ".img"
    };

    private static bool IsApprovedLogoUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (string.Equals(uri.Host, "image.tmdb.org", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "cdn.watchmode.com", StringComparison.OrdinalIgnoreCase));

    private static bool IsApprovedContentType(string? contentType) => contentType is "image/png" or "image/jpeg" or "image/svg+xml";

    private static async Task<Dictionary<string, LogoCacheEntry>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(IndexPath))
        {
            return new Dictionary<string, LogoCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(IndexPath);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, LogoCacheEntry>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new Dictionary<string, LogoCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record LogoCacheEntry(string FileName, string ContentType);
}

/// <summary>A read stream and content type for one cached provider or network logo.</summary>
public sealed record LogoFile(Stream Stream, string ContentType);
