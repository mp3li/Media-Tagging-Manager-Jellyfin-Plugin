using Jellyfin.Plugin.MediaTaggingManager.Models;
using Jellyfin.Plugin.MediaTaggingManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Controller.Library;
using PluginConfiguration = Jellyfin.Plugin.MediaTaggingManager.Configuration.PluginConfiguration;

namespace Jellyfin.Plugin.MediaTaggingManager.Api;

/// <summary>Administrator-only endpoints used by the plugin dashboard.</summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("MediaTaggingManager")]
public sealed class ProviderNetworkController : ControllerBase
{
    private readonly ProviderNetworkScanner _scanner;
    private readonly ScanStateStore _state;
    private readonly TagBackupManager _backups;
    private readonly ILibraryManager _libraryManager;

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkController"/> class.</summary>
    public ProviderNetworkController(ProviderNetworkScanner scanner, ScanStateStore state, TagBackupManager backups, ILibraryManager libraryManager)
    {
        _scanner = scanner;
        _state = state;
        _backups = backups;
        _libraryManager = libraryManager;
    }

    /// <summary>Returns plugin settings and selectable libraries without relying on dashboard-internal endpoints.</summary>
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin has not finished initializing.");
        return Ok(new
        {
            Configuration = plugin.Configuration,
            // Match Jellyfin's own LibraryStructureController, which asks for
            // all virtual folders by passing true.
            Libraries = _libraryManager.GetVirtualFolders(true).Select(folder => new { folder.ItemId, folder.Name })
        });
    }

    /// <summary>Saves administrator settings without relying on the dashboard plugin-configuration endpoint.</summary>
    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] PluginConfiguration configuration)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin has not finished initializing.");
        plugin.UpdateConfiguration(configuration);
        return NoContent();
    }

    /// <summary>Returns active scan status, including an estimated remaining duration.</summary>
    [HttpGet("status")]
    public ActionResult<ScanProgress> GetStatus() => Ok(_state.GetProgress());

    /// <summary>Starts an asynchronous scan of one enabled library.</summary>
    [HttpPost("scan/{libraryId:guid}")]
    public IActionResult ScanLibrary(Guid libraryId)
    {
        _ = Task.Run(() => _scanner.ScanLibraryAsync(libraryId, null, CancellationToken.None));
        return Accepted();
    }

    /// <summary>Starts an asynchronous scan of all selected libraries.</summary>
    [HttpPost("scan")]
    public IActionResult ScanAll()
    {
        _ = Task.Run(() => _scanner.ScanConfiguredLibrariesAsync(null, CancellationToken.None));
        return Accepted();
    }

    /// <summary>Returns the live library overview and applies optional dashboard filters.</summary>
    [HttpGet("items")]
    public ActionResult<IEnumerable<TaggedItemDto>> GetItems(
        [FromQuery] Guid? libraryId,
        [FromQuery] string? provider,
        [FromQuery] string? network,
        [FromQuery] bool? isTagged) => Ok(_scanner.GetDashboardItems(libraryId, provider, network, isTagged));

    /// <summary>Replaces an item's plugin-owned tags with administrator-entered values.</summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] ManualTagsRequest request, CancellationToken cancellationToken)
    {
        await _scanner.ApplyManualTagsAsync(itemId, request.Providers ?? [], request.Networks ?? [], cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Creates a complete tag backup for all currently selected libraries.</summary>
    [HttpPost("backups")]
    public async Task<ActionResult<TagBackupSummary>> CreateBackup([FromBody] CreateBackupRequest? request, CancellationToken cancellationToken)
    {
        var libraries = Plugin.Instance?.Configuration.LibraryIds ?? [];
        if (libraries.Length == 0)
        {
            return BadRequest("Select at least one library before creating a tag backup.");
        }

        var label = string.IsNullOrWhiteSpace(request?.Label) ? "Manual tag backup" : request.Label;
        return Ok(await _backups.CreateAsync(label, libraries, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lists restorable complete tag backups, newest first.</summary>
    [HttpGet("backups")]
    public async Task<ActionResult<IReadOnlyCollection<TagBackupSummary>>> GetBackups(CancellationToken cancellationToken) =>
        Ok(await _backups.GetAllAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Restores every saved tag list from a requested backup.</summary>
    [HttpPost("backups/{backupId:guid}/restore")]
    public async Task<ActionResult<TagBackupSummary>> RestoreBackup(Guid backupId, CancellationToken cancellationToken) =>
        Ok(await _backups.RestoreAsync(backupId, null, cancellationToken).ConfigureAwait(false));

    /// <summary>Restores the newest available tag backup.</summary>
    [HttpPost("backups/undo")]
    public async Task<ActionResult<TagBackupSummary>> UndoLatest(CancellationToken cancellationToken) =>
        Ok(await _backups.UndoLatestAsync(null, cancellationToken).ConfigureAwait(false));
}

/// <summary>Manual provider/network edits submitted from the dashboard.</summary>
public sealed class ManualTagsRequest
{
    /// <summary>Gets or sets provider names.</summary>
    public string[]? Providers { get; set; }

    /// <summary>Gets or sets network names.</summary>
    public string[]? Networks { get; set; }
}

/// <summary>Optional administrator label for a manually created complete tag backup.</summary>
public sealed class CreateBackupRequest
{
    /// <summary>Gets or sets the backup label.</summary>
    public string? Label { get; set; }
}
