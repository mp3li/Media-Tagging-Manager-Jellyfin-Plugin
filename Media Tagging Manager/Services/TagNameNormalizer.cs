using Jellyfin.Plugin.MediaTaggingManager.Models;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Normalizes explicit provider aliases and, when requested, documented provider-family variants.</summary>
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

    // These are intentionally explicit rather than fuzzy. Grouping is an opt-in
    // display preference; when it is off, a source's exact provider distinction
    // remains available to administrators.
    private static readonly IReadOnlyDictionary<string, string> ProviderVariantGroups =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Netflix"] = "Netflix",
            ["Netflix Kids"] = "Netflix",
            ["Netflix Standard with Ads"] = "Netflix",
            ["Apple TV"] = "Apple TV",
            ["Apple TV+"] = "Apple TV",
            ["Apple TV Store"] = "Apple TV",
            ["Apple TV Channels"] = "Apple TV",
            ["Amazon Prime Video"] = "Amazon",
            ["Amazon Prime"] = "Amazon",
            ["Amazon Video"] = "Amazon",
            ["Amazon Instant Video"] = "Amazon",
            ["Prime Video"] = "Amazon",
            ["Prime TV"] = "Amazon"
        };

    /// <summary>Returns a canonical display name while preserving provider variants unless grouping is enabled.</summary>
    public static string Normalize(TagKind kind, string name, bool groupProviderVariants = false)
    {
        var trimmed = name.Trim();
        if (kind != TagKind.Provider)
        {
            return trimmed;
        }

        var canonical = ProviderAliases.TryGetValue(trimmed, out var alias) ? alias : trimmed;
        return groupProviderVariants && ProviderVariantGroups.TryGetValue(canonical, out var group)
            ? group
            : canonical;
    }
}
