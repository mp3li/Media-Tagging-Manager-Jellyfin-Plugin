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
        serviceCollection.AddSingleton<TagBackupManager>();
        serviceCollection.AddSingleton<ProviderNetworkScanner>();
        serviceCollection.AddSingleton<MediaBrowser.Model.Tasks.IScheduledTask, RefreshAvailabilityTask>();
        serviceCollection.AddSingleton<MediaBrowser.Controller.Library.ILibraryPostScanTask, NewMediaPostScanTask>();
        serviceCollection.AddHttpClient<IAvailabilitySource, TmdbAvailabilitySource>();
        serviceCollection.AddHttpClient<IAvailabilitySource, WatchmodeAvailabilitySource>();
        serviceCollection.AddHttpClient<IAvailabilitySource, CustomJsonAvailabilitySource>();
    }
}
