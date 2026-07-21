using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaTaggingManager.Configuration;

/// <summary>Persisted administrator settings for the plugin.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the libraries the scanner may update. Empty means none, never all.</summary>
    public Guid[] LibraryIds { get; set; } = [];

    /// <summary>Gets or sets whether streaming-provider tags are written.</summary>
    public bool TagProviders { get; set; } = true;

    /// <summary>Gets or sets whether broadcaster/studio-network tags are written.</summary>
    public bool TagNetworks { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether automatic refresh is enabled.</summary>
    public bool EnableAutomaticRefresh { get; set; }

    /// <summary>Gets or sets a value indicating whether newly added media is checked after a Jellyfin library scan.</summary>
    public bool EnableNewMediaChecks { get; set; }

    /// <summary>Gets or sets the UTC time through which incoming media has been considered.</summary>
    public DateTime? LastIncomingMediaCheckUtc { get; set; }

    /// <summary>Gets or sets the automatic refresh period in hours.</summary>
    public int RefreshIntervalHours { get; set; } = 168;

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 region used for streaming availability.</summary>
    public string Region { get; set; } = "US";

    /// <summary>Gets or sets the optional TMDb API Read Access Token.</summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional Watchmode API key.</summary>
    public string WatchmodeApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets user-configured HTTP JSON sources.</summary>
    public List<CustomSourceConfiguration> CustomSources { get; set; } = [];

    /// <summary>Gets or sets whether tags previously created by this plugin are removed before every update.</summary>
    public bool ReplaceManagedTags { get; set; } = true;

    /// <summary>Gets or sets the maximum number of titles checked in parallel.</summary>
    public int MaxConcurrentLookups { get; set; } = 3;
}

/// <summary>An administrator-defined provider/network JSON endpoint.</summary>
public sealed class CustomSourceConfiguration
{
    /// <summary>Gets or sets a human-friendly source name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL template. Tokens: {tmdb}, {imdb}, {type}, {region}.</summary>
    public string UrlTemplate { get; set; } = string.Empty;

    /// <summary>Gets or sets a bearer token or API key sent as Authorization.</summary>
    public string Authorization { get; set; } = string.Empty;

    /// <summary>Gets or sets the dot-separated JSON path containing provider names.</summary>
    public string ProviderPath { get; set; } = "providers";

    /// <summary>Gets or sets the dot-separated JSON path containing network names.</summary>
    public string NetworkPath { get; set; } = "networks";

    /// <summary>Gets or sets a value indicating whether this source is queried.</summary>
    public bool Enabled { get; set; } = true;
}
