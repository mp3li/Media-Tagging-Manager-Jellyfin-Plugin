#pragma warning disable CS1591
namespace Jellyfin.Plugin.MediaTaggingManager.Models;

/// <summary>Only the controls on the Main Settings tab that are saved together.</summary>
public sealed class MainSettingsRequest
{
    /// <summary>Gets or sets the selected virtual-library identifiers.</summary>
    public Guid[]? LibraryIds { get; set; }
    /// <summary>Gets or sets whether logo caching is enabled.</summary>
    public bool EnableLogoCaching { get; set; }
    /// <summary>Gets or sets the logo-cache capacity in megabytes.</summary>
    public int LogoCacheLimitMegabytes { get; set; }
    /// <summary>Gets or sets whether newly added media is scanned.</summary>
    public bool EnableNewMediaChecks { get; set; }
    /// <summary>Gets or sets whether outdated plugin-owned tags are replaced.</summary>
    public bool ReplaceManagedTags { get; set; }
    /// <summary>Gets or sets whether scheduled refreshing is enabled.</summary>
    public bool EnableAutomaticRefresh { get; set; }
    /// <summary>Gets or sets the scheduled-refresh interval in hours.</summary>
    public int RefreshIntervalHours { get; set; }
    /// <summary>Gets or sets the TMDb Read Access Token.</summary>
    public string? TmdbApiKey { get; set; }
    /// <summary>Gets or sets the Watchmode API key.</summary>
    public string? WatchmodeApiKey { get; set; }
    /// <summary>Gets or sets the Watchmode 30-day request limit.</summary>
    public int WatchmodeMonthlyLimit { get; set; }
    /// <summary>Gets or sets Watchmode's quota-reset date.</summary>
    public string? WatchmodeQuotaResetsOn { get; set; }
    /// <summary>Gets or sets the current Watchmode request usage.</summary>
    public int WatchmodeRequestsUsed { get; set; }

    /// <summary>Gets or sets whether the Main Settings save should also apply edited API controls.</summary>
    public bool UpdateApiSettings { get; set; }
}

/// <summary>Only the API-credential controls saved by the immediate API Settings button.</summary>
public sealed class ApiSettingsRequest
{
    /// <summary>Gets or sets the TMDb Read Access Token.</summary>
    public string? TmdbApiKey { get; set; }
    /// <summary>Gets or sets the Watchmode API key.</summary>
    public string? WatchmodeApiKey { get; set; }
    /// <summary>Gets or sets the Watchmode 30-day request limit.</summary>
    public int WatchmodeMonthlyLimit { get; set; }
    /// <summary>Gets or sets Watchmode's quota-reset date.</summary>
    public string? WatchmodeQuotaResetsOn { get; set; }
    /// <summary>Gets or sets the current Watchmode request usage.</summary>
    public int WatchmodeRequestsUsed { get; set; }
}

/// <summary>Only the controls on the Network and Provider Settings tab.</summary>
public sealed class ProviderNetworkSettingsRequest
{
    /// <summary>Gets or sets whether Provider tags are enabled.</summary>
    public bool TagProviders { get; set; }
    /// <summary>Gets or sets whether Network tags are enabled.</summary>
    public bool TagNetworks { get; set; }
    /// <summary>Gets or sets the TV-network streaming-app mode.</summary>
    public string? TvNetworkAppTaggingMode { get; set; }
    /// <summary>Gets or sets the selected availability regions.</summary>
    public string[]? Regions { get; set; }
    /// <summary>Gets or sets the selected Providers.</summary>
    public string[]? SelectedProviderNames { get; set; }

    /// <summary>Gets or sets whether the Provider picker was intentionally edited before this tab save.</summary>
    public bool UpdateProviderSelection { get; set; }

    /// <summary>Gets or sets the selected Networks.</summary>
    public string[]? SelectedNetworkNames { get; set; }

    /// <summary>Gets or sets whether the Network picker was intentionally edited before this tab save.</summary>
    public bool UpdateNetworkSelection { get; set; }
}

/// <summary>Only the controls on the Genres and Keywords Settings tab.</summary>
public sealed class GenreKeywordSettingsRequest
{
    /// <summary>Gets or sets whether Genre tags are enabled.</summary>
    public bool TagGenres { get; set; }
    /// <summary>Gets or sets whether Keyword tags are enabled.</summary>
    public bool TagKeywords { get; set; }
    /// <summary>Gets or sets the selected Genres.</summary>
    public string[]? SelectedGenreNames { get; set; }

