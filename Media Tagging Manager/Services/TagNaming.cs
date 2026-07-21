namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Creates tags that are unambiguous, filterable, and safely managed by this plugin.</summary>
public static class TagNaming
{
    /// <summary>Gets the provider tag prefix.</summary>
    public const string ProviderPrefix = "Provider: ";

    /// <summary>Gets the network tag prefix.</summary>
    public const string NetworkPrefix = "Network: ";

    /// <summary>Formats a managed Jellyfin tag.</summary>
    public static string Format(Models.TagKind kind, string name) =>
        (kind == Models.TagKind.Provider ? ProviderPrefix : NetworkPrefix) + name.Trim();

    /// <summary>Returns whether a tag was created by this plugin.</summary>
    public static bool IsManaged(string tag) =>
        tag.StartsWith(ProviderPrefix, StringComparison.OrdinalIgnoreCase)
        || tag.StartsWith(NetworkPrefix, StringComparison.OrdinalIgnoreCase);
}
