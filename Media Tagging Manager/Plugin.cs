using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MediaTaggingManager;

/// <summary>Registers the Providers &amp; Networks Tagger plugin with Jellyfin.</summary>
public sealed class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    /// <summary>Gets the singleton plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Media Tagging Manager Jellyfin Plugin";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c7b639bd-55c5-4694-aa8e-32c816048da8");

    /// <summary>Saves a configuration mutation made by a background service.</summary>
    public void SaveCurrentConfiguration()
    {
        base.SaveConfiguration(Configuration);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            // Keep the catalog/plugin title descriptive, while using the concise
            // label requested for Jellyfin's Dashboard plugin menu.
            Name = "Media Tagging Manager",
            DisplayName = "Media Tagging Manager",
            EnableInMainMenu = true,
            EmbeddedResourcePath = "Jellyfin.Plugin.MediaTaggingManager.Web.configPage.html"
        }
    ];
}
