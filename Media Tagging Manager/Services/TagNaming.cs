namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Creates tags that are unambiguous, filterable, and safely managed by this plugin.</summary>
public static class TagNaming
{
    /// <summary>Gets the provider tag prefix.</summary>
    public const string ProviderPrefix = "Provider: ";

    /// <summary>Gets the network tag prefix.</summary>
    public const string NetworkPrefix = "Network: ";
    /// <summary>Gets the genre tag prefix.</summary>
    public const string GenrePrefix = "Genre: ";

    /// <summary>Gets the keyword tag prefix.</summary>
    public const string KeywordPrefix = "Keyword: ";

    /// <summary>Gets the collection tag prefix.</summary>
    public const string CollectionPrefix = "Collection: ";

    /// <summary>Formats a managed Jellyfin tag.</summary>
    public static string Format(Models.TagKind kind, string name) =>
        Prefix(kind) + name.Trim();

    /// <summary>Returns whether a tag was created by this plugin.</summary>
    public static bool IsManaged(string tag) =>
        TryGetKind(tag, out _);

    /// <summary>Returns the managed tag kind for a prefixed plugin tag.</summary>
    public static bool TryGetKind(string tag, out Models.TagKind kind)
    {
        foreach (var candidate in Enum.GetValues<Models.TagKind>())
        {
            if (tag.StartsWith(Prefix(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    /// <summary>Gets the prefix for one plugin-managed tag kind.</summary>
    public static string Prefix(Models.TagKind kind) => kind switch
    {
        Models.TagKind.Provider => ProviderPrefix,
        Models.TagKind.Network => NetworkPrefix,
        Models.TagKind.Genre => GenrePrefix,
        Models.TagKind.Keyword => KeywordPrefix,
        Models.TagKind.Collection => CollectionPrefix,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
