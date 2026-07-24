using Jellyfin.Plugin.MediaTaggingManager.Models;
using Jellyfin.Plugin.MediaTaggingManager.ScheduledTasks;
using Jellyfin.Plugin.MediaTaggingManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    private readonly WatchmodeAvailabilitySource _watchmode;
    private readonly WatchmodeQuotaTracker _watchmodeQuota;
    private readonly ProviderNetworkLogoCache _logos;
    private readonly LogoLoadStateStore _logoLoadState;

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkController"/> class.</summary>
    public ProviderNetworkController(
        ProviderNetworkScanner scanner,
        ScanStateStore state,
        TagBackupManager backups,
        ILibraryManager libraryManager,
        ITaskManager taskManager,
        ManualScanRequestQueue manualScanRequests,
        TmdbAvailabilitySource tmdb,
        WatchmodeAvailabilitySource watchmode,
        WatchmodeQuotaTracker watchmodeQuota,
        ProviderNetworkLogoCache logos,
        LogoLoadStateStore logoLoadState)
    {
        _scanner = scanner;
        _state = state;
        _backups = backups;
        _libraryManager = libraryManager;
        _taskManager = taskManager;
        _manualScanRequests = manualScanRequests;
        _tmdb = tmdb;
        _watchmode = watchmode;
        _watchmodeQuota = watchmodeQuota;
        _logos = logos;
        _logoLoadState = logoLoadState;
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
        if (!configuration.SaveTagsToJellyfin && !configuration.SaveTagsToNfoFiles)
        {
            return BadRequest("Select at least one tag destination: Here in Jellyfin or In my NFO files.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey)
            && !DateOnly.TryParseExact(
                configuration.WatchmodeQuotaResetsOn,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _))
        {
            return BadRequest("Enter Watchmode's Quota Resets On date in YYYY-MM-DD format before saving a Watchmode API key.");
        }

        var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin has not finished initializing.");
        configuration.TvNetworkAppTaggingMode = configuration.TvNetworkAppTaggingMode switch
        {
            "NetworkOnly" or "StreamingAppOnly" or "Both" => configuration.TvNetworkAppTaggingMode,
            _ => "NetworkOnly"
        };
        configuration.LogoCacheLimitMegabytes = Math.Clamp(configuration.LogoCacheLimitMegabytes, 10, 1024);
        configuration.WatchmodeRequestsUsed = Math.Max(0, configuration.WatchmodeRequestsUsed);
        configuration.SelectedProviderNames = (configuration.SelectedProviderNames ?? [])
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        plugin.UpdateConfiguration(configuration);
        _watchmodeQuota.SetManualUsage(configuration.WatchmodeRequestsUsed);
        return Ok(plugin.Configuration);
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

    /// <summary>Gets TMDb's official combined movie and TV genre list.</summary>
    [HttpGet("genres")]
    public async Task<ActionResult<IReadOnlyCollection<GenreDto>>> GetGenres(CancellationToken cancellationToken) =>
        Ok(await _tmdb.GetGenresAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Gets the current locally tracked Watchmode 30-day-cycle usage.</summary>
    [HttpGet("watchmode-usage")]
    public ActionResult<WatchmodeUsageDto> GetWatchmodeUsage() => Ok(_watchmodeQuota.GetUsage());

    /// <summary>Returns active scan status, including an estimated remaining duration.</summary>
    [HttpGet("status")]
    public ActionResult<ScanProgress> GetStatus() => Ok(_state.GetProgress());

    /// <summary>Queues a Jellyfin-managed scan of one enabled library.</summary>
    [HttpPost("scan/{libraryId:guid}")]
    public async Task<IActionResult> ScanLibrary(Guid libraryId, CancellationToken cancellationToken)
    {
        if (!(await _backups.GetAllAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            return BadRequest("Create a Tag Backup before starting a scan. The scan was not started.");
        }

        _manualScanRequests.EnqueueLibrary(libraryId);
        _taskManager.QueueScheduledTask<ManualScanTask>();
        return Accepted();
    }

    /// <summary>Queues a Jellyfin-managed scan of all selected libraries.</summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanAll(CancellationToken cancellationToken)
    {
        if (!(await _backups.GetAllAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            return BadRequest("Create a Tag Backup before starting a scan. The scan was not started.");
        }

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

    /// <summary>Returns source-catalog and selected-library provider/network names for the selection controls.</summary>
    [HttpGet("tag-choices")]
    public async Task<ActionResult<TagChoicesDto>> GetTagChoices(CancellationToken cancellationToken)
    {
        var discovered = _scanner.GetTagChoices();
        var tmdb = await _tmdb.GetReferenceCatalogAsync(cancellationToken).ConfigureAwait(false);
        var watchmode = await _watchmode.GetReferenceCatalogAsync(cancellationToken).ConfigureAwait(false);
        var providers = discovered.Providers.Concat(tmdb.Providers).Concat(watchmode.Providers)
            .Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        var networks = discovered.Networks.Concat(watchmode.Networks)
            .Select(name => TagNameNormalizer.Normalize(TagKind.Network, name))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        return Ok(new TagChoicesDto(
            providers,
            networks,
            CombineCatalogNotes(tmdb.Note, watchmode.Note),
            watchmode.Note));
    }

    /// <summary>Serves a previously cached source-supplied provider or network logo for dashboard reuse.</summary>
    [HttpGet("logos/{kind}/{name}")]
    public async Task<IActionResult> GetLogo(string kind, string name, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TagKind>(kind, true, out var tagKind)
            || tagKind is not TagKind.Provider and not TagKind.Network)
        {
            return NotFound();
        }

        var logo = await _logos.OpenAsync(tagKind, name, cancellationToken).ConfigureAwait(false);
        return logo is null ? NotFound() : File(logo.Stream, logo.ContentType);
    }

    /// <summary>Returns only prefixed Provider/Network tags in selected libraries that the plugin does not recognize.</summary>
    [HttpGet("unknown-tags")]
    public async Task<ActionResult<IReadOnlyCollection<UnknownTaggedNameDto>>> GetUnknownTags(CancellationToken cancellationToken)
    {
        var tmdb = await _tmdb.GetReferenceCatalogAsync(cancellationToken).ConfigureAwait(false);
        var watchmode = await _watchmode.GetReferenceCatalogAsync(cancellationToken).ConfigureAwait(false);
        return Ok(_scanner.GetUnknownTaggedNames(
            tmdb.Providers.Concat(watchmode.Providers),
            tmdb.Networks.Concat(watchmode.Networks)));
    }

    /// <summary>Lists selected-library media carrying one unknown Provider or Network tag.</summary>
    [HttpGet("unknown-tags/{kind}/{name}/items")]
    public ActionResult<IReadOnlyCollection<TaggedItemDto>> GetUnknownTagItems(string kind, string name)
    {
        if (!TryParseKind(kind, out var tagKind) || string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Choose a Provider or Network tag.");
        }

        return Ok(_scanner.GetItemsWithTag(tagKind, name));
    }

    /// <summary>Saves a canonical name for one otherwise unknown Provider or Network tag without modifying media tags.</summary>
    [HttpPut("unknown-tags/{kind}/{name}")]
    public IActionResult SaveUnknownTagMapping(string kind, string name, [FromBody] UnknownTagMappingRequest request)
    {
        if (!TryParseKind(kind, out var tagKind) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(request.OfficialName))
        {
            return BadRequest("Choose a Provider or Network tag and enter its official name.");
        }

        var plugin = Plugin.Instance ?? throw new InvalidOperationException("The plugin has not finished initializing.");
        plugin.Configuration.UnknownTagMappings ??= [];
        plugin.Configuration.UnknownTagMappings.RemoveAll(mapping => string.Equals(mapping.Kind, tagKind.ToString(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(mapping.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        plugin.Configuration.UnknownTagMappings.Add(new Configuration.UnknownTagMapping
        {
            Kind = tagKind.ToString(),
            Name = name.Trim(),
            OfficialName = request.OfficialName.Trim()
        });
        plugin.SaveCurrentConfiguration();
        return NoContent();
    }

    /// <summary>Saves one administrator-selected logo for an unknown-tag mapping.</summary>
    [HttpPost("unknown-tags/{kind}/{name}/logo")]
    public async Task<IActionResult> UploadUnknownTagLogo(string kind, string name, [FromForm] IFormFile logo, CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var tagKind) || logo is null || logo.Length == 0 || logo.Length > 2 * 1024 * 1024)
        {
            return BadRequest("Choose a Provider or Network logo file no larger than 2 MB.");
        }

        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        var officialName = configuration.UnknownTagMappings?
            .FirstOrDefault(mapping => string.Equals(mapping.Kind, tagKind.ToString(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(mapping.Name, name, StringComparison.OrdinalIgnoreCase))?.OfficialName ?? name;
        await using var stream = logo.OpenReadStream();
        await _logos.SaveUploadedAsync(tagKind, officialName, stream, logo.ContentType, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Deletes every cached source or administrator-uploaded logo without changing media tags.</summary>
    [HttpDelete("logos")]
    public async Task<IActionResult> DeleteCachedLogos(CancellationToken cancellationToken)
    {
        await _logos.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Returns current provider/network logo-cache storage and loading state.</summary>
    [HttpGet("logos/status")]
    public async Task<ActionResult<LogoCacheStatus>> GetLogoStatus(CancellationToken cancellationToken)
    {
        var usage = await _logos.GetUsageAsync(cancellationToken).ConfigureAwait(false);
        var limit = Math.Clamp(Plugin.Instance?.Configuration.LogoCacheLimitMegabytes ?? 100, 10, 1024);
        return Ok(new LogoCacheStatus(usage.Count, usage.Bytes, limit, _logoLoadState.GetProgress()));
    }

    /// <summary>Lists cached provider/network logos for selective administrator deletion.</summary>
    [HttpGet("logos")]
    public async Task<ActionResult<IReadOnlyCollection<CachedLogoDto>>> GetCachedLogos(CancellationToken cancellationToken) =>
        Ok(await _logos.GetAllAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Starts loading every source-supplied provider logo without changing media tags.</summary>
    [HttpPost("logos/load/all")]
    public IActionResult LoadAllLogos()
    {
        try
        {
            StartLogoLoad(selectedProvidersOnly: false);
            return Accepted();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>Starts loading source-supplied logos for the saved Provider selection without changing media tags.</summary>
    [HttpPost("logos/load/selected-providers")]
    public IActionResult LoadSelectedProviderLogos()
    {
        var selected = Plugin.Instance?.Configuration.SelectedProviderNames ?? [];
        if (selected.Length == 0)
        {
            return BadRequest("Select and save at least one Provider before loading selected Provider logos.");
        }

        try
        {
            StartLogoLoad(selectedProvidersOnly: true);
            return Accepted();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>Deletes one cached Provider or Network logo without changing media tags.</summary>
    [HttpDelete("logos/{kind}/{name}")]
    public async Task<IActionResult> DeleteCachedLogo(string kind, string name, CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out var tagKind) || string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Choose a Provider or Network logo.");
        }

        await _logos.DeleteAsync(tagKind, name, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Removes unselected provider tags without looking up or changing any source data.</summary>
    [HttpPost("sync/providers")]
    public Task<ActionResult<TagSyncResult>> SyncProviders([FromBody] TagSelectionRequest request, CancellationToken cancellationToken) =>
        SyncAsync(TagKind.Provider, request.Names ?? [], cancellationToken);

    /// <summary>Removes unselected network tags without looking up or changing any source data.</summary>
    [HttpPost("sync/networks")]
    public Task<ActionResult<TagSyncResult>> SyncNetworks([FromBody] TagSelectionRequest request, CancellationToken cancellationToken) =>
        SyncAsync(TagKind.Network, request.Names ?? [], cancellationToken);

    /// <summary>Removes unselected genre tags without looking up or changing any source data.</summary>
    [HttpPost("sync/genres")]
    public Task<ActionResult<TagSyncResult>> SyncGenres([FromBody] TagSelectionRequest request, CancellationToken cancellationToken) =>
        SyncAsync(TagKind.Genre, request.Names ?? [], cancellationToken);

    /// <summary>Removes every plugin-created keyword tag without contacting a source.</summary>
    [HttpPost("remove/keywords")]
    public Task<ActionResult<TagSyncResult>> RemoveKeywords(CancellationToken cancellationToken) =>
        SyncAsync(TagKind.Keyword, [], cancellationToken);

    /// <summary>Scans selected libraries for direct TMDb movie-collection matches without changing tags.</summary>
    [HttpPost("collections/scan")]
    public async Task<ActionResult<IReadOnlyCollection<CollectionMatchDto>>> ScanCollections(CancellationToken cancellationToken) =>
        Ok(await _scanner.ScanCollectionMatchesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Adds only the collection matches selected in the dashboard.</summary>
    [HttpPost("collections/apply")]
    public async Task<ActionResult<TagApplyResult>> ApplyCollections([FromBody] CollectionMatchRequest request, CancellationToken cancellationToken) =>
        Ok(await _scanner.ApplyCollectionMatchesAsync(request.Matches ?? [], cancellationToken).ConfigureAwait(false));

    private async Task<ActionResult<TagSyncResult>> SyncAsync(TagKind kind, IEnumerable<string> names, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scanner.SyncWithOnlySelectedAsync(kind, names, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = $"Could not sync {kind.ToString().ToLowerInvariant()} tags.",
                Detail = exception.Message,
                Status = 400
            });
        }
        catch (Exception exception)
        {
            return StatusCode(500, new ProblemDetails
            {
                Title = $"Could not sync {kind.ToString().ToLowerInvariant()} tags.",
                Detail = exception.Message,
                Status = 500
            });
        }
    }

    /// <summary>Replaces an item's plugin-owned tags with administrator-entered values.</summary>
    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] ManualTagsRequest request, CancellationToken cancellationToken)
    {
        await _scanner.ApplyManualTagsAsync(itemId, request.Providers ?? [], request.Networks ?? [], request.Genres ?? [], request.Keywords ?? [], request.Collections ?? [], cancellationToken).ConfigureAwait(false);
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

    private static string? CombineCatalogNotes(params string?[] notes)
    {
        var meaningful = notes.Where(note => !string.IsNullOrWhiteSpace(note)).ToArray();
        return meaningful.Length == 0 ? null : string.Join(" ", meaningful);
    }

    private void StartLogoLoad(bool selectedProvidersOnly)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!configuration.EnableLogoCaching)
        {
            throw new InvalidOperationException("Enable logo saving in Logo Settings before loading logos.");
        }

        if (!_logoLoadState.TryStart())
        {
            throw new InvalidOperationException("A logo-loading operation is already running.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var tmdb = await _tmdb.GetReferenceCatalogAsync(CancellationToken.None).ConfigureAwait(false);
                var watchmode = await _watchmode.GetReferenceCatalogAsync(CancellationToken.None).ConfigureAwait(false);
                IEnumerable<SourceTag> tags = (tmdb.ProviderLogoUrls ?? new Dictionary<string, string>())
                    .Concat(watchmode.ProviderLogoUrls ?? new Dictionary<string, string>())
                    .Select(pair => new SourceTag(TagKind.Provider, pair.Key, "Reference catalog", false, pair.Value));
                if (selectedProvidersOnly)
                {
                    var selected = new HashSet<string>((Plugin.Instance?.Configuration.SelectedProviderNames ?? [])
                        .Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name)), StringComparer.OrdinalIgnoreCase);
                    tags = tags.Where(tag => selected.Contains(TagNameNormalizer.Normalize(TagKind.Provider, tag.Name)));
                }
                else
                {
                    var networks = await _tmdb.GetNetworkLogoTagsAsync(watchmode.NetworkTmdbIds ?? new Dictionary<string, int>(), CancellationToken.None).ConfigureAwait(false);
                    tags = tags.Concat(networks);
                }

                var uniqueTags = tags.GroupBy(tag => (tag.Kind, Name: TagNameNormalizer.Normalize(tag.Kind, tag.Name)))
                    .Select(group => group.First())
                    .ToArray();
                _logoLoadState.SetTotal(uniqueTags.Length);
                await _logos.CacheAsync(uniqueTags, CancellationToken.None, _logoLoadState.Report).ConfigureAwait(false);
                _logoLoadState.Complete();
            }
            catch (Exception exception)
            {
                _logoLoadState.Complete("Logo loading stopped: " + exception.Message);
            }
        });
    }

    private static bool TryParseKind(string value, out TagKind kind) => Enum.TryParse(value, true, out kind)
        && kind is TagKind.Provider or TagKind.Network;
}

/// <summary>Manual provider/network edits submitted from the dashboard.</summary>
public sealed class ManualTagsRequest
{
    /// <summary>Gets or sets provider names.</summary>
    public string[]? Providers { get; set; }

    /// <summary>Gets or sets network names.</summary>
    public string[]? Networks { get; set; }

    /// <summary>Gets or sets genre names.</summary>
    public string[]? Genres { get; set; }

    /// <summary>Gets or sets keyword names.</summary>
    public string[]? Keywords { get; set; }

    /// <summary>Gets or sets collection names.</summary>
    public string[]? Collections { get; set; }
}

/// <summary>Provider or network names selected for a no-lookup synchronization action.</summary>
public sealed class TagSelectionRequest
{
    /// <summary>Gets or sets the names to retain.</summary>
    public string[]? Names { get; set; }
}

/// <summary>A canonical display name submitted for an otherwise unknown tag.</summary>
public sealed class UnknownTagMappingRequest
{
    /// <summary>Gets or sets the official/canonical provider or network name.</summary>
    public string OfficialName { get; set; } = string.Empty;
}

/// <summary>Collection matches selected for one additive collection-tag operation.</summary>
public sealed class CollectionMatchRequest
{
    /// <summary>Gets or sets the direct TMDb movie collection matches to apply.</summary>
    public CollectionMatchDto[]? Matches { get; set; }
}

/// <summary>Optional administrator label for a manually created complete tag backup.</summary>
public sealed class CreateBackupRequest
{
    /// <summary>Gets or sets the backup label.</summary>
    public string? Label { get; set; }
}
