using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Caches one source-supplied logo for each normalized Provider or Network name in plugin data.</summary>
public sealed class ProviderNetworkLogoCache
{
    private const long MaxSourceLogoBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkLogoCache"/> class.</summary>
    public ProviderNetworkLogoCache(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    /// <summary>Caches each supplied logo once. Existing cached files are never duplicated or replaced by later scans.</summary>
    public async Task CacheAsync(IEnumerable<SourceTag> tags, CancellationToken cancellationToken)
        => await CacheAsync(tags, cancellationToken, null).ConfigureAwait(false);

    /// <summary>Caches source logos once and optionally reports each processed source value.</summary>
    public async Task CacheAsync(IEnumerable<SourceTag> tags, CancellationToken cancellationToken, Action<int>? onProcessed)
    {
        var logoTags = tags.Where(static tag => !string.IsNullOrWhiteSpace(tag.LogoUrl)).ToArray();
        for (var index = 0; index < logoTags.Length; index++)
        {
            var tag = logoTags[index];
            await CacheAsync(tag.Kind, tag.Name, tag.LogoUrl!, cancellationToken).ConfigureAwait(false);
            onProcessed?.Invoke(index + 1);
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
            if (contentType is not "image/png" and not "image/jpeg" and not "image/svg+xml"
                || response.Content.Headers.ContentLength is > MaxSourceLogoBytes)
            {
                return;
            }

            Directory.CreateDirectory(CacheDirectory);
            var remainingBytes = GetRemainingCacheBytes(index, key);
            if (remainingBytes <= 0 || (response.Content.Headers.ContentLength is { } contentLength && contentLength > remainingBytes))
            {
                return;
            }

            var fileName = $"{Hash(key)}{Extension(contentType)}";
            var path = Path.Combine(CacheDirectory, fileName);
            var temporaryPath = path + ".partial";
            try
            {
                await using (var output = File.Create(temporaryPath))
                {
                    await CopyWithLimitAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), output, Math.Min(MaxSourceLogoBytes, remainingBytes), cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, path, overwrite: true);
                index[key] = new LogoCacheEntry(fileName, contentType);
                await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        catch (HttpRequestException)
        {
            // A logo is optional metadata. A temporary image-host failure must not affect tagging.
        }
        catch (InvalidOperationException)
        {
            // A source logo over the configured limit is optional metadata and
            // must not prevent the related media tags from being saved.
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
            var remainingBytes = GetRemainingCacheBytes(index, key);
            if (remainingBytes <= 0)
            {
                throw new InvalidOperationException("The logo cache has reached its configured size limit. Delete cached logos or raise the limit in Logo Settings.");
            }

            var safeContentType = contentType!;
            var fileName = $"{Hash(key)}{Extension(safeContentType)}";
            var path = Path.Combine(CacheDirectory, fileName);
            var temporaryPath = path + ".partial";
            try
            {
                await using (var output = File.Create(temporaryPath))
                {
                    await CopyWithLimitAsync(content, output, Math.Min(MaxSourceLogoBytes, remainingBytes), cancellationToken).ConfigureAwait(false);
                }

                if (index.TryGetValue(key, out var prior))
                {
                    var priorPath = Path.Combine(CacheDirectory, prior.FileName);
                    if (File.Exists(priorPath))
                    {
                        File.Delete(priorPath);
                    }
                }

                File.Move(temporaryPath, path, overwrite: true);
                index[key] = new LogoCacheEntry(fileName, safeContentType);
                await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
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

    /// <summary>Lists the currently cached logos without returning image content.</summary>
    public async Task<IReadOnlyCollection<CachedLogoDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            return index.Select(pair =>
                {
                    var separator = pair.Key.IndexOf(':');
                    var kind = separator > 0 && Enum.TryParse<TagKind>(pair.Key[..separator], out var parsed) ? parsed : TagKind.Provider;
                    var name = separator > 0 ? pair.Key[(separator + 1)..] : pair.Key;
                    var path = Path.Combine(CacheDirectory, pair.Value.FileName);
                    return new CachedLogoDto(kind, name, File.Exists(path) ? new FileInfo(path).Length : 0, pair.Value.ContentType);
                })
                .Where(item => item.Bytes > 0)
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Deletes one cached logo without changing media tags or other cache entries.</summary>
    public async Task DeleteAsync(TagKind kind, string name, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadIndexAsync(cancellationToken).ConfigureAwait(false);
            var key = Key(kind, TagNameNormalizer.Normalize(kind, name));
            if (!index.Remove(key, out var entry))
            {
                return;
            }

            var path = Path.Combine(CacheDirectory, entry.FileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            await File.WriteAllTextAsync(IndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Gets the current stored-logo count and byte size.</summary>
    public async Task<(int Count, long Bytes)> GetUsageAsync(CancellationToken cancellationToken)
    {
        var entries = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return (entries.Count, entries.Sum(item => item.Bytes));
    }

    private static string CacheDirectory => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "provider-network-logos");

    private static string IndexPath => Path.Combine(CacheDirectory, "index.json");

    private static bool IsEnabled => Plugin.Instance?.Configuration.EnableLogoCaching == true;

    private static long CacheLimitBytes => (long)Math.Clamp(Plugin.Instance?.Configuration.LogoCacheLimitMegabytes ?? 100, 10, 1024) * 1024 * 1024;

    private static long GetRemainingCacheBytes(IReadOnlyDictionary<string, LogoCacheEntry> index, string replacingKey)
    {
        var existingBytes = index.Where(pair => !string.Equals(pair.Key, replacingKey, StringComparison.OrdinalIgnoreCase))
            .Select(pair => Path.Combine(CacheDirectory, pair.Value.FileName))
            .Where(File.Exists)
            .Sum(path => new FileInfo(path).Length);
        return Math.Max(0, CacheLimitBytes - existingBytes);
    }

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

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long maximumBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return;
            }

            total += count;
            if (total > maximumBytes)
            {
                throw new InvalidOperationException("The logo exceeds its configured cache limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
    }

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
