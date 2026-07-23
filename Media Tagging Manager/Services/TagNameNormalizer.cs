using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Normalizes only explicit provider spelling aliases that represent the same service.</summary>
public static class TagNameNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> ProviderAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Apple TV Plus"] = "Apple TV+",
            ["Disney Plus"] = "Disney+",
            ["Disney +"] = "Disney+",
            ["Discovery Plus"] = "Discovery+",
            ["Discovery +"] = "Discovery+"
        };

    /// <summary>Returns the canonical display name for a safe, explicit alias; otherwise preserves the source name.</summary>
    public static string Normalize(TagKind kind, string name)
    {
        var trimmed = name.Trim();
        return kind == TagKind.Provider && ProviderAliases.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed;
    }
}
