namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Thread-safe pending marker for a dashboard-requested production-metadata action.</summary>
public sealed class ProductionScanRequestQueue
{
    private int _pending;

    /// <summary>Requests a selected-library production metadata action.</summary>
    public void Enqueue() => Interlocked.Exchange(ref _pending, 1);

    /// <summary>Consumes one pending action, if present.</summary>
    public bool TryDequeue() => Interlocked.Exchange(ref _pending, 0) == 1;
}
