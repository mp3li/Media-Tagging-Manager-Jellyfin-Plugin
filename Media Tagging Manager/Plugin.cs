using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.MediaTaggingManager.Configuration;

namespace Jellyfin.Plugin.MediaTaggingManager;

/// <summary>Registers the Providers &amp; Networks Tagger plugin with Jellyfin.</summary>
public sealed class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>Gets the singleton plugin instance.</summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="Plugin"/> class.</summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        _applicationPaths = applicationPaths;
        Instance = this;

        // Jellyfin's BasePlugin falls back to a new default configuration if
        // its XML cannot be read. Restore our last successfully saved,
        // server-local mirror before Configuration is first accessed so an
        // update or a damaged XML file cannot silently discard credentials
        // and selections.
        ConfigurationRecovery.RestoreIfNecessary(this, applicationPaths, xmlSerializer);

        // Seed the recovery mirror immediately for existing installations; a
        // user should not have to revisit a settings page just to gain update
        // protection.
        ConfigurationRecovery.Save(applicationPaths, Configuration);
    }

    /// <inheritdoc />
    public override string Name => "Media Tagging Manager Jellyfin Plugin";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("c7b639bd-55c5-4694-aa8e-32c816048da8");

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        base.UpdateConfiguration(configuration);
        ConfigurationRecovery.Save(_applicationPaths, Configuration);
    }

    /// <summary>
    /// Saves a configuration mutation made by a background service and refreshes
    /// the server-local recovery mirror at the same time.
    /// </summary>
    public void SaveConfigurationWithRecovery()
    {
        base.SaveConfiguration(Configuration);
        ConfigurationRecovery.Save(_applicationPaths, Configuration);
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
