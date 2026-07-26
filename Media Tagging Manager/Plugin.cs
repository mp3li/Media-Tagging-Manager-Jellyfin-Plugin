using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MediaTaggingManager;

/// <summary>Registers the Providers &amp; Networks Tagger plugin with Jellyfin.</summary>
public sealed class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    private readonly object _configurationLock = new();
    /// <summary>Gets the singleton plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Media Tagging Manager Jellyfin Plugin";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c7b639bd-55c5-4694-aa8e-32c816048da8");

    /// <summary>
    /// Applies one complete, serialized configuration update using Jellyfin's
    /// supported configuration-update API. Callers receive a private copy to
    /// mutate, so one dashboard request or background task cannot overwrite an
    /// unrelated setting with a stale in-memory value.
    /// </summary>
    public Configuration.PluginConfiguration UpdateConfiguration(Action<Configuration.PluginConfiguration> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_configurationLock)
        {
            var updated = CloneConfiguration(Configuration);
            update(updated);
            base.UpdateConfiguration(updated);
            return updated;
        }
    }

    /// <summary>Persists the current configuration through Jellyfin's supported update API.</summary>
    public void SaveCurrentConfiguration() => UpdateConfiguration(_ => { });

    private static Configuration.PluginConfiguration CloneConfiguration(Configuration.PluginConfiguration source) => new()
    {
        LibraryIds = (source.LibraryIds ?? []).ToArray(),
        TagProviders = source.TagProviders,
        TagNetworks = source.TagNetworks,
        TagGenres = source.TagGenres,
        TagKeywords = source.TagKeywords,
        EnableAutomaticRefresh = source.EnableAutomaticRefresh,
        EnableNewMediaChecks = source.EnableNewMediaChecks,
        LastIncomingMediaCheckUtc = source.LastIncomingMediaCheckUtc,
        RefreshIntervalHours = source.RefreshIntervalHours,
        Region = source.Region,
        Regions = (source.Regions ?? []).ToArray(),
        TmdbApiKey = source.TmdbApiKey,
        WatchmodeApiKey = source.WatchmodeApiKey,
        ReplaceManagedTags = source.ReplaceManagedTags,
        SelectedProviderNames = (source.SelectedProviderNames ?? []).ToArray(),
        SelectedNetworkNames = (source.SelectedNetworkNames ?? []).ToArray(),
        SelectedGenreNames = (source.SelectedGenreNames ?? []).ToArray(),
        RestrictProvidersToSelected = source.RestrictProvidersToSelected,
        RestrictNetworksToSelected = source.RestrictNetworksToSelected,
        TvNetworkAppTaggingMode = source.TvNetworkAppTaggingMode,
        EnableLogoCaching = source.EnableLogoCaching,
        LogoCacheLimitMegabytes = source.LogoCacheLimitMegabytes,
        LastScanAddedTagColor = source.LastScanAddedTagColor,
        LastScanRemovedTagColor = source.LastScanRemovedTagColor,
        UnknownTagMappings = (source.UnknownTagMappings ?? []).Select(mapping => new Configuration.UnknownTagMapping
        {
            Kind = mapping.Kind,
            Name = mapping.Name,
            OfficialName = mapping.OfficialName
        }).ToList(),
        KnownProviderNames = (source.KnownProviderNames ?? []).ToArray(),
        KnownNetworkNames = (source.KnownNetworkNames ?? []).ToArray(),
        WatchmodeMonthlyLimit = source.WatchmodeMonthlyLimit,
        WatchmodeQuotaResetsOn = source.WatchmodeQuotaResetsOn,
        WatchmodeUsageCycleStart = source.WatchmodeUsageCycleStart,
        WatchmodeRequestsUsed = source.WatchmodeRequestsUsed,
        AddMissingCast = source.AddMissingCast,
        MaximumCastMembers = source.MaximumCastMembers,
        FillMissingCastPhotos = source.FillMissingCastPhotos,
        AddMissingCrew = source.AddMissingCrew,
        SelectedCrewJobs = (source.SelectedCrewJobs ?? []).ToArray(),
        FillMissingCrewPhotos = source.FillMissingCrewPhotos,
        CastCrewAddedColor = source.CastCrewAddedColor,
        CastCrewRemovedColor = source.CastCrewRemovedColor,
        AddRecommendations = source.AddRecommendations,
        AddSimilarTitles = source.AddSimilarTitles,
        SaveMoreLikeThisImageLinks = source.SaveMoreLikeThisImageLinks,
        SaveMoreLikeThisImagesToDisk = source.SaveMoreLikeThisImagesToDisk,
        MoreLikeThisImageCacheLimitMegabytes = source.MoreLikeThisImageCacheLimitMegabytes,
        MoreLikeThisAddedColor = source.MoreLikeThisAddedColor,
        MoreLikeThisRemovedColor = source.MoreLikeThisRemovedColor,
        AddProductionCompanies = source.AddProductionCompanies,
        SaveProductionCompanyLogos = source.SaveProductionCompanyLogos,
        AddProductionCountries = source.AddProductionCountries,
        SelectedProductionCountryCodes = (source.SelectedProductionCountryCodes ?? []).ToArray(),
        ProductionAddedColor = source.ProductionAddedColor,
        ProductionRemovedColor = source.ProductionRemovedColor,
        AddCommunityRatings = source.AddCommunityRatings,
        SaveVoteCounts = source.SaveVoteCounts,
        AddAgeRatings = source.AddAgeRatings,
        SelectedClassificationCountryCodes = (source.SelectedClassificationCountryCodes ?? []).ToArray(),
        PrimaryClassificationCountryCode = source.PrimaryClassificationCountryCode,
        SaveAdultFlags = source.SaveAdultFlags,
        RatingsAddedColor = source.RatingsAddedColor,
        RatingsRemovedColor = source.RatingsRemovedColor,
        SaveOriginalLanguages = source.SaveOriginalLanguages,
        SaveSpokenLanguages = source.SaveSpokenLanguages,
        SaveAvailableTranslations = source.SaveAvailableTranslations,
        LanguagesAddedColor = source.LanguagesAddedColor,
        LanguagesRemovedColor = source.LanguagesRemovedColor
    };

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            // Keep the catalog/plugin title descriptive, while using the concise
            // label requested for Jellyfin's Dashboard plugin menu.
            Name = "Media Tagging Manager",
            DisplayName = "Media Tagging Manager",
            EnableInMainMenu = true,
            EmbeddedResourcePath = "Jellyfin.Plugin.MediaTaggingManager.Web.configPage.html"
        }
    ];
}
