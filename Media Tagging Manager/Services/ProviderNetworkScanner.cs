using System.Collections.Concurrent;
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
    private readonly TagDestinationWriter _destinations;
    private readonly ProviderNetworkLogoCache _logos;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private readonly object _knownTagLock = new();

    /// <summary>Initializes a new instance of the <see cref="ProviderNetworkScanner"/> class.</summary>
    public ProviderNetworkScanner(
        ILibraryManager libraryManager,
        IEnumerable<IAvailabilitySource> sources,
        ScanStateStore state,
        TagBackupManager backups,
        TagDestinationWriter destinations,
        ProviderNetworkLogoCache logos)
    {
        _libraryManager = libraryManager;
        _sources = sources.ToArray();
        _state = state;
        _backups = backups;
        _destinations = destinations;
        _logos = logos;
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
        _destinations.Validate(configuration, [libraryId]);

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
        _destinations.Validate(configuration, libraries);
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _backups.CreateAsync("Before configured-library scan", libraries, cancellationToken).ConfigureAwait(false);
            var items = libraries.SelectMany(libraryId =>
            {
                var query = new InternalItemsQuery
                {
                    ParentId = libraryId,
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
                };
                return _libraryManager.GetItemList(query).Select(item => (Item: item, LibraryId: libraryId));
            }).ToArray();
            await ScanItemsAsync(items, configuration, progress, cancellationToken).ConfigureAwait(false);
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
            _destinations.Validate(configuration, configuration.LibraryIds);
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

    /// <summary>Applies administrator-entered plugin-owned values without contacting external services.</summary>
    public async Task ApplyManualTagsAsync(Guid itemId, IEnumerable<string> providers, IEnumerable<string> networks, IEnumerable<string> genres, IEnumerable<string> keywords, IEnumerable<string> collections, CancellationToken cancellationToken)
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

            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            _destinations.Validate(configuration, [libraryId]);

            await _backups.CreateAsync("Before manual tag edit", [libraryId], cancellationToken).ConfigureAwait(false);
            var tags = providers.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Provider, value, "Manual"))
                .Concat(networks.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Network, value, "Manual")))
                .Concat(genres.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Genre, value, "Manual")))
                .Concat(keywords.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Keyword, value, "Manual")))
                .Concat(collections.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(value => new SourceTag(TagKind.Collection, value, "Manual")));
            await ApplyTagsAsync(item, libraryId, tags, ["Manual"], true, cancellationToken, forceManagedReplacement: true, replaceKinds: Enum.GetValues<TagKind>()).ConfigureAwait(false);
            RememberKnownTags(providers, networks);
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
        return allowedLibraries.SelectMany(libraryId =>
            {
                var query = new InternalItemsQuery
                {
                    ParentId = libraryId,
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
                };
                return _libraryManager.GetItemList(query).Select(item => ToDto(item, libraryId, null, []));
            })
            .Where(item => libraryId is null || item.LibraryId == libraryId)
            .Where(item => string.IsNullOrWhiteSpace(provider) || item.Providers.Any(value => value.Contains(provider, StringComparison.OrdinalIgnoreCase)))
            .Where(item => string.IsNullOrWhiteSpace(network) || item.Networks.Any(value => value.Contains(network, StringComparison.OrdinalIgnoreCase)))
            .Where(item => isTagged is null || (item.Providers.Count > 0 || item.Networks.Count > 0 || item.Genres.Count > 0 || item.Keywords.Count > 0 || item.Collections.Count > 0) == isTagged)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns names discovered by scans plus all matching current tags in selected libraries.</summary>
    public TagChoicesDto GetTagChoices()
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var items = GetDashboardItems(null, null, null, null).ToArray();
        return new TagChoicesDto(
            (configuration.KnownProviderNames ?? []).Concat(items.SelectMany(item => item.Providers)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            (configuration.KnownNetworkNames ?? []).Concat(items.SelectMany(item => item.Networks)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>Returns only prefixed Provider/Network tags that are not plugin-known or source-recognized.</summary>
    public IReadOnlyCollection<UnknownTaggedNameDto> GetUnknownTaggedNames(
        IEnumerable<string> recognizedProviders,
        IEnumerable<string> recognizedNetworks)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var knownProviders = new HashSet<string>((configuration.KnownProviderNames ?? []).Concat(recognizedProviders)
            .Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name)), StringComparer.OrdinalIgnoreCase);
        var knownNetworks = new HashSet<string>((configuration.KnownNetworkNames ?? []).Concat(recognizedNetworks)
            .Select(name => TagNameNormalizer.Normalize(TagKind.Network, name)), StringComparer.OrdinalIgnoreCase);
        var unknown = new Dictionary<(TagKind Kind, string Name), int>();
        foreach (var item in GetDashboardItems(null, null, null, null))
        {
            foreach (var name in item.Providers)
            {
                var normalized = TagNameNormalizer.Normalize(TagKind.Provider, name);
                if (!knownProviders.Contains(normalized))
                {
                    unknown[(TagKind.Provider, name)] = unknown.GetValueOrDefault((TagKind.Provider, name)) + 1;
                }
            }

            foreach (var name in item.Networks)
            {
                var normalized = TagNameNormalizer.Normalize(TagKind.Network, name);
                if (!knownNetworks.Contains(normalized))
                {
                    unknown[(TagKind.Network, name)] = unknown.GetValueOrDefault((TagKind.Network, name)) + 1;
                }
            }
        }

        return unknown
            .Select(pair => new UnknownTaggedNameDto(pair.Key.Kind, pair.Key.Name, pair.Value))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Returns selected-library items carrying one exact prefixed Provider or Network tag.</summary>
    public IReadOnlyCollection<TaggedItemDto> GetItemsWithTag(TagKind kind, string name)
    {
        if (kind is not TagKind.Provider and not TagKind.Network)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Only Provider and Network tags can be inspected here.");
        }

        var normalized = TagNameNormalizer.Normalize(kind, name);
        return GetDashboardItems(null, null, null, null)
            .Where(item => (kind == TagKind.Provider ? item.Providers : item.Networks)
                .Any(value => string.Equals(TagNameNormalizer.Normalize(kind, value), normalized, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Removes unselected tags of one kind from selected libraries without contacting any source.</summary>
    public async Task<TagSyncResult> SyncWithOnlySelectedAsync(TagKind kind, IEnumerable<string> selectedNames, CancellationToken cancellationToken)
    {
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            if (configuration.LibraryIds.Length == 0)
            {
                throw new InvalidOperationException("Select and save at least one library before synchronizing tags.");
            }

            _destinations.Validate(configuration, configuration.LibraryIds);

            var selected = NormalizeNames(kind, selectedNames);
            if (kind == TagKind.Provider)
            {
                configuration.SelectedProviderNames = selected;
                configuration.RestrictProvidersToSelected = true;
            }
            else if (kind == TagKind.Network)
            {
                configuration.SelectedNetworkNames = selected;
                configuration.RestrictNetworksToSelected = true;
            }
            else if (kind == TagKind.Genre)
            {
                configuration.SelectedGenreNames = selected;
                configuration.TagGenres = true;
            }
            else if (kind != TagKind.Keyword)
            {
                throw new InvalidOperationException("Only Provider, Network, and Genre tags have selectable sync lists.");
            }

            await _backups.CreateAsync($"Before {kind.ToString().ToLowerInvariant()} selection sync", configuration.LibraryIds, cancellationToken).ConfigureAwait(false);
            Plugin.Instance?.SaveConfiguration(configuration);

            var tagsRemoved = 0;
            var mediaItemsChanged = 0;
            foreach (var libraryId in configuration.LibraryIds)
            {
                var query = new InternalItemsQuery
                {
                    ParentId = libraryId,
                    Recursive = true,
                    IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
                };

                foreach (var item in _libraryManager.GetItemList(query))
                {
                    var existing = item.Tags ?? [];
                    var retained = existing
                        .Select(tag => IsTagKind(tag, kind)
                            ? TagNaming.Format(kind, TagNameNormalizer.Normalize(kind, RemoveTagPrefix(tag, kind)))
                            : tag)
                        .Where(tag => !IsTagKind(tag, kind) || selected.Contains(RemoveTagPrefix(tag, kind)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var removed = existing.Length - retained.Length;
                    if (existing.SequenceEqual(retained, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    item.Tags = retained;
                    await _destinations.SaveAsync(item, configuration, cancellationToken).ConfigureAwait(false);
                    tagsRemoved += removed;
                    mediaItemsChanged++;
                }
            }

            return new TagSyncResult(tagsRemoved, mediaItemsChanged);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Finds direct TMDb movie-collection memberships for movies in selected libraries without changing tags.</summary>
    public async Task<IReadOnlyCollection<CollectionMatchDto>> ScanCollectionMatchesAsync(CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (configuration.LibraryIds.Length == 0)
        {
            throw new InvalidOperationException("Select and save at least one library before scanning collections.");
        }

        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            throw new InvalidOperationException("Save a TMDb API Read Access Token before scanning collections.");
        }

        var tmdb = _sources.OfType<TmdbAvailabilitySource>().FirstOrDefault()
            ?? throw new InvalidOperationException("TMDb is unavailable.");
        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = configuration.LibraryIds.SelectMany(libraryId => _libraryManager.GetItemList(new InternalItemsQuery
            {
                ParentId = libraryId,
                Recursive = true,
                IncludeItemTypes = [BaseItemKind.Movie]
            }).Select(item => (Item: item, LibraryId: libraryId))).ToArray();
            var matches = new ConcurrentBag<CollectionMatchDto>();
            await Parallel.ForEachAsync(entries, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 3 }, async (entry, token) =>
            {
                var collection = await tmdb.GetCollectionAsync(new ExternalIds(GetProviderId(entry.Item, "Tmdb"), GetProviderId(entry.Item, "Imdb"), entry.Item.GetType().Name), token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(collection))
                {
                    matches.Add(new CollectionMatchDto(entry.Item.Id, entry.LibraryId, entry.Item.Name, collection, "TMDb"));
                }
            }).ConfigureAwait(false);
            return matches.OrderBy(match => match.LibraryId).ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>Verifies and adds administrator-selected direct collection matches after making one complete backup.</summary>
    public async Task<TagApplyResult> ApplyCollectionMatchesAsync(IEnumerable<CollectionMatchDto> matches, CancellationToken cancellationToken)
    {
        var selected = matches.GroupBy(match => match.ItemId).Select(group => group.First()).ToArray();
        if (selected.Length == 0)
        {
            return new TagApplyResult(0, 0);
        }

        await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
            _destinations.Validate(configuration, configuration.LibraryIds);
            var tmdb = _sources.OfType<TmdbAvailabilitySource>().FirstOrDefault()
                ?? throw new InvalidOperationException("TMDb is unavailable.");
            await _backups.CreateAsync("Before selected collection tags", configuration.LibraryIds, cancellationToken).ConfigureAwait(false);
            var changed = 0;
            var added = 0;
            foreach (var match in selected)
            {
                var item = _libraryManager.GetItemById(match.ItemId);
                if (item is not Movie || !configuration.LibraryIds.Contains(item.GetTopParent().Id))
                {
                    continue;
                }

                // Do not trust a browser-submitted collection label. Confirm the
                // current direct TMDb membership before writing any Collection tag.
                var collectionName = await tmdb.GetCollectionAsync(
                    new ExternalIds(GetProviderId(item, "Tmdb"), GetProviderId(item, "Imdb"), item.GetType().Name),
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(collectionName))
                {
                    continue;
                }

                var tag = TagNaming.Format(TagKind.Collection, collectionName);
                if ((item.Tags ?? []).Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                item.Tags = (item.Tags ?? []).Append(tag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                await _destinations.SaveAsync(item, configuration, cancellationToken).ConfigureAwait(false);
                changed++;
                added++;
            }

            return new TagApplyResult(added, changed);
        }
        finally
        {
            _scanLock.Release();
        }
    }

    private async Task<IReadOnlyCollection<SourceTag>> ScanItemAsync(BaseItem item, Guid libraryId, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var ids = new ExternalIds(
            GetProviderId(item, "Tmdb"),
            GetProviderId(item, "Imdb"),
            item.GetType().Name);

        var results = new List<SourceLookupResult>();
        var tmdb = _sources.FirstOrDefault(source => string.Equals(source.Name, "TMDb", StringComparison.Ordinal));
        if (tmdb is not null)
        {
            results.Add(await LookupSafelyAsync(tmdb, ids, cancellationToken).ConfigureAwait(false));
            if (tmdb is TmdbAvailabilitySource tmdbMetadata)
            {
                results.Add(await tmdbMetadata.LookupClassificationsAsync(ids, configuration.TagGenres, configuration.TagKeywords, cancellationToken).ConfigureAwait(false));
            }
        }

        // Watchmode is the quota-limited fallback. It is queried only for a
        // requested kind that TMDb did not return; TMDb stays authoritative for
        // a kind it did return.
        var primaryProvidersFound = results.SelectMany(result => result.Tags).Any(tag => tag.Kind == TagKind.Provider);
        var primaryNetworksFound = results.SelectMany(result => result.Tags).Any(tag => tag.Kind == TagKind.Network);
        var watchmode = _sources.FirstOrDefault(source => string.Equals(source.Name, "Watchmode", StringComparison.Ordinal));
        var needsProviderFallback = configuration.TagProviders && !primaryProvidersFound;
        var needsNetworkFallback = configuration.TagNetworks && item is Series && !primaryNetworksFound;
        if ((needsProviderFallback || needsNetworkFallback) && watchmode is not null)
        {
            results.Add(await LookupSafelyAsync(watchmode, ids, cancellationToken).ConfigureAwait(false));
        }

        var tags = results
            .SelectMany(result => result.Tags)
            .Where(tag => !string.Equals(tag.Source, "Watchmode", StringComparison.Ordinal)
                || (tag.Kind == TagKind.Provider && !primaryProvidersFound)
                || (tag.Kind == TagKind.Network && !primaryNetworksFound))
            .ToArray();
        await ApplyTagsAsync(
            item,
            libraryId,
            tags,
            results.Select(result => result.Source),
            results.All(result => string.IsNullOrWhiteSpace(result.Note)),
            cancellationToken).ConfigureAwait(false);
        return tags;
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
        var discoveredProviders = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var discoveredNetworks = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        await Parallel.ForEachAsync(items, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            // A conservative fixed concurrency replaces the exposed setting.
            // Watchmode quota reservation is serialized separately.
            MaxDegreeOfParallelism = 3
        }, async (entry, token) =>
        {
            _state.Report(Volatile.Read(ref completed), entry.Item.Name);
            var tags = await ScanItemAsync(entry.Item, entry.LibraryId, token).ConfigureAwait(false);
            foreach (var tag in tags)
            {
                if (tag.Kind == TagKind.Provider)
                {
                    discoveredProviders.TryAdd(tag.Name, 0);
                }
                else if (tag.Kind == TagKind.Network)
                {
                    discoveredNetworks.TryAdd(tag.Name, 0);
                }
            }
            var count = Interlocked.Increment(ref completed);
            _state.Report(count, entry.Item.Name);
            jellyfinProgress?.Report(items.Count == 0 ? 100 : count * 100d / items.Count);
        }).ConfigureAwait(false);
        RememberKnownTags(discoveredProviders.Keys, discoveredNetworks.Keys);
        _state.Complete();
    }

    private static void EnsureSourceConfigured(Configuration.PluginConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey)
            && string.IsNullOrWhiteSpace(configuration.WatchmodeApiKey))
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
        bool forceManagedReplacement = false,
        IReadOnlyCollection<TagKind>? replaceKinds = null)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        var selectedProviderNames = new HashSet<string>((configuration.SelectedProviderNames ?? []).Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name)), StringComparer.OrdinalIgnoreCase);
        var selectedNetworkNames = new HashSet<string>((configuration.SelectedNetworkNames ?? []).Select(name => TagNameNormalizer.Normalize(TagKind.Network, name)), StringComparer.OrdinalIgnoreCase);
        var selectedGenreNames = new HashSet<string>((configuration.SelectedGenreNames ?? []).Select(name => TagNameNormalizer.Normalize(TagKind.Genre, name)), StringComparer.OrdinalIgnoreCase);
        var normalized = values
            .Select(value => value with { Name = TagNameNormalizer.Normalize(value.Kind, value.Name) })
            .ToArray();
        var hasTvNetworkApp = normalized.Any(static value => value.Kind == TagKind.Provider && value.IsTvNetworkApp);
        var selectedValues = normalized
            .Where(value => (value.Kind == TagKind.Provider && configuration.TagProviders)
                || (value.Kind == TagKind.Network && configuration.TagNetworks)
                || (value.Kind == TagKind.Genre && configuration.TagGenres && selectedGenreNames.Contains(value.Name))
                || (value.Kind == TagKind.Keyword && configuration.TagKeywords)
                || value.Kind == TagKind.Collection)
            .Where(value => !value.IsTvNetworkApp || !string.Equals(configuration.TvNetworkAppTaggingMode, "NetworkOnly", StringComparison.Ordinal))
            .Where(value => !hasTvNetworkApp || !string.Equals(configuration.TvNetworkAppTaggingMode, "StreamingAppOnly", StringComparison.Ordinal) || value.Kind != TagKind.Network)
            .Where(value => value.Kind != TagKind.Provider || !configuration.RestrictProvidersToSelected || selectedProviderNames.Contains(value.Name))
            .Where(value => value.Kind != TagKind.Network || !configuration.RestrictNetworksToSelected || selectedNetworkNames.Contains(value.Name))
            .Where(value => !string.IsNullOrWhiteSpace(value.Name))
            .ToArray();
        await _logos.CacheAsync(selectedValues, cancellationToken).ConfigureAwait(false);
        var selected = selectedValues
            .Select(value => TagNaming.Format(value.Kind, value.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = item.Tags ?? [];
        var tagsAdded = selected.Count(tag => !existing.Contains(tag, StringComparer.OrdinalIgnoreCase));
        // A failed source must never erase previously known availability. A later healthy scan reconciles it.
        // Scheduled replacement reconciles current availability only. Genre and
        // keyword tags are explicitly controlled through their own settings,
        // sync, and removal actions; collection tags are additive by design.
        var replacementKinds = (replaceKinds ?? normalized
            .Where(value => value.Kind is TagKind.Provider or TagKind.Network)
            .Select(value => value.Kind)
            .Distinct())
            .ToHashSet();
        var retained = (configuration.ReplaceManagedTags || forceManagedReplacement) && replaceManagedTags
            ? existing.Where(tag => !TagNaming.TryGetKind(tag, out var kind) || !replacementKinds.Contains(kind))
            : existing;
        item.Tags = retained.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await _destinations.SaveAsync(item, configuration, cancellationToken).ConfigureAwait(false);
        _state.RecordTagAdditions(tagsAdded);
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
            tags.Where(tag => tag.StartsWith(TagNaming.GenrePrefix, StringComparison.OrdinalIgnoreCase)).Select(tag => tag[TagNaming.GenrePrefix.Length..]).ToArray(),
            tags.Where(tag => tag.StartsWith(TagNaming.KeywordPrefix, StringComparison.OrdinalIgnoreCase)).Select(tag => tag[TagNaming.KeywordPrefix.Length..]).ToArray(),
            tags.Where(tag => tag.StartsWith(TagNaming.CollectionPrefix, StringComparison.OrdinalIgnoreCase)).Select(tag => tag[TagNaming.CollectionPrefix.Length..]).ToArray(),
            checkedUtc,
            sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string? GetProviderId(BaseItem item, string name) =>
        item.ProviderIds is not null && item.ProviderIds.TryGetValue(name, out var value) ? value : null;

    private void RememberKnownTags(IEnumerable<string> providers, IEnumerable<string> networks)
    {
        lock (_knownTagLock)
        {
            var configuration = Plugin.Instance?.Configuration;
            if (configuration is null)
            {
                return;
            }

            var knownProviders = configuration.KnownProviderNames ?? [];
            var knownNetworks = configuration.KnownNetworkNames ?? [];
            var updatedProviders = knownProviders.Concat(providers).Where(static name => !string.IsNullOrWhiteSpace(name)).Select(name => TagNameNormalizer.Normalize(TagKind.Provider, name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            var updatedNetworks = knownNetworks.Concat(networks).Where(static name => !string.IsNullOrWhiteSpace(name)).Select(name => TagNameNormalizer.Normalize(TagKind.Network, name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (updatedProviders.SequenceEqual(knownProviders, StringComparer.OrdinalIgnoreCase)
                && updatedNetworks.SequenceEqual(knownNetworks, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            configuration.KnownProviderNames = updatedProviders;
            configuration.KnownNetworkNames = updatedNetworks;
            Plugin.Instance?.SaveConfiguration(configuration);
        }
    }

    private static string[] NormalizeNames(TagKind kind, IEnumerable<string> names) => names
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Select(name => TagNameNormalizer.Normalize(kind, name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool IsTagKind(string tag, TagKind kind) => tag.StartsWith(TagNaming.Prefix(kind), StringComparison.OrdinalIgnoreCase);

    private static string RemoveTagPrefix(string tag, TagKind kind) => tag[TagNaming.Prefix(kind).Length..];
}
