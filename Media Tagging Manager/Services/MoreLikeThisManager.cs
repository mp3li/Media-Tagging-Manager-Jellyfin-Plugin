using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Persists TMDb recommendations and similar titles separately from Jellyfin media tags for later plugin reuse.</summary>
public sealed class MoreLikeThisManager
{
    private const long MaxPosterBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILibraryManager _libraryManager;
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly MoreLikeThisStateStore _state;
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private readonly SemaphoreSlim _posterLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="MoreLikeThisManager"/> class.</summary>
    public MoreLikeThisManager(ILibraryManager libraryManager, TmdbAvailabilitySource tmdb, MoreLikeThisStateStore state)
    {
        _libraryManager = libraryManager;
        _tmdb = tmdb;
        _state = state;
    }

    /// <summary>Gets whether a normal scan has any More Like This work enabled.</summary>
    public static bool IsConfigured(Configuration.PluginConfiguration configuration) => configuration.AddRecommendations || configuration.AddSimilarTitles;

    /// <summary>Refreshes the enabled TMDb direct relationship lists for one selected-library Movie or Series.</summary>
    internal async Task<MoreLikeThisItemResult> ApplyConfiguredAsync(BaseItem item, Guid libraryId, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!IsConfigured(configuration) || item is not Movie && item is not Series)
        {
            return MoreLikeThisItemResult.NotConfigured;
        }

        var ids = new ExternalIds(GetProviderId(item, "Tmdb"), GetProviderId(item, "Imdb"), item.GetType().Name);
        if (string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            return MoreLikeThisItemResult.MissingTmdbId;
        }

