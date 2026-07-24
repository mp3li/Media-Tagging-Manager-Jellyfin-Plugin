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
    /// <summary>Gets or sets the selected Networks.</summary>
    public string[]? SelectedNetworkNames { get; set; }
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
