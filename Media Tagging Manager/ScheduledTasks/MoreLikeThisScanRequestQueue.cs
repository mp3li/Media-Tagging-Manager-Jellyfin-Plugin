namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Thread-safe pending action marker for dedicated TMDb relationship loading and synchronization.</summary>
public sealed class MoreLikeThisScanRequestQueue
{
    private int _action;

    /// <summary>Requests an initial load for selected-library titles that do not yet have a relationship record.</summary>
    public void EnqueueLoad() => Interlocked.Exchange(ref _action, 1);

    /// <summary>Requests a full selected-library refresh that reconciles saved relationship records with TMDb.</summary>
    public void EnqueueSync() => Interlocked.Exchange(ref _action, 2);

    /// <summary>Consumes one pending action, if present.</summary>
    public bool TryDequeue(out bool onlyMissing)
    {
        var action = Interlocked.Exchange(ref _action, 0);
        onlyMissing = action == 1;
        return action is 1 or 2;
    }
}