        TmdbRelatedTitlesResult result;
        try
        {
            result = await _tmdb.GetRelatedTitlesAsync(ids, configuration.AddRecommendations, configuration.AddSimilarTitles, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Relationship data is optional. A temporary TMDb outage must not
            // cancel Provider/Network, genre, keyword, or people updates.
            return MoreLikeThisItemResult.LookupFailure;
        }
        catch (JsonException)
        {
            // Keep existing stored relationships if TMDb returns one malformed
            // relationship response rather than treating it as an empty list.
            return MoreLikeThisItemResult.LookupFailure;
        }
        if (!string.IsNullOrWhiteSpace(result.Note))
        {
            return MoreLikeThisItemResult.LookupFailure;
        }

        var recommendations = await PrepareTitlesAsync(result.Recommendations, configuration, cancellationToken).ConfigureAwait(false);
        var similarTitles = await PrepareTitlesAsync(result.SimilarTitles, configuration, cancellationToken).ConfigureAwait(false);
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var existing = document.Items.FirstOrDefault(value => value.ItemId == item.Id);
            var previousRecommendations = existing?.Recommendations ?? [];
            var previousSimilarTitles = existing?.SimilarTitles ?? [];
            if (existing is null)
            {
                existing = new MoreLikeThisStoredItem { ItemId = item.Id };
                document.Items.Add(existing);
            }

            existing.LibraryId = libraryId;
            existing.Name = item.Name;
            existing.ItemType = item.GetType().Name;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
            if (configuration.AddRecommendations)
            {
                existing.Recommendations = recommendations.ToList();
                existing.RecommendationsLoaded = true;
            }

            if (configuration.AddSimilarTitles)
            {
                existing.SimilarTitles = similarTitles.ToList();
                existing.SimilarTitlesLoaded = true;
            }

            document.LastChanges.RemoveAll(value => value.ItemId == item.Id);
            document.LastChanges.Add(new MoreLikeThisStoredChange
            {
                ItemId = item.Id,
                AddedRecommendationIds = configuration.AddRecommendations ? AddedIds(previousRecommendations, recommendations) : [],
                RemovedRecommendationIds = configuration.AddRecommendations ? RemovedIds(previousRecommendations, recommendations) : [],
                AddedSimilarTitleIds = configuration.AddSimilarTitles ? AddedIds(previousSimilarTitles, similarTitles) : [],
                RemovedSimilarTitleIds = configuration.AddSimilarTitles ? RemovedIds(previousSimilarTitles, similarTitles) : [],
                RemovedRecommendations = configuration.AddRecommendations ? RemovedTitles(previousRecommendations, recommendations) : [],
                RemovedSimilarTitles = configuration.AddSimilarTitles ? RemovedTitles(previousSimilarTitles, similarTitles) : []
            });
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            var totalRelationships = recommendations.Count + similarTitles.Count;
            return new MoreLikeThisItemResult(recommendations.Count, similarTitles.Count, true, totalRelationships == 0 ? MoreLikeThisItemOutcome.EmptyRelationshipResult : MoreLikeThisItemOutcome.Saved);
        }
        finally
        {
            _storeLock.Release();
        }
    }

    /// <summary>Returns a clear administrator-facing prerequisite error for dedicated relationship actions.</summary>
    public string? GetScanValidationError()
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!IsConfigured(configuration))
        {
            return "Enable Add recommendations and/or Add similar titles, then save More Like This Settings before loading or updating.";
        }

        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            return "Save a TMDb API Read Access Token before loading recommendations and similar titles.";
        }

        return configuration.LibraryIds.Length == 0
            ? "Select and save one or more libraries in Main Settings before loading recommendations and similar titles."
            : null;
    }

    /// <summary>Loads missing records or updates every selected-library relationship record without running the normal tagging scan.</summary>
    public async Task ScanConfiguredLibrariesAsync(bool onlyMissing, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var validationError = GetScanValidationError();
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var configuration = Plugin.Instance!.Configuration;
        // Preserve the exact configured library identifier. GetTopParent() is
        // not a library identity contract for all Jellyfin item hierarchies;
        // using it here stored records outside the selected-library scope and
        // caused the overview to hide otherwise valid relationship records.
        var candidates = configuration.LibraryIds
            .SelectMany(libraryId => GetLibraryItems(libraryId).Select(item => (Item: item, LibraryId: libraryId)))
            .ToArray();
        if (onlyMissing)
        {
            candidates = await FilterMissingRecordsAsync(candidates, cancellationToken).ConfigureAwait(false);
        }

        var action = onlyMissing ? "Loading" : "Updating";
        _state.Start(candidates.Length, action);
        try
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ApplyConfiguredAsync(candidates[index].Item, candidates[index].LibraryId, cancellationToken).ConfigureAwait(false);
                _state.RecordItem(result);
                progress?.Report(candidates.Length == 0 ? 100 : (index + 1) * 100d / candidates.Length);
            }

            _state.Complete();
        }
        catch (OperationCanceledException)
        {
            _state.Complete("Recommendations and Similar Titles action was cancelled. Records already saved remain available.");
            throw;
        }
        catch (Exception exception)
        {
            _state.Complete($"Recommendations and Similar Titles action stopped: {exception.Message}");
            throw;
        }
    }

    /// <summary>Removes only relationship records owned by this plugin for currently selected libraries, never Jellyfin metadata.</summary>
    public async Task<int> RemoveConfiguredLibraryRecordsAsync(CancellationToken cancellationToken)
    {
        var selectedLibraries = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Items.RemoveAll(value => selectedLibraries.Contains(value.LibraryId));
            document.LastChanges.RemoveAll(value => document.Items.All(item => item.ItemId != value.ItemId));
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            return removed;
        }
        finally
        {
            _storeLock.Release();
        }
    }

    /// <summary>Returns current stored relationships for selected-library media, even before a later scan changes them.</summary>
    public async Task<IReadOnlyCollection<MoreLikeThisOverviewItemDto>> GetOverviewAsync(Guid? requestedLibraryId, CancellationToken cancellationToken)
    {
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
            return document.Items
                .Where(value => selected.Contains(value.LibraryId) && (!requestedLibraryId.HasValue || value.LibraryId == requestedLibraryId.Value))
                .Where(value => _libraryManager.GetItemById(value.ItemId) is Movie or Series)
                .Select(value =>
                {
                    var change = document.LastChanges.FirstOrDefault(candidate => candidate.ItemId == value.ItemId);
                    return new MoreLikeThisOverviewItemDto(
                        value.ItemId,
                        value.Name,
                        value.ItemType,
                        value.LibraryId,
                        value.Recommendations,
                        value.SimilarTitles,
                        change?.RemovedRecommendations ?? [],
                        change?.RemovedSimilarTitles ?? [],
                        change?.AddedRecommendationIds ?? [],
                        change?.RemovedRecommendationIds ?? [],
                        change?.AddedSimilarTitleIds ?? [],
                        change?.RemovedSimilarTitleIds ?? []);
                })
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            _storeLock.Release();
        }
    }

    /// <summary>Returns a bounded page of saved relationships for one selected library.</summary>
    public async Task<MoreLikeThisOverviewPageDto> GetOverviewPageAsync(Guid libraryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 25);
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
            if (!selected.Contains(libraryId))
            {
                throw new InvalidOperationException("Choose and save this library in Main Settings before viewing its relationships.");
            }

            var items = document.Items
                .Where(value => value.LibraryId == libraryId && _libraryManager.GetItemById(value.ItemId) is Movie or Series)
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var pageItems = items.Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(value => ToOverviewItem(value, document.LastChanges.FirstOrDefault(candidate => candidate.ItemId == value.ItemId)))
                .ToArray();
            return new MoreLikeThisOverviewPageDto(pageItems, items.Length, page, pageSize);
        }
        finally
        {
            _storeLock.Release();
        }
    }

    /// <summary>Returns lightweight saved-relationship counts for currently selected libraries.</summary>
    public async Task<IReadOnlyCollection<MoreLikeThisOverviewCountDto>> GetOverviewCountsAsync(CancellationToken cancellationToken)
    {
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var selected = Plugin.Instance?.Configuration.LibraryIds ?? [];
            var document = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return selected.Select(libraryId => new MoreLikeThisOverviewCountDto(
                libraryId,
                document.Items.Count(value => value.LibraryId == libraryId && _libraryManager.GetItemById(value.ItemId) is Movie or Series))).ToArray();
        }
        finally
        {
            _storeLock.Release();
        }
    }

    /// <summary>Opens one plugin-cached poster, if it is currently available.</summary>
    public async Task<(Stream Stream, string ContentType)?> OpenPosterAsync(int tmdbId, CancellationToken cancellationToken)
    {
        await _posterLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadPosterIndexAsync(cancellationToken).ConfigureAwait(false);
            if (!index.TryGetValue(tmdbId, out var entry))
            {
                return null;
            }

            var path = Path.Combine(PosterDirectory, entry.FileName);
            return File.Exists(path) ? (File.OpenRead(path), entry.ContentType) : null;
        }
        finally
        {
            _posterLock.Release();
        }
    }

    /// <summary>Returns bounded poster-cache storage information for the settings tab.</summary>
    public async Task<MoreLikeThisImageCacheStatus> GetImageCacheStatusAsync(CancellationToken cancellationToken)
    {
        await _posterLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadPosterIndexAsync(cancellationToken).ConfigureAwait(false);
            var bytes = index.Values.Sum(value =>
            {
                var path = Path.Combine(PosterDirectory, value.FileName);
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            });
            var limit = Math.Clamp(Plugin.Instance?.Configuration.MoreLikeThisImageCacheLimitMegabytes ?? 100, 10, 1024);
            return new MoreLikeThisImageCacheStatus(index.Count, bytes, limit);
        }
        finally
        {
            _posterLock.Release();
        }
    }

    private async Task<IReadOnlyCollection<RelatedTitleDto>> PrepareTitlesAsync(IReadOnlyCollection<RelatedTitleDto> values, Configuration.PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        foreach (var title in values.Where(value => !string.IsNullOrWhiteSpace(value.PosterUrl)))
        {
            if (configuration.SaveMoreLikeThisImagesToDisk)
            {
                try
                {
                    await CachePosterAsync(title.TmdbId, title.PosterUrl!, configuration, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    // Poster caching is optional and must not discard the
                    // title relationship when the image host is unavailable.
                }
                catch (IOException)
                {
                    // A full or unavailable plugin-data directory must not
                    // prevent a normal metadata scan from completing.
                }
            }
        }

        return values.Select(value => configuration.SaveMoreLikeThisImageLinks ? value : value with { PosterUrl = null }).ToArray();
    }

    private async Task<(BaseItem Item, Guid LibraryId)[]> FilterMissingRecordsAsync((BaseItem Item, Guid LibraryId)[] candidates, CancellationToken cancellationToken)
    {
        await _storeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            var existing = (await ReadAsync(cancellationToken).ConfigureAwait(false)).Items.ToDictionary(value => value.ItemId);
            return candidates.Where(value => !existing.TryGetValue(value.Item.Id, out var record)
                || record.LibraryId != value.LibraryId
                || (configuration.AddRecommendations && !record.RecommendationsLoaded)
                || (configuration.AddSimilarTitles && !record.SimilarTitlesLoaded)).ToArray();
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private IEnumerable<BaseItem> GetLibraryItems(Guid libraryId) => _libraryManager.GetItemList(new InternalItemsQuery
    {
        ParentId = libraryId,
        Recursive = true,
        IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
    });

    private async Task CachePosterAsync(int tmdbId, string posterUrl, Configuration.PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        await _posterLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await ReadPosterIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.ContainsKey(tmdbId))
            {
                return;
            }

            var limitBytes = Math.Clamp(configuration.MoreLikeThisImageCacheLimitMegabytes, 10, 1024) * 1024L * 1024L;
            var currentBytes = index.Values.Sum(value =>
            {
                var path = Path.Combine(PosterDirectory, value.FileName);
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            });
            if (currentBytes >= limitBytes)
            {
                return;
            }

            var image = await _tmdb.DownloadPosterImageAsync(posterUrl, cancellationToken).ConfigureAwait(false);
            if (image is null || image.Content.LongLength > MaxPosterBytes || currentBytes + image.Content.LongLength > limitBytes)
            {
                return;
            }

            Directory.CreateDirectory(PosterDirectory);
            var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tmdbId.ToString(System.Globalization.CultureInfo.InvariantCulture)))) + Extension(image.ContentType);
            var path = Path.Combine(PosterDirectory, fileName);
            await File.WriteAllBytesAsync(path, image.Content, cancellationToken).ConfigureAwait(false);
            index[tmdbId] = new MoreLikeThisPosterCacheEntry { FileName = fileName, ContentType = image.ContentType };
            await File.WriteAllTextAsync(PosterIndexPath, JsonSerializer.Serialize(index, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _posterLock.Release();
        }
    }

    private async Task<MoreLikeThisDocument> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StorePath))
        {
            return new MoreLikeThisDocument();
        }

        await using var stream = File.OpenRead(StorePath);
        return await JsonSerializer.DeserializeAsync<MoreLikeThisDocument>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? new MoreLikeThisDocument();
    }

    private async Task WriteAsync(MoreLikeThisDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
        await using var stream = File.Create(StorePath);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<int, MoreLikeThisPosterCacheEntry>> ReadPosterIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(PosterIndexPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(PosterIndexPath);
        return await JsonSerializer.DeserializeAsync<Dictionary<int, MoreLikeThisPosterCacheEntry>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private static IReadOnlyCollection<int> AddedIds(IEnumerable<RelatedTitleDto> before, IEnumerable<RelatedTitleDto> after) => after.Select(value => value.TmdbId).Except(before.Select(value => value.TmdbId)).ToArray();

    private static IReadOnlyCollection<int> RemovedIds(IEnumerable<RelatedTitleDto> before, IEnumerable<RelatedTitleDto> after) => before.Select(value => value.TmdbId).Except(after.Select(value => value.TmdbId)).ToArray();
    private static IReadOnlyCollection<RelatedTitleDto> RemovedTitles(IEnumerable<RelatedTitleDto> before, IEnumerable<RelatedTitleDto> after) => before.Where(value => !after.Any(candidate => candidate.TmdbId == value.TmdbId)).ToArray();

    private static string? GetProviderId(BaseItem item, string name) => item.ProviderIds.TryGetValue(name, out var value) ? value : null;
    private static MoreLikeThisOverviewItemDto ToOverviewItem(MoreLikeThisStoredItem value, MoreLikeThisStoredChange? change) => new(
        value.ItemId,
        value.Name,
        value.ItemType,
        value.LibraryId,
        value.Recommendations,
        value.SimilarTitles,
        change?.RemovedRecommendations ?? [],
        change?.RemovedSimilarTitles ?? [],
        change?.AddedRecommendationIds ?? [],
        change?.RemovedRecommendationIds ?? [],
        change?.AddedSimilarTitleIds ?? [],
        change?.RemovedSimilarTitleIds ?? []);
    private static string StorePath => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "more-like-this.json");
    private static string PosterDirectory => Path.Combine(Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin data folder is unavailable."), "more-like-this-posters");
    private static string PosterIndexPath => Path.Combine(PosterDirectory, "index.json");
    private static string Extension(string contentType) => contentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };

    /// <summary>One item's relationship result used by normal and dedicated scans.</summary>
    internal enum MoreLikeThisItemOutcome
    {
        /// <summary>The feature was disabled while the scan was in progress.</summary>
        NotConfigured,
        /// <summary>The Jellyfin item has no TMDb identifier.</summary>
        MissingTmdbId,
        /// <summary>TMDb could not provide a usable relationship response.</summary>
        LookupFailure,
        /// <summary>TMDb returned a valid empty relationship response.</summary>
        EmptyRelationshipResult,
        /// <summary>At least one requested relationship title was saved.</summary>
        Saved
    }

    internal sealed record MoreLikeThisItemResult(int Recommendations, int SimilarTitles, bool Saved, MoreLikeThisItemOutcome Outcome)
    {
        /// <summary>Gets an empty relationship result.</summary>
        public static MoreLikeThisItemResult NotConfigured { get; } = new(0, 0, false, MoreLikeThisItemOutcome.NotConfigured);
        /// <summary>Gets a missing-ID result.</summary>
        public static MoreLikeThisItemResult MissingTmdbId { get; } = new(0, 0, false, MoreLikeThisItemOutcome.MissingTmdbId);
        /// <summary>Gets a safe TMDb lookup-failure result.</summary>
        public static MoreLikeThisItemResult LookupFailure { get; } = new(0, 0, false, MoreLikeThisItemOutcome.LookupFailure);
    }
}

