namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Thread-safe single pending request marker for the dedicated Jellyfin-managed people-photo task.</summary>
public sealed class CastCrewPhotoScanRequestQueue
{
    private int _requested;

    /// <summary>Requests one selected-library person-photo scan.</summary>
    public void Enqueue() => Interlocked.Exchange(ref _requested, 1);

    /// <summary>Consumes the pending request if one exists.</summary>
    public bool TryDequeue() => Interlocked.Exchange(ref _requested, 0) == 1;

    /// <summary>Clears a request that has not started.</summary>
    public void Clear() => Interlocked.Exchange(ref _requested, 0);
}
