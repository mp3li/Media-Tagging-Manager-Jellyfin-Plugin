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

    /// <summary>Gets or sets the legacy single availability region retained when upgrading older plugin settings.</summary>
    public string Region { get; set; } = "US";

    /// <summary>Gets or sets up to three ISO 3166-1 alpha-2 regions used for streaming availability.</summary>
    public string[] Regions { get; set; } = [];

    /// <summary>Gets or sets the optional TMDb API Read Access Token.</summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional Watchmode API key.</summary>
    public string WatchmodeApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets whether tags previously created by this plugin are removed before every update.</summary>
    public bool ReplaceManagedTags { get; set; } = true;

    /// <summary>Gets or sets the monthly Watchmode request limit selected by the server administrator.</summary>
    public int WatchmodeMonthlyLimit { get; set; } = 2500;

    /// <summary>Gets or sets the UTC year-month for which Watchmode requests are tracked.</summary>
    public string WatchmodeUsageMonth { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of Watchmode requests used during <see cref="WatchmodeUsageMonth"/>.</summary>
    public int WatchmodeRequestsUsed { get; set; }
}
