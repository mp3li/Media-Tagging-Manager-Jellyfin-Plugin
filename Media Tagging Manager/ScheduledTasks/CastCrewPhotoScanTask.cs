using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Runs the Dashboard-requested missing cast-and-crew photo scan through Jellyfin's task manager.</summary>
public sealed class CastCrewPhotoScanTask : IScheduledTask
{
    private readonly CastCrewManager _manager;
    private readonly CastCrewPhotoScanRequestQueue _requests;

    /// <summary>Initializes a new instance of the task.</summary>
    public CastCrewPhotoScanTask(CastCrewManager manager, CastCrewPhotoScanRequestQueue requests)
    {
        _manager = manager;
        _requests = requests;
    }

    /// <inheritdoc />
    public string Name => "Scan missing cast and crew photos";

    /// <inheritdoc />
    public string Key => "MediaTaggingManagerCastCrewPhotos";

    /// <inheritdoc />
    public string Description => "Fills missing Jellyfin person photos from TMDb for selected library cast and crew.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_requests.TryDequeue())
        {
            await _manager.ScanMissingPhotosAsync(progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
