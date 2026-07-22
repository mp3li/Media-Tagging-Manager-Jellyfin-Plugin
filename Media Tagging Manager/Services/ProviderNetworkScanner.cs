using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Coordinates external lookups, safe tag writes, dashboard data, and scan progress.</summary>
public sealed class ProviderNetworkScanner
{
    private readonly ILibraryManager _libraryManager;
    private readonly IReadOnlyCollection<IAvailabilitySource> _sources;
    private readonly ScanStateStore _state;
    private readonly TagBackupManager _backups;
    private readonly SemaphoreSlim _scanLock = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkScanner"/> class.</summary>
    public ProviderNetworkScanner(
        ILibraryManager libraryManager,
        IEnumerable<IAvailabilitySource> sources,
        ScanStateStore state,
        TagBackupManager backups)
    {
        _libraryManager = libraryManager;
        _sources = sources.ToArray();
        _state = state;
        _backups = backups;
    }

    /// <summary>Scans a configured library. Only movies and series receive tags; episodes inherit their series context.</summary>
    public async Task ScanLibraryAsync(Guid libraryId, IProgress<double>? jellyfinProgress, CancellationToken cancellationToken, bool createBackup = true)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!configuration.LibraryIds.Contains(libraryId))
        {
            throw new InvalidOperationException("This library is not enabled in Media Tagging Manager settings.");
        }

        EnsureSourceConfigured(configuration);

        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ScanLibraryLockedAsync(libraryId, configuration, jellyfinProgress, cancellationToken, createBackup).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _state.Complete(exception.Message);
            throw;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Scans every currently configured library.</summary>
    public async Task ScanConfiguredLibrariesAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var libraries = configuration.LibraryIds;
        if (libraries.Length == 0)
        {
            return;
        }

        EnsureSourceConfigured(configuration);
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _backups.CreateAsync("Before configured-library scan", libraries, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < libraries.Length; index++)
            {
                var offset = index;
                await ScanLibraryLockedAsync(
                    libraries[index],
                    configuration,
                    new Progress<double>(value => progress?.Report((offset * 100d + value) / libraries.Length)),
                    cancellationToken,
                    createBackup: false).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _state.Complete(exception.Message);
            throw;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Checks only titles added since the previous enabled post-library-scan check.</summary>
    public async Task ScanNewIncomingMediaAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!configuration.EnableNewMediaChecks)
        {
            return;
        }

        // A post-scan hook must never break Jellyfin's library scan when setup is incomplete.
        try
        {
            EnsureSourceConfigured(configuration);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var startedUtc = DateTime.UtcNow;
            if (configuration.LastIncomingMediaCheckUtc is null)
            {
                configuration.LastIncomingMediaCheckUtc = startedUtc;
                Plugin.Instance?.SaveConfiguration(configuration);
                return;
            }

            var candidates = new List<(BaseItem Item, Guid LibraryId)>();
            foreach (var libraryId in configuration.LibraryIds)
            {
                var query = new InternalItemsQuery
                {
                    ParentId = libraryId,
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
                };
                candidates.AddRange(_libraryManager.GetItemList(query)
                    .Where(item => item.DateCreated >= configuration.LastIncomingMediaCheckUtc.Value && item.DateCreated < startedUtc)
                    .Select(item => (Item: item, LibraryId: libraryId)));
            }

            if (candidates.Count > 0)
            {
                await _backups.CreateAsync("Before incoming-media update", configuration.LibraryIds, cancellationToken).ConfigureAwait(false);
                await ScanItemsAsync(candidates, configuration, progress, cancellationToken).ConfigureAwait(false);
            }
            configuration.LastIncomingMediaCheckUtc = startedUtc;
            Plugin.Instance?.SaveConfiguration(configuration);
        }
        catch (Exception exception)
        {
            _state.Complete(exception.Message);
            throw;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Applies administrator-entered provider and network values without contacting external services.</summary>
    public async Task ApplyManualTagsAsync(Guid itemId, IEnumerable<string> providers, IEnumerable<string> networks, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The Jellyfin item no longer exists.");
            var libraryId = item.GetTopParent().Id;
            var enabledLibraries = Plugin.Instance?.Configuration.LibraryIds ?? [];
            if (!enabledLibraries.Contains(libraryId) || (item is not Movie && item is not Series))
            {
                throw new InvalidOperationException("Only movies and series in selected libraries may be edited by Media Tagging Manager.");
            }

            await _backups.CreateAsync("Before manual tag edit", [libraryId], cancellationToken).ConfigureAwait(false);
            var tags = providers.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Provider, value, "Manual"))
                .Concat(networks.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Network, value, "Manual")));
            await ApplyTagsAsync(item, libraryId, tags, ["Manual"], true, cancellationToken, forceManagedReplacement: true).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Creates a complete snapshot while preventing a scan or manual edit from changing tags concurrently.</summary>
    public async Task<TagBackupSummary> CreateBackupAsync(string label, IEnumerable<Guid> libraryIds, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _backups.CreateAsync(label, libraryIds, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Restores a snapshot while preventing a scan or manual edit from changing tags concurrently.</summary>
    public async Task<TagBackupSummary> RestoreBackupAsync(Guid backupId, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _backups.RestoreAsync(backupId, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Restores the newest snapshot while preventing a scan or manual edit from changing tags concurrently.</summary>
    public async Task<TagBackupSummary> UndoLatestBackupAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _backups.UndoLatestAsync(progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Returns a live, filterable view of all supported items in configured libraries.</summary>
    public IEnumerable<TaggedItemDto> GetDashboardItems(Guid? libraryId, string? provider, string? network, bool? isTagged)
    {
        var allowedLibraries = Plugin.Instance?.Configuration.LibraryIds ?? [];
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
        };

        return _libraryManager.GetItemList(query)
            .Where(item => allowedLibraries.Contains(item.GetTopParent().Id))
            .Select(ToDto)
            .Where(item => libraryId is null || item.LibraryId == libraryId)
            .Where(item => string.IsNullOrWhiteSpace(provider) || item.Providers.Any(value => value.Contains(provider, StringComparison.OrdinalIgnoreCase)))
            .Where(item => string.IsNullOrWhiteSpace(network) || item.Networks.Any(value => value.Contains(network, StringComparison.OrdinalIgnoreCase)))
            .Where(item => isTagged is null || (item.Providers.Count > 0 || item.Networks.Count > 0) == isTagged)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ScanItemAsync(BaseItem item, Guid libraryId, CancellationToken cancellationToken)
    {
        var ids = new ExternalIds(
            GetProviderId(item, "Tmdb"),
            GetProviderId(item, "Imdb"),
            item.GetType().Name);

        var results = await Task.WhenAll(_sources.Select(source => LookupSafelyAsync(source, ids, cancellationToken))).ConfigureAwait(false);
        var tags = results.SelectMany(result => result.Tags).ToArray();
        await ApplyTagsAsync(
            item,
            libraryId,
            tags,
            results.Select(result => result.Source),
            results.All(result => string.IsNullOrWhiteSpace(result.Note)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ScanLibraryLockedAsync(
        Guid libraryId,
        Configuration.PluginConfiguration configuration,
        IProgress<double>? jellyfinProgress,
        CancellationToken cancellationToken,
        bool createBackup)
    {
        if (createBackup)
        {
            await _backups.CreateAsync("Before library scan", [libraryId], cancellationToken).ConfigureAwait(false);
        }

        var query = new InternalItemsQuery
        {
            ParentId = libraryId,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
        };
        var items = _libraryManager.GetItemList(query);
        await ScanItemsAsync(items.Select(item => (Item: item, LibraryId: libraryId)).ToArray(), configuration, jellyfinProgress, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScanItemsAsync(
        IReadOnlyCollection<(BaseItem Item, Guid LibraryId)> items,
        Configuration.PluginConfiguration configuration,
        IProgress<double>? jellyfinProgress,
        CancellationToken cancellationToken)
    {
        _state.Start(items.Count);
        var completed = 0;
        await Parallel.ForEachAsync(items, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(configuration.MaxConcurrentLookups, 1, 12)
        }, async (entry, token) =>
        {
            _state.Report(Volatile.Read(ref completed), entry.Item.Name);
            await ScanItemAsync(entry.Item, entry.LibraryId, token).ConfigureAwait(false);
            var count = Interlocked.Increment(ref completed);
            _state.Report(count, entry.Item.Name);
            jellyfinProgress?.Report(items.Count == 0 ? 100 : count * 100d / items.Count);
        }).ConfigureAwait(false);
        _state.Complete();
    }

    private static void EnsureSourceConfigured(Configuration.PluginConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey)
            && string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey)
            && !configuration.CustomSources.Any(static source => source.Enabled && !string.IsNullOrWhiteSpace(source.UrlTemplate)))
        {
            throw new InvalidOperationException("Configure at least one enabled source before scanning. Existing plugin tags were left unchanged.");
        }
    }

    private async Task<SourceLookupResult> LookupSafelyAsync(IAvailabilitySource source, ExternalIds ids, CancellationToken cancellationToken)
    {
        try
        {
            return await source.LookupAsync(ids, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return new SourceLookupResult(source.Name, [], exception.Message);
        }
        catch (JsonException exception)
        {
            return new SourceLookupResult(source.Name, [], exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new SourceLookupResult(source.Name, [], exception.Message);
        }
    }

    private async Task ApplyTagsAsync(
        BaseItem item,
        Guid libraryId,
        IEnumerable<SourceTag> values,
        IEnumerable<string> sources,
        bool replaceManagedTags,
        CancellationToken cancellationToken,
        bool forceManagedReplacement = false)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var selected = values
            .Where(value => (value.Kind == TagKind.Provider && configuration.TagProviders) || (value.Kind == TagKind.Network && configuration.TagNetworks))
            .Where(value => !string.IsNullOrWhiteSpace(value.Name))
            .Select(value => TagNaming.Format(value.Kind, value.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        var existing = item.Tags ?? [];
        // A failed source must never erase previously known availability. A later healthy scan reconciles it.
        var retained = (configuration.ReplaceManagedTags || forceManagedReplacement) && replaceManagedTags ? existing.Where(tag => !TagNaming.IsManaged(tag)) : existing;
        item.Tags = retained.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await _libraryManager.UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        _state.Save(ToDto(item, libraryId, DateTimeOffset.UtcNow, sources));
    }

    private TaggedItemDto ToDto(BaseItem item) => ToDto(item, item.GetTopParent().Id, null, []);

    private static TaggedItemDto ToDto(BaseItem item, Guid libraryId, DateTimeOffset? checkedUtc, IEnumerable<string> sources)
    {
        var tags = item.Tags ?? [];
        return new TaggedItemDto(
            item.Id,
            item.Name,
            item.GetType().Name,
            libraryId,
            tags.Where(tag => tag.StartsWith(TagNaming.ProviderPrefix, StringComparison.OrdinalIgnoreCase)).Select(tag => tag[TagNaming.ProviderPrefix.Length..]).ToArray(),
            tags.Where(tag => tag.StartsWith(TagNaming.NetworkPrefix, StringComparison.OrdinalIgnoreCase)).Select(tag => tag[TagNaming.NetworkPrefix.Length..]).ToArray(),
            checkedUtc,
            sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? GetProviderId(BaseItem item, string name) =>
        item.ProviderIds is not null && item.ProviderIds.TryGetValue(name, out var value) ? value : null;
}
