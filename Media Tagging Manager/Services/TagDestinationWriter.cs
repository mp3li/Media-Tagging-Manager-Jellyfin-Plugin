using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Writes changed tags through Jellyfin's supported metadata-update workflow.</summary>
public sealed class TagDestinationWriter
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>Initializes a new instance of the <see cref="TagDestinationWriter"/> class.</summary>
    public TagDestinationWriter(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Saves an item's changed tags once through Jellyfin. Jellyfin then follows
    /// the selected library's metadata-saver policy, including NFO output when
    /// that library has it enabled.
    /// </summary>
    public Task SaveAsync(BaseItem item, CancellationToken cancellationToken) =>
        _libraryManager.UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken);
}
