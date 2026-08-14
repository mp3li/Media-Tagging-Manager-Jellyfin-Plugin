#pragma warning disable CS1591
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
    Collection,

    /// <summary>A TMDb production company saved in Jellyfin's native Studio metadata.</summary>
    ProductionCompany
}

/// <summary>A source value with provenance, optional TV-network-app classification, and source logo URL.</summary>
public sealed record SourceTag(TagKind Kind, string Name, string Source, bool IsTvNetworkApp = false, string? LogoUrl = null);

/// <summary>Stable external identifiers available for a Jellyfin item.</summary>
public sealed record ExternalIds(string? Tmdb, string? Imdb, string MediaType);

/// <summary>One TMDb cast or crew credit available to fill missing Jellyfin people data.</summary>
public sealed record TmdbPersonCredit(
    int TmdbPersonId,
    string Name,
    string? Character,
    string? Job,
    string? Department,
    int Order,
    string? ProfilePath);

/// <summary>Cast and crew returned for one TMDb movie or television title.</summary>
public sealed record TmdbCreditsResult(
    IReadOnlyCollection<TmdbPersonCredit> Cast,
    IReadOnlyCollection<TmdbPersonCredit> Crew,
    string? Note = null);

/// <summary>One downloaded TMDb profile image ready for Jellyfin's native image writer.</summary>
public sealed record TmdbPersonImage(byte[] Content, string ContentType, long SourceBytes);

/// <summary>One TMDb production company associated directly with a title.</summary>
public sealed record TmdbProductionCompany(int TmdbCompanyId, string Name, string? OriginCountry, string? LogoUrl);

/// <summary>One TMDb production country associated directly with a title.</summary>
public sealed record TmdbProductionCountry(string Code, string Name);

/// <summary>Production companies and countries returned from one TMDb movie or TV detail record.</summary>
public sealed record TmdbProductionResult(IReadOnlyCollection<TmdbProductionCompany> Companies, IReadOnlyCollection<TmdbProductionCountry> Countries, string? Note = null);

/// <summary>TMDb ratings, certifications, languages, and translations for one title.</summary>
public sealed record TmdbSupplementalMetadataResult(
    double? VoteAverage,
    int? VoteCount,
    bool? Adult,
    string? OriginalLanguage,
    IReadOnlyCollection<string> SpokenLanguages,
    IReadOnlyCollection<TmdbClassification> Classifications,
    IReadOnlyCollection<TmdbTranslation> Translations,
    string? Note = null);

/// <summary>One country-specific TMDb certification for a title.</summary>
public sealed record TmdbClassification(string CountryCode, string Certification);

/// <summary>One available TMDb translation, preserving its language and optional region.</summary>
public sealed record TmdbTranslation(string LanguageCode, string? CountryCode, string? Title, string? Overview);

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
    IReadOnlyDictionary<string, string>? ProviderLogoUrls = null,
    IReadOnlyDictionary<string, int>? NetworkTmdbIds = null);

/// <summary>One source-owned Network catalog entry with its source-supplied origin country.</summary>
public sealed record NetworkCatalogEntry(string Name, string OriginCountry, string Source, int? TmdbId = null);

/// <summary>Current progress of the explicit Network-catalog load action.</summary>
public sealed class NetworkCatalogLoadProgress
{
    /// <summary>Gets or sets whether the catalog load is active.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the number of TMDb Network records to inspect.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the number of TMDb Network records inspected.</summary>
    public int Completed { get; set; }

    /// <summary>Gets or sets a user-facing status message.</summary>
    public string Message { get; set; } = "Networks have not been loaded for the saved availability regions.";
}

