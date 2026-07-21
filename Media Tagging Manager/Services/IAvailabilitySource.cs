using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Looks up current availability and network information for one media identity.</summary>
public interface IAvailabilitySource
{
    /// <summary>Gets the source display name.</summary>
    string Name { get; }

    /// <summary>Looks up classifications. Sources should return an empty result when IDs/keys are unavailable.</summary>
    Task<SourceLookupResult> LookupAsync(ExternalIds ids, CancellationToken cancellationToken);
}
