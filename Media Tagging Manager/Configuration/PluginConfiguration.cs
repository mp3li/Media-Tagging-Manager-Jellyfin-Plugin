using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaTaggingManager.Configuration;

/// <summary>Persisted administrator settings for the plugin.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the libraries the scanner may update. Empty means none, never all.</summary>
    public Guid[] LibraryIds { get; set; } = [];

    /// <summary>Gets or sets whether tags are saved to Jellyfin's metadata database.</summary>
    public bool SaveTagsToJellyfin { get; set; } = true;

    /// <summary>Gets or sets whether tags are also saved through each library's configured NFO metadata saver.</summary>
    public bool SaveTagsToNfoFiles { get; set; }

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

    /// <summary>Gets or sets provider names selected for an optional future-scan allow-list.</summary>
    public string[] SelectedProviderNames { get; set; } = [];

    /// <summary>Gets or sets network names selected for an optional future-scan allow-list.</summary>
    public string[] SelectedNetworkNames { get; set; } = [];

    /// <summary>Gets or sets whether provider tags are restricted to <see cref="SelectedProviderNames"/>.</summary>
    public bool RestrictProvidersToSelected { get; set; }

    /// <summary>Gets or sets how a television network's own streaming app is represented in new tags.</summary>
    public string TvNetworkAppTaggingMode { get; set; } = "NetworkOnly";

    /// <summary>Gets or sets whether network tags are restricted to <see cref="SelectedNetworkNames"/>.</summary>
    public bool RestrictNetworksToSelected { get; set; }

    /// <summary>Gets or sets whether source and administrator-supplied logos are cached and displayed.</summary>
    public bool EnableLogoCaching { get; set; } = true;

    /// <summary>Gets or sets administrator-defined canonical names for otherwise unknown Provider or Network tags.</summary>
    public List<UnknownTagMapping> UnknownTagMappings { get; set; } = [];

    /// <summary>Gets or sets provider names previously discovered by scans for the selection controls.</summary>
    public string[] KnownProviderNames { get; set; } = [];

    /// <summary>Gets or sets network names previously discovered by scans for the selection controls.</summary>
    public string[] KnownNetworkNames { get; set; } = [];

    /// <summary>Gets or sets the Watchmode request limit selected by the server administrator for each 30-day quota cycle.</summary>
    public int WatchmodeMonthlyLimit { get; set; } = 2500;

    /// <summary>Gets or sets Watchmode's displayed quota-reset date in ISO format.</summary>
    public string WatchmodeQuotaResetsOn { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO start date of the Watchmode 30-day cycle for the stored usage counter.</summary>
    public string WatchmodeUsageCycleStart { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of Watchmode requests used during <see cref="WatchmodeUsageCycleStart"/>.</summary>
    public int WatchmodeRequestsUsed { get; set; }
}

/// <summary>An administrator-defined canonical name for an external Provider or Network tag.</summary>
public sealed class UnknownTagMapping
{
    /// <summary>Gets or sets the tag kind: Provider or Network.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the currently tagged name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the official/canonical display name.</summary>
    public string OfficialName { get; set; } = string.Empty;
}