/// <summary>Persistent private relationship data; it is deliberately not a Jellyfin tag or NFO field.</summary>
public sealed class MoreLikeThisDocument
{
    /// <summary>Gets or sets the stored current relationships.</summary>
    public List<MoreLikeThisStoredItem> Items { get; set; } = [];

    /// <summary>Gets or sets the colored changes from the latest normal scan.</summary>
    public List<MoreLikeThisStoredChange> LastChanges { get; set; } = [];
}

/// <summary>One current selected-library item's persisted direct TMDb relationships.</summary>
public sealed class MoreLikeThisStoredItem
{
    /// <summary>Gets or sets the related Jellyfin item identifier.</summary>
    public Guid ItemId { get; set; }
    /// <summary>Gets or sets the selected library that owns the Jellyfin item.</summary>
    public Guid LibraryId { get; set; }
    /// <summary>Gets or sets the displayed Jellyfin item title captured on refresh.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the captured Jellyfin item type.</summary>
    public string ItemType { get; set; } = string.Empty;
    /// <summary>Gets or sets when TMDb relationships were last refreshed.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
    /// <summary>Gets or sets the current direct TMDb recommendations.</summary>
    public List<RelatedTitleDto> Recommendations { get; set; } = [];
    /// <summary>Gets or sets the current direct TMDb similar titles.</summary>
    public List<RelatedTitleDto> SimilarTitles { get; set; } = [];
    /// <summary>Gets or sets whether the current Recommendation setting has been loaded at least once for this item.</summary>
    public bool RecommendationsLoaded { get; set; }
    /// <summary>Gets or sets whether the current Similar Titles setting has been loaded at least once for this item.</summary>
    public bool SimilarTitlesLoaded { get; set; }
}

