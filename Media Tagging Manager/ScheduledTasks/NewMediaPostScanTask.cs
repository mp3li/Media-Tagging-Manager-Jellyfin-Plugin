using Jellyfin.Plugin.MediaTaggingManager.Services;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;

/// <summary>Checks newly added supported media after Jellyfin completes a library scan.</summary>
public sealed class NewMediaPostScanTask : ILibraryPostScanTask
{
    private readonly ProviderNetworkScanner _scanner;

    /// <summary>Initializes a new instance of the <see cref="NewMediaPostScanTask"/> class.</summary>
    public NewMediaPostScanTask(ProviderNetworkScanner scanner) => _scanner = scanner;

    /// <inheritdoc />
    public Task Run(IProgress<double> progress, CancellationToken cancellationToken) =>
        _scanner.ScanNewIncomingMediaAsync(progress, cancellationToken);
}
