using Jellyfin.Plugin.MediaTaggingManager.Models;
using Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;
using Jellyfin.Plugin.MediaTaggingManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
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
    private readonly ITaskManager _taskManager;
    private readonly ManualScanRequestQueue _manualScanRequests;
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly WatchmodeQuotaTracker _watchmodeQuota;

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkController"/> class.</summary>
    public ProviderNetworkController(
        ProviderNetworkScanner scanner,
        ScanStateStore state,
        TagBackupManager backups,
        ILibraryManager libraryManager,
        ITaskManager taskManager,
        ManualScanRequestQueue manualScanRequests,
        TmdbAvailabilitySource tmdb,
        WatchmodeQuotaTracker watchmodeQuota)
    {
        _scanner = scanner;
        _state = state;
        _backups = backups;
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _manualScanRequests = manualScanRequests;
        _tmdb = tmdb;
        _watchmodeQuota = watchmodeQuota;
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

    /// <summary>Gets TMDb's official watch-provider regions for the settings dropdowns.</summary>
    [HttpGet("regions")]
    public async Task<ActionResult<AvailabilityRegionsResponse>> GetRegions(CancellationToken cancellationToken)
    {
        var regions = await _tmdb.GetAvailableRegionsAsync(cancellationToken).ConfigureAwait(false);
        var hasToken = !string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration.TmdbApiKey);
        var message = regions.Count > 0
            ? null
            : hasToken
                ? "TMDb did not return its country list. Verify that this is a TMDb API Read Access Token (not an API key), save Main Settings, then reload this page."
                : "Save a TMDb API Read Access Token in API Settings, then reload this page to load all available countries.";
        return Ok(new AvailabilityRegionsResponse(regions, message));
    }

    /// <summary>Gets the current locally tracked Watchmode monthly usage.</summary>
    [HttpGet("watchmode-usage")]
    public ActionResult<WatchmodeUsageDto> GetWatchmodeUsage() => Ok(_watchmodeQuota.GetUsage());

    /// <summary>Returns active scan status, including an estimated remaining duration.</summary>
    [HttpGet("status")]
    public ActionResult<ScanProgress> GetStatus() => Ok(_state.GetProgress());

    /// <summary>Queues a Jellyfin-managed scan of one enabled library.</summary>
    [HttpPost("scan/{libraryId:guid}")]
    public IActionResult ScanLibrary(Guid libraryId)
    {
        _manualScanRequests.EnqueueLibrary(libraryId);
        _taskManager.QueueScheduledTask<ManualScanTask>();
        return Accepted();
    }

    /// <summary>Queues a Jellyfin-managed scan of all selected libraries.</summary>
    [HttpPost("scan")]
    public IActionResult ScanAll()
    {
        _manualScanRequests.EnqueueAllLibraries();
        _taskManager.QueueScheduledTask<ManualScanTask>();
        return Accepted();
    }

    /// <summary>Cancels the current dashboard-initiated scan and clears requests that have not begun.</summary>
    [HttpPost("scan/cancel")]
    public IActionResult CancelScan()
    {
        _manualScanRequests.Clear();
        _taskManager.CancelIfRunning<ManualScanTask>();
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
        return Ok(await _scanner.CreateBackupAsync(label, libraries, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lists restorable complete tag backups, newest first.</summary>
    [HttpGet("backups")]
    public async Task<ActionResult<IReadOnlyCollection<TagBackupSummary>>> GetBackups(CancellationToken cancellationToken) =>
        Ok(await _backups.GetAllAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Deletes one stored backup without restoring it or changing Jellyfin tags.</summary>
    [HttpDelete("backups/{backupId:guid}")]
    public async Task<IActionResult> DeleteBackup(Guid backupId, CancellationToken cancellationToken)
    {
        await _backups.DeleteAsync(backupId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Restores every saved tag list from a requested backup.</summary>
    [HttpPost("backups/{backupId:guid}/restore")]
    public async Task<ActionResult<TagBackupSummary>> RestoreBackup(Guid backupId, CancellationToken cancellationToken) =>
        Ok(await _scanner.RestoreBackupAsync(backupId, null, cancellationToken).ConfigureAwait(false));

    /// <summary>Restores the newest available tag backup.</summary>
    [HttpPost("backups/undo")]
    public async Task<ActionResult<TagBackupSummary>> UndoLatest(CancellationToken cancellationToken) =>
        Ok(await _scanner.UndoLatestBackupAsync(null, cancellationToken).ConfigureAwait(false));
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
