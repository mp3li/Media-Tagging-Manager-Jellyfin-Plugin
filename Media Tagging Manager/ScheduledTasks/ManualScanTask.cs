using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Executes scan requests submitted by the plugin dashboard through Jellyfin's task manager.</summary>
public sealed class ManualScanTask : IScheduledTask
{
    private readonly ProviderNetworkScanner _scanner;
    private readonly ManualScanRequestQueue _requests;

    /// <summary>Initializes a new instance of the <see cref="ManualScanTask"/> class.</summary>
    public ManualScanTask(ProviderNetworkScanner scanner, ManualScanRequestQueue requests)
    {
        _scanner = scanner;
        _requests = requests;
    }

    /// <inheritdoc />
    public string Name => "Scan provider and network tags";

    /// <inheritdoc />
    public string Key => "MediaTaggingManagerManualScan";

    /// <inheritdoc />
    public string Description => "Runs a scan requested from Media Tagging Manager's dashboard page.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        while (_requests.TryDequeue(out var libraryId))
        {
            if (libraryId is { } id)
            {
                await _scanner.ScanLibraryAsync(id, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _scanner.ScanConfiguredLibrariesAsync(progress, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