/// <summary>Colored latest-operation changes for one related-title record.</summary>
public sealed class MoreLikeThisStoredChange
{
    /// <summary>Gets or sets the related Jellyfin item identifier.</summary>
    public Guid ItemId { get; set; }
    /// <summary>Gets or sets titles newly present in the latest recommendation refresh.</summary>
    public IReadOnlyCollection<int> AddedRecommendationIds { get; set; } = [];
    /// <summary>Gets or sets titles no longer present in the latest recommendation refresh.</summary>
    public IReadOnlyCollection<int> RemovedRecommendationIds { get; set; } = [];
    /// <summary>Gets or sets titles newly present in the latest similar-title refresh.</summary>
    public IReadOnlyCollection<int> AddedSimilarTitleIds { get; set; } = [];
    /// <summary>Gets or sets titles no longer present in the latest similar-title refresh.</summary>
    public IReadOnlyCollection<int> RemovedSimilarTitleIds { get; set; } = [];
    /// <summary>Gets or sets retained details for the latest removed recommendations.</summary>
    public IReadOnlyCollection<RelatedTitleDto> RemovedRecommendations { get; set; } = [];
    /// <summary>Gets or sets retained details for the latest removed similar titles.</summary>
    public IReadOnlyCollection<RelatedTitleDto> RemovedSimilarTitles { get; set; } = [];
}

/// <summary>One cached poster file indexed by TMDb title ID.</summary>
public sealed class MoreLikeThisPosterCacheEntry
{
    /// <summary>Gets or sets the opaque cached poster file name.</summary>
    public string FileName { get; set; } = string.Empty;
    /// <summary>Gets or sets the cached poster MIME type.</summary>
    public string ContentType { get; set; } = "image/jpeg";
}
