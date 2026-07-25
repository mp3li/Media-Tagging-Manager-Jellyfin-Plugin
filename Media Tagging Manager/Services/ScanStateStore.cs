using System.Collections.Concurrent;
using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps the current in-memory scan status and item classifications for the dashboard.</summary>
public sealed class ScanStateStore
{
    private readonly ConcurrentDictionary<Guid, TaggedItemDto> _items = new();
    private readonly ConcurrentDictionary<Guid, LastScanItemDeltaDto> _lastScanItems = new();
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
                TagsAdded = _progress.TagsAdded,
                MediaItemsTagged = _progress.MediaItemsTagged,
                LastError = _progress.LastError,
                TmdbNetworkTagsReturned = _progress.TmdbNetworkTagsReturned,
                WatchmodeNetworkTagsReturned = _progress.WatchmodeNetworkTagsReturned,
                NetworkTagsFilteredBySelection = _progress.NetworkTagsFilteredBySelection,
                NetworkTagsEligibleForApplication = _progress.NetworkTagsEligibleForApplication,
                NetworkTagsAlreadyPresent = _progress.NetworkTagsAlreadyPresent,
                NetworkTagsSuppressedByStreamingAppSetting = _progress.NetworkTagsSuppressedByStreamingAppSetting,
                NetworkTagsAdded = _progress.NetworkTagsAdded,
                TmdbNetworkLookupFailures = _progress.TmdbNetworkLookupFailures,
                WatchmodeNetworkFallbackAttempts = _progress.WatchmodeNetworkFallbackAttempts,
                WatchmodeNetworkLookupFailures = _progress.WatchmodeNetworkLookupFailures
            };
        }
    }

    /// <summary>Starts a progress session.</summary>
    public void Start(int total)
    {
        lock (_progressLock)
        {
            _progress = new ScanProgress { IsRunning = true, Total = total, StartedUtc = DateTimeOffset.UtcNow };
            _lastScanItems.Clear();
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

    /// <summary>Records tags newly added to one item while a scan is active.</summary>
    public void RecordTagAdditions(int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_progressLock)
        {
            if (!_progress.IsRunning)
            {
                return;
            }

            _progress.TagsAdded += count;
            _progress.MediaItemsTagged++;
        }
    }

    /// <summary>Records an auditable Network-source outcome without changing scan success state.</summary>
    public void RecordNetworkOutcome(
        int tmdbReturned,
        int watchmodeReturned,
        int filteredBySelection,
        bool tmdbLookupFailed,
        bool watchmodeFallbackAttempted,
        bool watchmodeLookupFailed)
    {
        lock (_progressLock)
        {
            if (!_progress.IsRunning)
            {
                return;
            }

            _progress.TmdbNetworkTagsReturned += Math.Max(0, tmdbReturned);
            _progress.WatchmodeNetworkTagsReturned += Math.Max(0, watchmodeReturned);
            _progress.NetworkTagsFilteredBySelection += Math.Max(0, filteredBySelection);
            if (tmdbLookupFailed)
            {
                _progress.TmdbNetworkLookupFailures++;
            }

            if (watchmodeFallbackAttempted)
            {
                _progress.WatchmodeNetworkFallbackAttempts++;
            }

            if (watchmodeLookupFailed)
            {
                _progress.WatchmodeNetworkLookupFailures++;
            }
        }
    }

    /// <summary>Records final Network-candidate handling after the same filters and de-duplication used for a Jellyfin tag write.</summary>
    public void RecordNetworkApplication(int eligible, int alreadyPresent, int suppressedByStreamingAppSetting, int added)
    {
        lock (_progressLock)
        {
            if (!_progress.IsRunning)
            {
                return;
            }

            _progress.NetworkTagsEligibleForApplication += Math.Max(0, eligible);
            _progress.NetworkTagsAlreadyPresent += Math.Max(0, alreadyPresent);
            _progress.NetworkTagsSuppressedByStreamingAppSetting += Math.Max(0, suppressedByStreamingAppSetting);
            _progress.NetworkTagsAdded += Math.Max(0, added);
        }
    }

    /// <summary>Upserts a dashboard item.</summary>
    public void Save(TaggedItemDto item) => _items[item.ItemId] = item;

    /// <summary>Records actual plugin-owned additions and removals for the active scan only.</summary>
    public void RecordLastScanChange(TaggedItemDto item, IEnumerable<string> addedTags, IEnumerable<string> removedTags)
    {
        lock (_progressLock)
        {
            if (!_progress.IsRunning)
            {
                return;
            }
        }

        var added = GroupTagNames(addedTags);
        var removed = GroupTagNames(removedTags);
        if (added.Values.All(static names => names.Length == 0) && removed.Values.All(static names => names.Length == 0))
        {
            return;
        }

        _lastScanItems[item.ItemId] = new LastScanItemDeltaDto(
            item.ItemId,
            item.Name,
            item.ItemType,
            item.LibraryId,
            item.Providers,
            item.Networks,
            item.Genres,
            item.Keywords,
            item.Collections,
            added[TagKind.Provider],
            removed[TagKind.Provider],
            added[TagKind.Network],
            removed[TagKind.Network],
            added[TagKind.Genre],
            removed[TagKind.Genre],
            added[TagKind.Keyword],
            removed[TagKind.Keyword],
            added[TagKind.Collection],
            removed[TagKind.Collection]);
    }

    /// <summary>Gets changed items from the most recently started scan.</summary>
    public IEnumerable<LastScanItemDeltaDto> GetLastScanItems(Guid? libraryId) => _lastScanItems.Values
        .Where(item => libraryId is null || item.LibraryId == libraryId)
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets one changed item from the most recent scan.</summary>
    public LastScanItemDeltaDto? GetLastScanItem(Guid itemId) => _lastScanItems.GetValueOrDefault(itemId);

    /// <summary>Updates an in-memory last-scan row after an administrator edits that row.</summary>
    public bool UpdateLastScanItem(LastScanItemDeltaDto item)
    {
        if (!_lastScanItems.ContainsKey(item.ItemId))
        {
            return false;
        }

        _lastScanItems[item.ItemId] = item;
        return true;
    }

    /// <summary>Gets dashboard items, optionally restricted to a library.</summary>
    public IEnumerable<TaggedItemDto> GetItems(Guid? libraryId) => _items.Values
        .Where(item => libraryId is null || item.LibraryId == libraryId)
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<TagKind, string[]> GroupTagNames(IEnumerable<string> tags)
    {
        var grouped = Enum.GetValues<TagKind>().ToDictionary(kind => kind, _ => new List<string>());
        foreach (var tag in tags)
        {
            if (TagNaming.TryGetKind(tag, out var kind))
            {
                grouped[kind].Add(tag[TagNaming.Prefix(kind).Length..]);
            }
        }

        return grouped.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
