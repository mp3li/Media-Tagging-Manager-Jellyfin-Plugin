using System.Text.Json;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Builds and caches the explicit, country-filtered Network picker catalog without changing media tags.</summary>
public sealed class NetworkCatalogCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly WatchmodeAvailabilitySource _watchmode;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly object _progressLock = new();
    private NetworkCatalogLoadProgress _progress = new();

    /// <summary>Initializes the cache service.</summary>
    public NetworkCatalogCache(TmdbAvailabilitySource tmdb, WatchmodeAvailabilitySource watchmode)
    {
        _tmdb = tmdb;
        _watchmode = watchmode;
    }

    /// <summary>Gets the current cache only when it was built for the exact saved region selection and is still within Watchmode's cache window.</summary>
    public async Task<NetworkCatalogStatus> GetStatusAsync(Configuration.PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
        var regionsKey = RegionsKey(configuration);
        var valid = document is not null
            && string.Equals(document.RegionsKey, regionsKey, StringComparison.Ordinal)
            && document.CachedUtc >= DateTimeOffset.UtcNow.AddDays(-30);
        var progress = GetProgress();
        var message = progress.Message;
        if (!progress.IsRunning && valid)
        {
            message = $"{document!.Networks.Length} Networks are cached for the saved availability regions.";
        }
        else if (!progress.IsRunning && document is not null && !string.Equals(document.RegionsKey, regionsKey, StringComparison.Ordinal))
        {
            message = "Availability regions changed. Load Networks again to build the matching Network list.";
        }

        progress.Message = message;
        return new NetworkCatalogStatus(
            valid,
            valid ? document!.Networks : [],
            progress,
            valid ? document!.CachedUtc : null,
            valid ? document!.NetworkTmdbIds : null);
    }

    /// <summary>Begins an explicit background catalog load for the current saved regions.</summary>
    public bool TryStart(Configuration.PluginConfiguration configuration)
    {
        var regions = GetRegions(configuration);
        if (regions.Length == 0)
        {
            throw new InvalidOperationException("Save at least one availability region before loading Networks.");
        }

        lock (_progressLock)
        {
            if (_progress.IsRunning)
            {
                return false;
            }

            _progress = new NetworkCatalogLoadProgress { IsRunning = true, Message = "Preparing TMDb and Watchmode Network catalogs…" };
        }

        var regionsSnapshot = regions.ToArray();
        _ = Task.Run(async () =>
        {
            try
            {
                var tmdbTask = _tmdb.GetCountryNetworkCatalogAsync(
                    regionsSnapshot,
                    total => SetProgress(total, 0, "Loading and filtering TMDb Networks…"),
                    completed => SetCompleted(completed),
                    CancellationToken.None);
                IReadOnlyCollection<NetworkCatalogEntry> watchmodeNetworks;
                try
                {
                    watchmodeNetworks = await _watchmode.GetCountryNetworkCatalogAsync(regionsSnapshot, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException)
                {
                    watchmodeNetworks = [];
                    SetMessage("Loading and filtering TMDb Networks… Watchmode's optional catalog could not be loaded: " + exception.Message);
                }

                IReadOnlyCollection<NetworkCatalogEntry> tmdbNetworks;
                string? tmdbFailure = null;
                try
                {
                    tmdbNetworks = await tmdbTask.ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException)
                {
                    // Watchmode's source-owned catalog remains useful when the
                    // optional TMDb export is temporarily unavailable.
                    tmdbNetworks = [];
                    tmdbFailure = exception.Message;
                }
                var networks = tmdbNetworks.Concat(watchmodeNetworks)
                    .Select(entry => TagNameNormalizer.Normalize(TagKind.Network, entry.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var networkTmdbIds = tmdbNetworks.Concat(watchmodeNetworks)
                    .Where(entry => entry.TmdbId is > 0)
                    .GroupBy(entry => TagNameNormalizer.Normalize(TagKind.Network, entry.Name), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().TmdbId!.Value, StringComparer.OrdinalIgnoreCase);
                await WriteAsync(new NetworkCatalogDocument
                {
                    RegionsKey = string.Join(",", regionsSnapshot.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    CachedUtc = DateTimeOffset.UtcNow,
                    Networks = networks,
                    NetworkTmdbIds = networkTmdbIds
                }, CancellationToken.None).ConfigureAwait(false);
                var sourceNote = tmdbFailure is null ? string.Empty : " TMDb's catalog was unavailable; loaded the available Watchmode Network catalog instead.";
                Complete($"Loaded {networks.Length} Networks for the saved availability regions.{sourceNote}");
            }
            catch (Exception exception)
            {
                Complete("Network loading stopped: " + exception.Message);
            }
        });
        return true;
    }

    private NetworkCatalogLoadProgress GetProgress()
    {
        lock (_progressLock)
        {
            return new NetworkCatalogLoadProgress
            {
                IsRunning = _progress.IsRunning,
                Total = _progress.Total,
                Completed = _progress.Completed,
                Message = _progress.Message
            };
        }
    }

    private void SetProgress(int total, int completed, string message)
    {
        lock (_progressLock)
        {
            _progress.Total = total;
            _progress.Completed = completed;
            _progress.Message = message;
        }
    }

    private void SetCompleted(int completed)
    {
        lock (_progressLock)
        {
            _progress.Completed = completed;
        }
    }

    private void SetMessage(string message)
    {
        lock (_progressLock)
        {
            _progress.Message = message;
        }
    }

    private void Complete(string message)
    {
        lock (_progressLock)
        {
            _progress.IsRunning = false;
            _progress.Message = message;
        }
    }

    private async Task<NetworkCatalogDocument?> ReadAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(CachePath);
            return await JsonSerializer.DeserializeAsync<NetworkCatalogDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task WriteAsync(NetworkCatalogDocument document, CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(document, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string[] GetRegions(Configuration.PluginConfiguration configuration) => (configuration.Regions ?? [])
        .Where(region => !string.IsNullOrWhiteSpace(region))
        .Select(region => region.Trim().ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(3)
        .DefaultIfEmpty(configuration.Region.Trim().ToUpperInvariant())
        .ToArray();

    private static string RegionsKey(Configuration.PluginConfiguration configuration) => string.Join(",", GetRegions(configuration).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

    private static string CacheDirectory => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "network-catalog");

    private static string CachePath => Path.Combine(CacheDirectory, "networks.json");

    private sealed class NetworkCatalogDocument
    {
        public string RegionsKey { get; set; } = string.Empty;
        public DateTimeOffset CachedUtc { get; set; }
        public string[] Networks { get; set; } = [];

        public Dictionary<string, int> NetworkTmdbIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
