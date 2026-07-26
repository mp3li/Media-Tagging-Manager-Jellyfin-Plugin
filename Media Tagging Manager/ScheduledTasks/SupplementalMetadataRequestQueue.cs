#pragma warning disable CS1591
namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Queues dashboard-requested ratings or language metadata actions.</summary>
public sealed class SupplementalMetadataRequestQueue
{
    private int _ratings;
    private int _languages;
    public void Enqueue(bool ratings) { if (ratings) Interlocked.Exchange(ref _ratings, 1); else Interlocked.Exchange(ref _languages, 1); }
    public bool TryDequeue(bool ratings) => ratings
        ? Interlocked.Exchange(ref _ratings, 0) == 1
        : Interlocked.Exchange(ref _languages, 0) == 1;
}
