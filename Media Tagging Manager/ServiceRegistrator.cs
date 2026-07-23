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
        serviceCollection.AddSingleton<ProviderNetworkScanner>();
        serviceCollection.AddSingleton<ManualScanRequestQueue>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, RefreshAvailabilityTask>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, ManualScanTask>();
        serviceCollection.AddSingleton<MediaBrowser.Controller.Library.ILibraryPostScanTask, NewMediaPostScanTask>();
        serviceCollection.AddHttpClient<TmdbAvailabilitySource>();
        serviceCollection.AddHttpClient<WatchmodeAvailabilitySource>();
        serviceCollection.AddSingleton<IAvailabilitySource>(serviceProvider => serviceProvider.GetRequiredService<TmdbAvailabilitySource>());
        serviceCollection.AddSingleton<IAvailabilitySource>(serviceProvider => serviceProvider.GetRequiredService<WatchmodeAvailabilitySource>());
    }
}
