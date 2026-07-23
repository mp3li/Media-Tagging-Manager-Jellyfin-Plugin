namespace Jellyfin.Plugin.MediaTaggingManager.Models;

/// <summary>The distinct tag classifications managed by this plugin.</summary>
public enum TagKind
{
    /// <summary>A current streaming, rental, purchase, or television provider.</summary>
    Provider,

    /// <summary>A broadcast, cable, or production network.</summary>
    Network,

    /// <summary>A source-provided movie or television genre.</summary>
    Genre,

    /// <summary>A source-provided title keyword.</summary>
    Keyword,

    /// <summary>A direct TMDb movie collection membership.</summary>
    Collection
}

/// <summary>A source value with provenance, optional TV-network-app classification, and source logo URL.</summary>
public sealed record SourceTag(TagKind Kind, string Name, string Source, bool IsTvNetworkApp = false, string? LogoUrl = null);

/// <summary>Stable external identifiers available for a Jellyfin item.</summary>
public sealed record ExternalIds(string? Tmdb, string? Imdb, string MediaType);

/// <summary>The data returned from a single source adapter.</summary>
public sealed record SourceLookupResult(string Source, IReadOnlyCollection<SourceTag> Tags, string? Note = null);

/// <summary>A country with watch-provider data available from TMDb.</summary>
public sealed record AvailabilityRegionDto(string Code, string Name);

/// <summary>TMDb country choices plus any administrator-facing setup guidance.</summary>
public sealed record AvailabilityRegionsResponse(IReadOnlyCollection<AvailabilityRegionDto> Regions, string? Message);

/// <summary>An official TMDb movie or television genre.</summary>
public sealed record GenreDto(int Id, string Name);

/// <summary>Current locally tracked Watchmode usage for the administrator dashboard.</summary>
public sealed record WatchmodeUsageDto(int Used, int Limit, string CycleStart, string ResetsOn, bool IsConfigured, bool IsLimitReached);

/// <summary>All provider and network names known from selected-library scans and current tags.</summary>
public sealed record TagChoicesDto(
    IReadOnlyCollection<string> Providers,
    IReadOnlyCollection<string> Networks,
    string? ProviderCatalogStatus = null,
    string? NetworkCatalogStatus = null);

/// <summary>Reference names returned from one enabled source's provider and network catalog endpoints.</summary>
public sealed record SourceCatalogResult(
    IReadOnlyCollection<string> Providers,
    IReadOnlyCollection<string> Networks,
    string? Note = null,
    IReadOnlyDictionary<string, string>? ProviderLogoUrls = null);

/// <summary>Result of removing one kind of plugin-owned tag without contacting any source.</summary>
public sealed record TagSyncResult(int TagsRemoved, int MediaItemsChanged);

/// <summary>Result of adding administrator-selected plugin-owned tags.</summary>
public sealed record TagApplyResult(int TagsAdded, int MediaItemsChanged);

/// <summary>Progress visible while administrator-requested logo caching runs.</summary>
public sealed class LogoLoadProgress
{
    /// <summary>Gets or sets whether a logo-loading operation is active.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the number of source-supplied logos selected for processing.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the number of source-supplied logos processed.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets a user-facing completion or failure message.</summary>
    public string Message { get; set; } = "No logo load is currently running.";
}

/// <summary>One cached provider or network logo available for selective deletion.</summary>
public sealed record CachedLogoDto(TagKind Kind, string Name, long Bytes, string ContentType);

/// <summary>Current logo-cache storage and background-load status.</summary>
public sealed record LogoCacheStatus(int FileCount, long Bytes, int LimitMegabytes, LogoLoadProgress LoadProgress);

/// <summary>A Provider or Network tag in selected libraries that is neither plugin-known nor recognized by enabled source catalogs.</summary>
public sealed record UnknownTaggedNameDto(TagKind Kind, string Name, int MediaItemCount);

/// <summary>Progress visible in the dashboard while a scan runs.</summary>
public sealed class ScanProgress
{
    /// <summary>Gets or sets whether a scan is currently in progress.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the number of items queued for the active scan.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the number of completed items.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets the title currently being processed.</summary>
    public string CurrentTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time at which the scan began.</summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>Gets or sets the estimated remaining duration.</summary>
    public TimeSpan? EstimatedRemaining { get; set; }

    /// <summary>Gets the estimated remaining duration as a JavaScript-safe number of seconds.</summary>
    public double? EstimatedRemainingSeconds => EstimatedRemaining?.TotalSeconds;

    /// <summary>Gets or sets the number of plugin tags newly added by the current or most recent scan.</summary>
    public int TagsAdded { get; set; }

    /// <summary>Gets or sets the number of media items that received at least one new plugin tag.</summary>
    public int MediaItemsTagged { get; set; }

    /// <summary>Gets or sets the latest non-fatal error.</summary>
    public string? LastError { get; set; }
}

/// <summary>A dashboard-facing summary of one library item.</summary>
public sealed record TaggedItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid? LibraryId,
    IReadOnlyCollection<string> Providers,
    IReadOnlyCollection<string> Networks,
    IReadOnlyCollection<string> Genres,
    IReadOnlyCollection<string> Keywords,
    IReadOnlyCollection<string> Collections,
    DateTimeOffset? LastCheckedUtc,
    IReadOnlyCollection<string> Sources);

/// <summary>A direct TMDb collection match for one movie in a selected library.</summary>
public sealed record CollectionMatchDto(Guid ItemId, Guid LibraryId, string Title, string CollectionName, string Source);

/// <summary>A stored, restorable tag snapshot summary shown in the administrator dashboard.</summary>
public sealed record TagBackupSummary(Guid Id, string Label, DateTimeOffset CreatedUtc, int ItemCount);

/// <summary>A complete tag snapshot for selected Jellyfin library items.</summary>
public sealed class TagBackupDocument
{
    /// <summary>Gets or sets the backup format version.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Gets or sets the stable backup ID.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the backup label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Gets or sets every captured item and its complete tag list.</summary>
    public List<TagBackupItem> Items { get; set; } = [];
}

/// <summary>The complete tag state of one Jellyfin item at backup time.</summary>
public sealed class TagBackupItem
{
    /// <summary>Gets or sets the Jellyfin item ID.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the title captured for administrator reference.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the captured Jellyfin item type.</summary>
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Gets or sets the complete tag list, including tags not owned by this plugin.</summary>
    public string[] Tags { get; set; } = [];
}