    /// <summary>Gets or sets whether the Genre picker was intentionally edited before this tab save.</summary>
    public bool UpdateGenreSelection { get; set; }
}

/// <summary>Only the controls shared by the two Scheduled Tasks sections.</summary>
public sealed class ScheduledTasksSettingsRequest
{
    /// <summary>Gets or sets whether outdated plugin-owned tags are replaced.</summary>
    public bool ReplaceManagedTags { get; set; }
    /// <summary>Gets or sets whether scheduled refreshing is enabled.</summary>
    public bool EnableAutomaticRefresh { get; set; }
    /// <summary>Gets or sets the scheduled-refresh interval in hours.</summary>
    public int RefreshIntervalHours { get; set; }
}

/// <summary>Colors used to distinguish additions and removals in the latest scan view.</summary>
public sealed class LastScanColorSettingsRequest
{
    /// <summary>Gets or sets the addition color.</summary>
    public string? AddedColor { get; set; }

    /// <summary>Gets or sets the removal color.</summary>
    public string? RemovedColor { get; set; }
}

/// <summary>One edited last-scan row, including its preserved change classifications.</summary>
public sealed class LastScanDeltaUpdateRequest
{
    /// <summary>Gets or sets current provider names.</summary>
    public string[]? Providers { get; set; }
    /// <summary>Gets or sets current network names.</summary>
    public string[]? Networks { get; set; }
    /// <summary>Gets or sets current genre names.</summary>
    public string[]? Genres { get; set; }
    /// <summary>Gets or sets current keyword names.</summary>
    public string[]? Keywords { get; set; }
    /// <summary>Gets or sets current collection names.</summary>
    public string[]? Collections { get; set; }
    /// <summary>Gets or sets provider names added in the last scan.</summary>
    public string[]? AddedProviders { get; set; }
    /// <summary>Gets or sets provider names removed in the last scan.</summary>
    public string[]? RemovedProviders { get; set; }
    /// <summary>Gets or sets network names added in the last scan.</summary>
    public string[]? AddedNetworks { get; set; }
    /// <summary>Gets or sets network names removed in the last scan.</summary>
    public string[]? RemovedNetworks { get; set; }
    /// <summary>Gets or sets genre names added in the last scan.</summary>
    public string[]? AddedGenres { get; set; }
    /// <summary>Gets or sets genre names removed in the last scan.</summary>
    public string[]? RemovedGenres { get; set; }
    /// <summary>Gets or sets keyword names added in the last scan.</summary>
    public string[]? AddedKeywords { get; set; }
    /// <summary>Gets or sets keyword names removed in the last scan.</summary>
    public string[]? RemovedKeywords { get; set; }
    /// <summary>Gets or sets collection names added in the last scan.</summary>
    public string[]? AddedCollections { get; set; }
    /// <summary>Gets or sets collection names removed in the last scan.</summary>
    public string[]? RemovedCollections { get; set; }
}

/// <summary>Only the controls on the Cast and Crew Settings tab.</summary>
public sealed class CastCrewSettingsRequest
{
    /// <summary>Gets or sets whether scans may append missing TMDb cast members.</summary>
    public bool AddMissingCast { get; set; }

    /// <summary>Gets or sets the maximum total cast members retained when adding missing cast.</summary>
    public int MaximumCastMembers { get; set; }

    /// <summary>Gets or sets whether scans may fill missing cast photos returned by TMDb.</summary>
    public bool FillMissingCastPhotos { get; set; }

    /// <summary>Gets or sets whether scans may append missing TMDb crew members for selected jobs.</summary>
    public bool AddMissingCrew { get; set; }

    /// <summary>Gets or sets the TMDb crew jobs allowed to be appended.</summary>
    public string[]? SelectedCrewJobs { get; set; }

    /// <summary>Gets or sets whether scans may fill missing crew photos returned by TMDb.</summary>
    public bool FillMissingCrewPhotos { get; set; }
}

/// <summary>Only the accessible addition/removal colors on the Cast and Crew Settings tab.</summary>
public sealed class CastCrewColorSettingsRequest
{
    /// <summary>Gets or sets the color for cast, crew, and people photos added by the latest operation.</summary>
    public string? AddedColor { get; set; }

