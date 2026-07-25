using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Runs Dashboard-requested TMDb recommendation and similar-title loads and updates through Jellyfin's task manager.</summary>
public sealed class MoreLikeThisScanTask : IScheduledTask
{
    private readonly MoreLikeThisManager _manager;
    private readonly MoreLikeThisScanRequestQueue _requests;

    /// <summary>Initializes a new instance of the <see cref="MoreLikeThisScanTask"/> class.</summary>
    public MoreLikeThisScanTask(MoreLikeThisManager manager, MoreLikeThisScanRequestQueue requests)
    {
        _manager = manager;
        _requests = requests;
    }

    /// <inheritdoc />
    public string Name => "Load recommendations and similar titles";

    /// <inheritdoc />
    public string Key => "MediaTaggingManagerMoreLikeThis";

    /// <inheritdoc />
    public string Description => "Loads or updates TMDb recommendations and similar titles for selected libraries.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_requests.TryDequeue(out var onlyMissing))
        {
            await _manager.ScanConfiguredLibrariesAsync(onlyMissing, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
