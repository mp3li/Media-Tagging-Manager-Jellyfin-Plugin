using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>A Jellyfin scheduled task for refreshing provider and network data.</summary>
public sealed class RefreshAvailabilityTask : IScheduledTask
{
    private readonly ProviderNetworkScanner _scanner;

    /// <summary>Initializes a new instance of the <see cref="RefreshAvailabilityTask"/> class.</summary>
    public RefreshAvailabilityTask(ProviderNetworkScanner scanner) => _scanner = scanner;

    /// <inheritdoc />
    public string Name => "Refresh provider and network tags";

    /// <inheritdoc />
    public string Key => "MediaTaggingManagerRefresh";

    /// <inheritdoc />
    public string Description => "Checks enabled sources and updates configured libraries with current providers and networks.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) =>
        _scanner.ScanConfiguredLibrariesAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null || !configuration.EnableAutomaticRefresh)
        {
            return [];
        }

        return [new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.IntervalTrigger,
            IntervalTicks = TimeSpan.FromHours(Math.Clamp(configuration.RefreshIntervalHours, 6, 8760)).Ticks
        }];
    }
}
