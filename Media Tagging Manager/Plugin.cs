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

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            Name = Name,
            DisplayName = Name,
            EmbeddedResourcePath = "Jellyfin.Plugin.MediaTaggingManager.Web.configPage.html"
        }
    ];
}