/// <summary>Network names cached for exactly one saved set of availability regions.</summary>
public sealed record NetworkCatalogStatus(
    bool IsCachedForCurrentRegions,
    IReadOnlyCollection<string> Networks,
    NetworkCatalogLoadProgress LoadProgress,
    DateTimeOffset? CachedUtc,
    IReadOnlyDictionary<string, int>? NetworkTmdbIds = null,
    int TmdbNetworkCount = 0,
    int WatchmodeNetworkCount = 0);

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

    /// <summary>Gets or sets the number of Network values returned by TMDb during the active or latest scan.</summary>
    public int TmdbNetworkTagsReturned { get; set; }

    /// <summary>Gets or sets the number of Network values returned by Watchmode fallback lookups during the active or latest scan.</summary>
    public int WatchmodeNetworkTagsReturned { get; set; }

    /// <summary>Gets or sets the number of returned Network values excluded by the saved Network selection.</summary>
    public int NetworkTagsFilteredBySelection { get; set; }

    /// <summary>Gets or sets the number of unique selected Network tags that reached the final application stage.</summary>
    public int NetworkTagsEligibleForApplication { get; set; }

    /// <summary>Gets or sets the number of final Network candidates already present on their media item.</summary>
    public int NetworkTagsAlreadyPresent { get; set; }

    /// <summary>Gets or sets the number of Network candidates withheld by the explicit streaming-app-only setting.</summary>
    public int NetworkTagsSuppressedByStreamingAppSetting { get; set; }

    /// <summary>Gets or sets the number of Network tags actually added by the current or latest scan.</summary>
    public int NetworkTagsAdded { get; set; }

    /// <summary>Gets or sets the number of Series for which TMDb's Network lookup did not succeed.</summary>
    public int TmdbNetworkLookupFailures { get; set; }

    /// <summary>Gets or sets the number of Watchmode Network fallback attempts.</summary>
    public int WatchmodeNetworkFallbackAttempts { get; set; }

    /// <summary>Gets or sets the number of Watchmode Network fallback lookups that did not succeed.</summary>
    public int WatchmodeNetworkLookupFailures { get; set; }

    /// <summary>Gets or sets the number of cast members appended during the active or latest full scan.</summary>
    public int CastMembersAdded { get; set; }

    /// <summary>Gets or sets the number of crew members appended during the active or latest full scan.</summary>
    public int CrewMembersAdded { get; set; }

    /// <summary>Gets or sets the number of missing person photos inspected during the active or latest full scan.</summary>
    public int MissingPeoplePhotos { get; set; }

    /// <summary>Gets or sets the number of missing person photos with a TMDb image available during the active or latest full scan.</summary>
    public int TmdbPeoplePhotosAvailable { get; set; }

    /// <summary>Gets or sets the number of person photos saved during the active or latest full scan.</summary>
    public int PeoplePhotosAdded { get; set; }

    /// <summary>Gets or sets the known source bytes for person photos saved during the active or latest full scan.</summary>
    public long PeoplePhotoBytes { get; set; }
}

/// <summary>Progress and results for the dedicated missing-person-photo action.</summary>
public sealed class CastCrewPhotoProgress
{
    /// <summary>Gets or sets whether a dedicated people-photo scan is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the number of selected-library Movies and Series planned for inspection.</summary>
    public int TotalItems { get; set; }

    /// <summary>Gets or sets the number of selected-library items inspected.</summary>
    public int CompletedItems { get; set; }

    /// <summary>Gets or sets the number of missing people photos inspected.</summary>
    public int MissingPhotoCount { get; set; }

    /// <summary>Gets or sets the number of missing people photos for which TMDb supplied a profile image.</summary>
    public int TmdbPhotoAvailableCount { get; set; }

    /// <summary>Gets or sets the number of person photos successfully saved to Jellyfin.</summary>
    public int PhotosAdded { get; set; }

    /// <summary>Gets or sets the known source bytes downloaded for saved person photos.</summary>
    public long EstimatedBytes { get; set; }

    /// <summary>Gets or sets a user-facing status, warning, or completion message.</summary>
    public string Message { get; set; } = "No cast and crew photo scan is currently running.";
}

/// <summary>Result of removing only people assignments explicitly recorded as added by this plugin.</summary>
public sealed record CastCrewCleanupResult(int CastOrCrewRemoved, int PeopleImagesRemoved, int ItemsChanged, int ImagesSkipped);

/// <summary>Cast, crew, and shared-person-image changes from the latest Cast and Crew operation.</summary>
public sealed record CastCrewChangeItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid LibraryId,
    IReadOnlyCollection<string> AddedCast,
    IReadOnlyCollection<string> RemovedCast,
    IReadOnlyCollection<string> AddedCrew,
    IReadOnlyCollection<string> RemovedCrew,
    IReadOnlyCollection<string> AddedPeoplePhotos,
    IReadOnlyCollection<string> RemovedPeoplePhotos);

