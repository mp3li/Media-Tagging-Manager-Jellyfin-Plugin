using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Runs a dashboard-requested selected-library production metadata action through Jellyfin's task manager.</summary>
public sealed class ProductionScanTask : IScheduledTask
{
    private readonly ProductionManager _manager;
    private readonly ProductionScanRequestQueue _requests;

    /// <summary>Initializes a new instance of the <see cref="ProductionScanTask"/> class.</summary>
    public ProductionScanTask(ProductionManager manager, ProductionScanRequestQueue requests)
    {
        _manager = manager;
        _requests = requests;
    }

    /// <inheritdoc />
    public string Name => "Load production companies and countries";
    /// <inheritdoc />
    public string Key => "MediaTaggingManagerProduction";
    /// <inheritdoc />
    public string Description => "Loads TMDb production companies and countries for selected libraries.";
    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_requests.TryDequeue())
        {
            await _manager.ScanConfiguredLibrariesAsync(progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
