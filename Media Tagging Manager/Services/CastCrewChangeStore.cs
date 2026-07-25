using System.Collections.Concurrent;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Keeps the most recent cast, crew, and shared-person-image additions/removals for dashboard review.</summary>
public sealed class CastCrewChangeStore
{
    private readonly ConcurrentDictionary<Guid, CastCrewChangeItemDto> _items = new();

    /// <summary>Starts a new Cast and Crew operation history.</summary>
    public void Start() => _items.Clear();

    /// <summary>Records additions made to one selected-library item.</summary>
    public void RecordAdded(BaseItem item, Guid libraryId, CastCrewManager.CastCrewItemResult result)
    {
        if (!result.CastNames.Any() && !result.CrewNames.Any() && !result.PhotoNames.Any())
        {
            return;
        }

        Upsert(item, libraryId, result.CastNames, [], result.CrewNames, [], result.PhotoNames, []);
    }

    /// <summary>Records exact plugin-owned cast or crew records removed from one selected-library item.</summary>
    public void RecordRemovedPeople(BaseItem item, Guid libraryId, IEnumerable<CastCrewOwnedAssignment> people)
    {
        var removed = people.ToArray();
        Upsert(
            item,
            libraryId,
            [],
            removed.Where(person => person.Type is PersonKind.Actor or PersonKind.GuestStar).Select(person => person.Name),
            [],
            removed.Where(person => person.Type is not PersonKind.Actor and not PersonKind.GuestStar).Select(person => person.Name),
            [],
            []);
    }

    /// <summary>Records a shared person photo removed for every selected-library item currently using that person.</summary>
    public void RecordRemovedPhoto(BaseItem item, Guid libraryId, string personName) =>
        Upsert(item, libraryId, [], [], [], [], [], [personName]);

    /// <summary>Returns the current operation's changed items, optionally narrowed to one selected library.</summary>
    public IEnumerable<CastCrewChangeItemDto> GetItems(Guid? libraryId) => _items.Values
        .Where(item => !libraryId.HasValue || item.LibraryId == libraryId.Value)
        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a changed-item overlay for one current selected-library item.</summary>
    public CastCrewChangeItemDto? GetItem(Guid itemId) => _items.GetValueOrDefault(itemId);

    private void Upsert(
        BaseItem item,
        Guid libraryId,
        IEnumerable<string> addedCast,
        IEnumerable<string> removedCast,
        IEnumerable<string> addedCrew,
        IEnumerable<string> removedCrew,
        IEnumerable<string> addedPhotos,
        IEnumerable<string> removedPhotos)
    {
        _items.AddOrUpdate(
            item.Id,
            _ => Create(item, libraryId, addedCast, removedCast, addedCrew, removedCrew, addedPhotos, removedPhotos),
            (_, current) => new CastCrewChangeItemDto(
                current.ItemId,
                current.Name,
                current.ItemType,
                current.LibraryId,
                Combine(current.AddedCast, addedCast),
                Combine(current.RemovedCast, removedCast),
                Combine(current.AddedCrew, addedCrew),
                Combine(current.RemovedCrew, removedCrew),
                Combine(current.AddedPeoplePhotos, addedPhotos),
                Combine(current.RemovedPeoplePhotos, removedPhotos)));
    }

    private static CastCrewChangeItemDto Create(BaseItem item, Guid libraryId, IEnumerable<string> addedCast, IEnumerable<string> removedCast, IEnumerable<string> addedCrew, IEnumerable<string> removedCrew, IEnumerable<string> addedPhotos, IEnumerable<string> removedPhotos) =>
        new(item.Id, item.Name, item.GetType().Name, libraryId, Combine([], addedCast), Combine([], removedCast), Combine([], addedCrew), Combine([], removedCrew), Combine([], addedPhotos), Combine([], removedPhotos));

    private static string[] Combine(IEnumerable<string> existing, IEnumerable<string> incoming) => existing
        .Concat(incoming)
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Select(static name => name.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