/// <summary>Current selected-library people metadata plus the newest Cast and Crew operation's colored changes.</summary>
public sealed record CastCrewOverviewItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid LibraryId,
    IReadOnlyCollection<string> Cast,
    IReadOnlyCollection<string> Crew,
    IReadOnlyCollection<CastCrewPersonPhotoDto> PeoplePhotos,
    IReadOnlyCollection<string> AddedCast,
    IReadOnlyCollection<string> RemovedCast,
    IReadOnlyCollection<string> AddedCrew,
    IReadOnlyCollection<string> RemovedCrew,
    IReadOnlyCollection<string> AddedPeoplePhotos,
    IReadOnlyCollection<string> RemovedPeoplePhotos);

/// <summary>One current Jellyfin person primary image available to display in the Cast and Crew overview.</summary>
public sealed record CastCrewPersonPhotoDto(Guid PersonId, string Name);

/// <summary>One stored TMDb title-to-title relationship for reuse by this and future plugins.</summary>
public sealed record RelatedTitleDto(
    int TmdbId,
    string Title,
    int? Year,
    string Overview,
    string? PosterUrl,
    IReadOnlyCollection<int> GenreIds,
    double? Popularity,
    double? VoteAverage,
    int? VoteCount);

/// <summary>One selected-library item's saved TMDb recommendations and similar titles, with latest-scan changes.</summary>
public sealed record MoreLikeThisOverviewItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid LibraryId,
    IReadOnlyCollection<RelatedTitleDto> Recommendations,
    IReadOnlyCollection<RelatedTitleDto> SimilarTitles,
    IReadOnlyCollection<RelatedTitleDto> RemovedRecommendations,
    IReadOnlyCollection<RelatedTitleDto> RemovedSimilarTitles,
    IReadOnlyCollection<int> AddedRecommendationIds,
    IReadOnlyCollection<int> RemovedRecommendationIds,
    IReadOnlyCollection<int> AddedSimilarTitleIds,
    IReadOnlyCollection<int> RemovedSimilarTitleIds);

/// <summary>One small page of selected-library More Like This records for responsive dashboard rendering.</summary>
public sealed record MoreLikeThisOverviewPageDto(IReadOnlyCollection<MoreLikeThisOverviewItemDto> Items, int TotalItems, int Page, int PageSize);

/// <summary>Lightweight saved-relationship count for one selected library.</summary>
public sealed record MoreLikeThisOverviewCountDto(Guid LibraryId, int TotalItems);

/// <summary>Current More Like This poster-cache usage.</summary>
public sealed record MoreLikeThisImageCacheStatus(int Count, long Bytes, int LimitMegabytes);

/// <summary>Current native production metadata plus the latest plugin-owned additions and removals.</summary>
public sealed record ProductionOverviewItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid LibraryId,
    IReadOnlyCollection<string> Companies,
    IReadOnlyCollection<string> Countries,
    IReadOnlyCollection<string> AddedCompanies,
    IReadOnlyCollection<string> RemovedCompanies,
    IReadOnlyCollection<string> AddedCountries,
    IReadOnlyCollection<string> RemovedCountries);

/// <summary>Result from a production metadata update or cleanup.</summary>
public sealed record ProductionOperationResult(int CompaniesAdded, int CountriesAdded, int CompaniesRemoved, int CountriesRemoved, int ItemsChanged);

/// <summary>Progress and result details for the dedicated selected-library production-metadata action.</summary>
public sealed class ProductionScanProgress
{
    /// <summary>Gets or sets whether the action is currently running.</summary>
    public bool IsRunning { get; set; }
    /// <summary>Gets or sets the number of selected-library Movies and Series to inspect.</summary>
    public int TotalItems { get; set; }
    /// <summary>Gets or sets the number of inspected media items.</summary>
    public int CompletedItems { get; set; }
    /// <summary>Gets or sets the number of production companies added.</summary>
    public int CompaniesAdded { get; set; }
    /// <summary>Gets or sets the number of production countries added.</summary>
    public int CountriesAdded { get; set; }
    /// <summary>Gets or sets the number of changed media items.</summary>
    public int ItemsChanged { get; set; }
    /// <summary>Gets or sets the administrator-facing progress or completion message.</summary>
    public string? Message { get; set; }
}

