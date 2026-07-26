#pragma warning disable CS1591
using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Model.Tasks;
namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;
/// <summary>Runs the dashboard-requested language and translation action through Jellyfin's task manager.</summary>
public sealed class LanguagesScanTask(SupplementalMetadataManager manager, SupplementalMetadataRequestQueue requests) : IScheduledTask
{
    public string Name => "Load spoken languages and translations";
    public string Key => "MediaTaggingManagerLanguages";
    public string Description => "Loads TMDb language and translation data for selected libraries.";
    public string Category => "Library";
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) { if (requests.TryDequeue(false)) await manager.ScanConfiguredLibrariesAsync(false, progress, cancellationToken).ConfigureAwait(false); }
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
