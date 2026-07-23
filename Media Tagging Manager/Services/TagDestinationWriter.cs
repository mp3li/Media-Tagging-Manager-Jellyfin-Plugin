using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Writes changed tags only to the administrator-selected Jellyfin metadata destinations.</summary>
public sealed class TagDestinationWriter
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;

    /// <summary>Initializes a new instance of the <see cref="TagDestinationWriter"/> class.</summary>
    public TagDestinationWriter(ILibraryManager libraryManager, IProviderManager providerManager)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
    }

    /// <summary>Ensures that the selected destinations are valid for the supplied libraries before changes begin.</summary>
    public void Validate(PluginConfiguration configuration, IEnumerable<Guid> libraryIds)
    {
        if (!configuration.SaveTagsToJellyfin && !configuration.SaveTagsToNfoFiles)
        {
            throw new InvalidOperationException("Select at least one tag destination: Here in Jellyfin or In my NFO files.");
        }

        if (!configuration.SaveTagsToNfoFiles)
        {
            return;
        }

        foreach (var libraryId in libraryIds.Distinct())
        {
            var library = _libraryManager.GetItemById(libraryId)
                ?? throw new InvalidOperationException("A selected Jellyfin library could not be found.");
            var options = _libraryManager.GetLibraryOptions(library);
            if (!options.SaveLocalMetadata || !(options.MetadataSavers ?? []).Contains("Nfo", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"NFO tags are selected, but the library '{library.Name}' is not configured to save NFO metadata.");
            }
        }
    }

    /// <summary>Saves an item's changed tags using each configured destination.</summary>
    public async Task SaveAsync(BaseItem item, PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        if (configuration.SaveTagsToJellyfin)
        {
            await _libraryManager.UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        }

        if (configuration.SaveTagsToNfoFiles)
        {
            await _providerManager.SaveMetadataAsync(item, ItemUpdateType.MetadataEdit).ConfigureAwait(false);
        }
    }
}
