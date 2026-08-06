using Jellyfin.Plugin.MediaTaggingManager.Services;
using Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediaTaggingManager;

/// <summary>Registers plugin services with Jellyfin's dependency-injection container.</summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ScanStateStore>();
        serviceCollection.AddSingleton<TmdbRequestGate>();
        serviceCollection.AddSingleton<TagDestinationWriter>();
        serviceCollection.AddSingleton<TagBackupManager>();
        serviceCollection.AddSingleton<WatchmodeQuotaTracker>();
        serviceCollection.AddSingleton<ProviderNetworkLogoCache>();
        serviceCollection.AddSingleton<LogoLoadStateStore>();
        serviceCollection.AddSingleton<NetworkCatalogCache>();
        serviceCollection.AddSingleton<CastCrewStateStore>();
        serviceCollection.AddSingleton<MoreLikeThisStateStore>();
        serviceCollection.AddSingleton<ProductionStateStore>();
        serviceCollection.AddSingleton<SupplementalMetadataStateStore>();
        serviceCollection.AddSingleton<CastCrewChangeStore>();
        serviceCollection.AddSingleton<CastCrewOwnershipStore>();
        serviceCollection.AddSingleton<ProductionOwnershipStore>();
        serviceCollection.AddSingleton<TagOwnershipStore>();
        serviceCollection.AddSingleton<CastCrewManager>();
        serviceCollection.AddSingleton<ProductionManager>();
        serviceCollection.AddSingleton<MoreLikeThisManager>();
        serviceCollection.AddSingleton<SupplementalMetadataManager>();
        serviceCollection.AddSingleton<ProviderNetworkScanner>();
        serviceCollection.AddSingleton<ManualScanRequestQueue>();
        serviceCollection.AddSingleton<CastCrewPhotoScanRequestQueue>();
        serviceCollection.AddSingleton<MoreLikeThisScanRequestQueue>();
        serviceCollection.AddSingleton<ProductionScanRequestQueue>();
        serviceCollection.AddSingleton<SupplementalMetadataRequestQueue>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, RefreshAvailabilityTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, ManualScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, CastCrewPhotoScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, MoreLikeThisScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, ProductionScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, RatingsScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, LanguagesScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Controller.Library.ILibraryPostScanTask, NewMediaPostScanTask>();
        serviceCollection.AddHttpClient<TmdbAvailabilitySource>();
        serviceCollection.AddHttpClient<WatchmodeAvailabilitySource>();
        serviceCollection.AddSingleton<IAvailabilitySource>(serviceProvider => serviceProvider.GetRequiredService<TmdbAvailabilitySource>());
        serviceCollection.AddSingleton<IAvailabilitySource>(serviceProvider => serviceProvider.GetRequiredService<WatchmodeAvailabilitySource>());
    }
}
