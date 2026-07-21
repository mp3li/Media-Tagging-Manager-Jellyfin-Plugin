using System.Collections.Concurrent;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps scan status and item classifications in the plugin data directory.</summary>
public sealed class ScanStateStore
{
    private readonly ConcurrentDictionary<Guid, TaggedItemDto> _items = new();
    private readonly object _progressLock = new();
    private ScanProgress _progress = new();

    /// <summary>Gets a snapshot of current scan progress.</summary>
    public ScanProgress GetProgress()
    {
        lock (_progressLock)
        {
            return new ScanProgress
            {
                IsRunning = _progress.IsRunning,
                Total = _progress.Total,
                Completed = _progress.Completed,
                CurrentTitle = _progress.CurrentTitle,
                StartedUtc = _progress.StartedUtc,
                EstimatedRemaining = _progress.EstimatedRemaining,
                LastError = _progress.LastError
            };
        }
    }

    /// <summary>Starts a progress session.</summary>
    public void Start(int total)
    {
        lock (_progressLock)
        {
            _progress = new ScanProgress { IsRunning = true, Total = total, StartedUtc = DateTimeOffset.UtcNow };
        }
    }

    /// <summary>Updates the active scan's visible progress.</summary>
    public void Report(int completed, string title)
    {
        lock (_progressLock)
        {
            _progress.Completed = completed;
            _progress.CurrentTitle = title;
            if (_progress.StartedUtc is { } started && completed > 0)
            {
                var elapsed = DateTimeOffset.UtcNow - started;
                _progress.EstimatedRemaining = TimeSpan.FromTicks(elapsed.Ticks * (_progress.Total - completed) / completed);
            }
        }
    }

    /// <summary>Marks a progress session finished.</summary>
    public void Complete(string? error = null)
    {
        lock (_progressLock)
        {
            _progress.IsRunning = false;
            _progress.CurrentTitle = string.Empty;
            _progress.EstimatedRemaining = TimeSpan.Zero;
            _progress.LastError = error;
        }
    }

    /// <summary>Upserts a dashboard item.</summary>
    public void Save(TaggedItemDto item) => _items[item.ItemId] = item;

    /// <summary>Gets dashboard items, optionally restricted to a library.</summary>
    public IEnumerable<TaggedItemDto> GetItems(Guid? libraryId) => _items.Values
        .Where(item => libraryId is null || item.LibraryId == libraryId)
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
}