    /// <summary>Gets or sets the color for cast, crew, and people photos removed by the latest operation.</summary>
    public string? RemovedColor { get; set; }
}

/// <summary>Only the controls on the More Like This Settings tab.</summary>
public sealed class MoreLikeThisSettingsRequest
{
    /// <summary>Gets or sets whether normal scans save TMDb recommendations.</summary>
    public bool AddRecommendations { get; set; }

    /// <summary>Gets or sets whether normal scans save TMDb similar titles.</summary>
    public bool AddSimilarTitles { get; set; }

    /// <summary>Gets or sets whether poster URLs are retained for future plugin use.</summary>
    public bool SaveImageLinks { get; set; }

    /// <summary>Gets or sets whether posters are downloaded into the plugin cache.</summary>
    public bool SaveImagesToDisk { get; set; }

    /// <summary>Gets or sets the bounded poster-cache capacity in megabytes.</summary>
    public int ImageCacheLimitMegabytes { get; set; }
}

/// <summary>Accessible colors used by the More Like This relationship overview.</summary>
public sealed class MoreLikeThisColorSettingsRequest
{
    /// <summary>Gets or sets the color for relationships added by the latest scan.</summary>
    public string? AddedColor { get; set; }

    /// <summary>Gets or sets the color for relationships removed by the latest scan.</summary>
    public string? RemovedColor { get; set; }
}

/// <summary>Only the controls on the Production Companies and Countries Settings tab.</summary>
public sealed class ProductionSettingsRequest
{
    /// <summary>Gets or sets whether missing Jellyfin Studio values are filled from TMDb companies.</summary>
    public bool AddProductionCompanies { get; set; }

    /// <summary>Gets or sets whether source-supplied company logos are saved to the bounded logo cache.</summary>
    public bool SaveProductionCompanyLogos { get; set; }

    /// <summary>Gets or sets whether missing Jellyfin production-country values are filled from TMDb.</summary>
    public bool AddProductionCountries { get; set; }

    /// <summary>Gets or sets selected ISO country codes.</summary>
    public string[]? SelectedProductionCountryCodes { get; set; }

    /// <summary>Gets or sets whether the country picker was intentionally edited before saving.</summary>
    public bool UpdateProductionCountrySelection { get; set; }
}

/// <summary>Accessible colors used by the Production Companies and Countries overview.</summary>
public sealed class ProductionColorSettingsRequest
{
    /// <summary>Gets or sets the color for production metadata added by the latest operation.</summary>
    public string? AddedColor { get; set; }

    /// <summary>Gets or sets the color for production metadata removed by cleanup.</summary>
    public string? RemovedColor { get; set; }
}

/// <summary>One administrator edit to native production-company and production-country metadata.</summary>
public sealed class ProductionItemUpdateRequest
{
    /// <summary>Gets or sets the complete current Studio/company list for the item.</summary>
    public string[]? Companies { get; set; }

    /// <summary>Gets or sets the complete current production-country list for the item.</summary>
    public string[]? Countries { get; set; }
}

/// <summary>Only the controls on the Ratings Settings tab.</summary>
public sealed class RatingsSettingsRequest
{
    public bool AddCommunityRatings { get; set; }
    public bool SaveVoteCounts { get; set; }
    public bool AddAgeRatings { get; set; }
    public string[]? SelectedClassificationCountryCodes { get; set; }
    public string? PrimaryClassificationCountryCode { get; set; }
    public bool SaveAdultFlags { get; set; }
}

/// <summary>Accessible colors for the ratings and classifications review.</summary>
public sealed class RatingsColorSettingsRequest
{
    public string? AddedColor { get; set; }
    public string? RemovedColor { get; set; }
}

/// <summary>Only the controls on the Spoken Languages and Translations tab.</summary>
public sealed class LanguagesSettingsRequest
{
    public bool SaveOriginalLanguages { get; set; }
    public bool SaveSpokenLanguages { get; set; }
    public bool SaveAvailableTranslations { get; set; }
}

/// <summary>Accessible colors for the spoken languages and translations review.</summary>
public sealed class LanguagesColorSettingsRequest
{
    public string? AddedColor { get; set; }
    public string? RemovedColor { get; set; }
}
