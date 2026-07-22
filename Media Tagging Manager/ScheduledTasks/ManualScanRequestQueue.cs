using System.Collections.Concurrent;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Stores dashboard scan requests until Jellyfin's task manager starts the matching task.</summary>
public sealed class ManualScanRequestQueue
{
    private readonly ConcurrentQueue<Guid?> _requests = new();

    /// <summary>Queues one selected Jellyfin library.</summary>
    public void EnqueueLibrary(Guid libraryId) => _requests.Enqueue(libraryId);

    /// <summary>Queues all libraries currently selected in plugin settings.</summary>
    public void EnqueueAllLibraries() => _requests.Enqueue(null);

    /// <summary>Attempts to read the next dashboard request.</summary>
    public bool TryDequeue(out Guid? libraryId) => _requests.TryDequeue(out libraryId);

    /// <summary>Discards requests that have not started after an administrator cancels a scan.</summary>
    public void Clear()
    {
        while (_requests.TryDequeue(out _))
        {
        }
    }
}
