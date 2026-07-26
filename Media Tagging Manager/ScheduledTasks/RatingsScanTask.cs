#pragma warning disable CS1591
using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;
namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;
/// <summary>Runs the dashboard-requested ratings action through Jellyfin's task manager.</summary>
public sealed class RatingsScanTask(SupplementalMetadataManager manager, SupplementalMetadataRequestQueue requests) : IScheduledTask
{
    public string Name => "Load ratings and classifications";
    public string Key => "MediaTaggingManagerRatings";
    public string Description => "Loads TMDb community ratings and country classifications for selected libraries.";
    public string Category => "Library";
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) { if (requests.TryDequeue(true)) await manager.ScanConfiguredLibrariesAsync(true, progress, cancellationToken).ConfigureAwait(false); }
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