/// <summary>Progress and result details for the dedicated More Like This selected-library action.</summary>
public sealed class MoreLikeThisScanProgress
{
    /// <summary>Gets or sets whether a dedicated relationship load or sync is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the selected-library items planned for this action.</summary>
    public int TotalItems { get; set; }

    /// <summary>Gets or sets the selected-library items inspected.</summary>
    public int CompletedItems { get; set; }

    /// <summary>Gets or sets the number of items whose relationship record was created or refreshed.</summary>
    public int RecordsSaved { get; set; }

    /// <summary>Gets or sets the number of recommendation titles currently saved by the action.</summary>
    public int RecommendationsSaved { get; set; }

    /// <summary>Gets or sets the number of similar titles currently saved by the action.</summary>
    public int SimilarTitlesSaved { get; set; }

    /// <summary>Gets or sets the number of inspected items without a usable TMDb identifier.</summary>
    public int MissingTmdbIds { get; set; }

    /// <summary>Gets or sets the number of items whose TMDb relationship lookup failed.</summary>
    public int LookupFailures { get; set; }

    /// <summary>Gets or sets the number of valid TMDb responses that had no requested relationship titles.</summary>
    public int EmptyRelationshipResults { get; set; }

    /// <summary>Gets or sets the current administrator-facing action status.</summary>
    public string Message { get; set; } = "No Recommendation or Similar Title action is currently running.";
}

/// <summary>Progress for either dedicated Ratings or Languages selected-library action.</summary>
public sealed class SupplementalScanProgress
{
    public bool IsRunning { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int RecordsUpdated { get; set; }
    public int MissingTmdbIds { get; set; }
    public int LookupFailures { get; set; }
    public string Message { get; set; } = "No action is currently running.";
}

/// <summary>Saved rating and classification data for one selected-library Movie or Series.</summary>
public sealed record RatingsOverviewItemDto(Guid ItemId, string Name, string ItemType, Guid LibraryId, double? CommunityRating, int? VoteCount, string? OfficialRating, bool? Adult, IReadOnlyCollection<TmdbClassification> Classifications, bool ChangedInLatestAction);

/// <summary>Saved original/spoken language and translation data for one selected-library Movie or Series.</summary>
public sealed record LanguagesOverviewItemDto(Guid ItemId, string Name, string ItemType, Guid LibraryId, string? OriginalLanguage, IReadOnlyCollection<string> SpokenLanguages, IReadOnlyCollection<TmdbTranslation> Translations, bool ChangedInLatestAction);

/// <summary>A dashboard-facing summary of one library item.</summary>
public sealed record TaggedItemDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid? LibraryId,
    IReadOnlyCollection<string> Providers,
    IReadOnlyCollection<string> Networks,
    IReadOnlyCollection<string> Genres,
    IReadOnlyCollection<string> NativeGenres,
    IReadOnlyCollection<string> Keywords,
    IReadOnlyCollection<string> Collections,
    DateTimeOffset? LastCheckedUtc,
    IReadOnlyCollection<string> Sources);

/// <summary>Current item tags plus the plugin-owned tags added or removed by the most recent scan.</summary>
public sealed record LastScanItemDeltaDto(
    Guid ItemId,
    string Name,
    string ItemType,
    Guid? LibraryId,
    IReadOnlyCollection<string> Providers,
    IReadOnlyCollection<string> Networks,
    IReadOnlyCollection<string> Genres,
    IReadOnlyCollection<string> Keywords,
    IReadOnlyCollection<string> Collections,
    IReadOnlyCollection<string> AddedProviders,
    IReadOnlyCollection<string> RemovedProviders,
    IReadOnlyCollection<string> AddedNetworks,
    IReadOnlyCollection<string> RemovedNetworks,
    IReadOnlyCollection<string> AddedGenres,
    IReadOnlyCollection<string> RemovedGenres,
    IReadOnlyCollection<string> AddedKeywords,
    IReadOnlyCollection<string> RemovedKeywords,
    IReadOnlyCollection<string> AddedCollections,
    IReadOnlyCollection<string> RemovedCollections);

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
