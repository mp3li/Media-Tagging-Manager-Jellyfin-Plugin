using System.Collections.Concurrent;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaTaggingManager.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MediaTaggingManager.Services;

/// <summary>Fills missing native Jellyfin Studio and production-country metadata from direct TMDb title details.</summary>
public sealed class ProductionManager
{
    private readonly ILibraryManager _libraryManager;
    private readonly TmdbAvailabilitySource _tmdb;
    private readonly TagDestinationWriter _writer;
    private readonly ProviderNetworkLogoCache _logos;
    private readonly ProductionOwnershipStore _ownership;
    private readonly ProductionStateStore _state;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ProductionChange> _changes = new();

    /// <summary>Initializes the production metadata manager.</summary>
    public ProductionManager(ILibraryManager libraryManager, TmdbAvailabilitySource tmdb, TagDestinationWriter writer, ProviderNetworkLogoCache logos, ProductionOwnershipStore ownership, ProductionStateStore state)
    {
        _libraryManager = libraryManager;
        _tmdb = tmdb;
        _writer = writer;
        _logos = logos;
        _ownership = ownership;
        _state = state;
    }

    /// <summary>Gets whether a normal scan has production metadata work enabled.</summary>
    public static bool IsConfigured(Configuration.PluginConfiguration configuration) => configuration.AddProductionCompanies || configuration.AddProductionCountries;

    /// <summary>Starts a new latest-operation review without altering current Jellyfin metadata.</summary>
    public void StartChangeReview() => _changes.Clear();

    /// <summary>Returns a clear administrator-facing prerequisite error for the dedicated production action.</summary>
    public string? GetScanValidationError()
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("The plugin configuration is unavailable.");
        if (!IsConfigured(configuration))
        {
            return "Enable Add Production Companies and/or Add Production Countries, then save Production Companies and Countries Settings before loading.";
        }

        if (string.IsNullOrWhiteSpace(configuration.TmdbApiKey))
        {
            return "Save a TMDb API Read Access Token in Main Settings before loading production companies and countries.";
        }

        return configuration.LibraryIds.Length == 0
            ? "Select and save one or more libraries in Main Settings before loading production companies and countries."
            : null;
    }

    /// <summary>Loads configured production metadata for all selected-library Movies and Series without running other scan features.</summary>
    public async Task ScanConfiguredLibrariesAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var validationError = GetScanValidationError();
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        var configuration = Plugin.Instance!.Configuration;
        var candidates = configuration.LibraryIds
            .SelectMany(libraryId => GetLibraryItems(libraryId).Select(item => (Item: item, LibraryId: libraryId)))
            .ToArray();
        StartChangeReview();
        _state.Start(candidates.Length);
        try
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _state.Record(await ApplyConfiguredAsync(candidates[index].Item, candidates[index].LibraryId, cancellationToken).ConfigureAwait(false));
                progress?.Report(candidates.Length == 0 ? 100 : (index + 1) * 100d / candidates.Length);
            }

            _state.Complete();
        }
        catch (OperationCanceledException)
        {
            _state.Complete("Production companies and countries action was cancelled. Metadata already added remains available.");
            throw;
        }
        catch (Exception exception)
        {
            _state.Complete($"Production companies and countries action stopped: {exception.Message}");
            throw;
        }
    }

    /// <summary>Fills missing native production metadata for one Movie or Series.</summary>
    public async Task<ProductionOperationResult> ApplyConfiguredAsync(BaseItem item, Guid libraryId, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!IsConfigured(configuration) || item is not Movie && item is not Series)
        {
            return new ProductionOperationResult(0, 0, 0, 0, 0);
        }

        var ids = new ExternalIds(GetProviderId(item, "Tmdb"), GetProviderId(item, "Imdb"), item.GetType().Name);
        if (string.IsNullOrWhiteSpace(ids.Tmdb))
        {
            return new ProductionOperationResult(0, 0, 0, 0, 0);
        }

        TmdbProductionResult source;
        try
        {
            source = await _tmdb.GetProductionAsync(ids, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new ProductionOperationResult(0, 0, 0, 0, 0);
        }
        catch (System.Text.Json.JsonException)
        {
            return new ProductionOperationResult(0, 0, 0, 0, 0);
        }

        if (!string.IsNullOrWhiteSpace(source.Note))
        {
            return new ProductionOperationResult(0, 0, 0, 0, 0);
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existingCompanies = (item.Studios ?? []).Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).ToList();
            var existingCountries = (item.ProductionLocations ?? []).Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).ToList();
            var companies = configuration.AddProductionCompanies
                ? source.Companies.Where(company => !Contains(existingCompanies, company.Name)).ToArray()
                : [];
            var allowedCountries = (configuration.SelectedProductionCountryCodes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var countries = configuration.AddProductionCountries
                ? source.Countries.Where(country => allowedCountries.Contains(country.Code) && !Contains(existingCountries, country.Name)).ToArray()
                : [];

            // A source-supplied company logo is useful even when the native
            // Studio value already existed before this plugin. The bounded
            // cache keeps one logo per company and avoids a second metadata
            // write just to make the logo available.
            if (configuration.SaveProductionCompanyLogos)
            {
                await _logos.CacheAsync(source.Companies.Where(company => !string.IsNullOrWhiteSpace(company.LogoUrl))
                    .Select(company => new SourceTag(TagKind.ProductionCompany, company.Name, "TMDb", false, company.LogoUrl)), cancellationToken).ConfigureAwait(false);
            }

            if (companies.Length == 0 && countries.Length == 0)
            {
                return new ProductionOperationResult(0, 0, 0, 0, 0);
            }

            item.Studios = existingCompanies.Concat(companies.Select(company => company.Name.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            item.ProductionLocations = existingCountries.Concat(countries.Select(country => country.Name.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            await _writer.SaveAsync(item, cancellationToken).ConfigureAwait(false);

            await _ownership.RecordAsync(
                companies.Select(company => new ProductionOwnedValue { ItemId = item.Id, LibraryId = libraryId, Kind = "Company", Name = company.Name.Trim(), LogoUrl = company.LogoUrl })
                    .Concat(countries.Select(country => new ProductionOwnedValue { ItemId = item.Id, LibraryId = libraryId, Kind = "Country", Name = country.Name.Trim(), CountryCode = country.Code })),
                cancellationToken).ConfigureAwait(false);

            RecordChange(item, libraryId, companies.Select(company => company.Name), [], countries.Select(country => country.Name), []);
            return new ProductionOperationResult(companies.Length, countries.Length, 0, 0, 1);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>Removes only native company and country values recorded as created by this plugin in selected libraries.</summary>
    public async Task<ProductionOperationResult> RemoveOwnedAsync(bool companies, bool countries, CancellationToken cancellationToken)
    {
        var selectedLibraries = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        var owned = (await _ownership.GetAsync(cancellationToken).ConfigureAwait(false)).Values
            .Where(value => selectedLibraries.Contains(value.LibraryId)
                && ((companies && value.Kind == "Company") || (countries && value.Kind == "Country")))
            .GroupBy(value => value.ItemId)
            .ToArray();
        var companiesRemoved = 0;
        var countriesRemoved = 0;
        var itemsChanged = 0;
        var handled = new List<ProductionOwnedValue>();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _changes.Clear();
            foreach (var group in owned)
            {
                var item = _libraryManager.GetItemById(group.Key);
                if (item is not Movie && item is not Series || item is null)
                {
                    continue;
                }

                var removedCompanies = group.Where(value => value.Kind == "Company" && Contains(item.Studios ?? [], value.Name)).Select(value => value.Name).ToArray();
                var removedCountries = group.Where(value => value.Kind == "Country" && Contains(item.ProductionLocations ?? [], value.Name)).Select(value => value.Name).ToArray();
                var changed = false;
                if (removedCompanies.Length > 0)
                {
                    item.Studios = (item.Studios ?? []).Where(value => !Contains(removedCompanies, value)).ToArray();
                    companiesRemoved += removedCompanies.Length;
                    changed = true;
                }

                if (removedCountries.Length > 0)
                {
                    item.ProductionLocations = (item.ProductionLocations ?? []).Where(value => !Contains(removedCountries, value)).ToArray();
                    countriesRemoved += removedCountries.Length;
                    changed = true;
                }

                if (changed)
                {
                    await _writer.SaveAsync(item, cancellationToken).ConfigureAwait(false);
                    itemsChanged++;
                    RecordChange(item, item.GetTopParent().Id, [], removedCompanies, [], removedCountries);
                }

                handled.AddRange(group);
            }

            await _ownership.RemoveAsync(handled, cancellationToken).ConfigureAwait(false);
            return new ProductionOperationResult(0, 0, companiesRemoved, countriesRemoved, itemsChanged);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>Applies one administrator edit to native Studio and production-country metadata in a selected library.</summary>
    public async Task UpdateItemAsync(Guid itemId, IEnumerable<string> companies, IEnumerable<string> countries, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId) ?? throw new KeyNotFoundException("The Jellyfin item no longer exists.");
        var selected = (Plugin.Instance?.Configuration.LibraryIds ?? []).ToHashSet();
        if ((item is not Movie && item is not Series) || !selected.Contains(item.GetTopParent().Id))
        {
            throw new InvalidOperationException("Only Movies and Series in selected libraries may be edited by Media Tagging Manager.");
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            item.Studios = Names(companies);
            item.ProductionLocations = Names(countries);
            await _writer.SaveAsync(item, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>Caches source-supplied logos for production companies this plugin has recorded from selected libraries.</summary>
    public async Task<int> LoadCompanyLogosAsync(CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
        if (!configuration.SaveProductionCompanyLogos)
        {
            throw new InvalidOperationException("Enable Save Production Company Logos and save Production Companies and Countries Settings before loading logos.");
        }

        var selected = (configuration.LibraryIds ?? []).ToHashSet();
        var companies = (await _ownership.GetAsync(cancellationToken).ConfigureAwait(false)).Values
            .Where(value => value.Kind == "Company" && selected.Contains(value.LibraryId) && !string.IsNullOrWhiteSpace(value.LogoUrl))
            .GroupBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(value => new SourceTag(TagKind.ProductionCompany, value.Name, "TMDb", false, value.LogoUrl))
            .ToArray();
        await _logos.CacheAsync(companies, cancellationToken).ConfigureAwait(false);
        return companies.Length;
    }

    /// <summary>Returns native production metadata for current selected-library Movies and Series with latest-operation overlays.</summary>
    public IEnumerable<ProductionOverviewItemDto> GetOverview(Guid? requestedLibraryId)
    {
        var selectedLibraries = Plugin.Instance?.Configuration.LibraryIds ?? [];
        var libraries = requestedLibraryId.HasValue ? selectedLibraries.Where(value => value == requestedLibraryId.Value) : selectedLibraries;
        return libraries.SelectMany(libraryId => GetLibraryItems(libraryId).Select(item => ToOverview(item, libraryId)))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ProductionOverviewItemDto ToOverview(BaseItem item, Guid libraryId)
    {
        var changes = _changes.TryGetValue(item.Id, out var change) ? change : ProductionChange.Empty;
        return new ProductionOverviewItemDto(item.Id, item.Name, item.GetType().Name, libraryId,
            (item.Studios ?? []).Where(static value => !string.IsNullOrWhiteSpace(value)).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            (item.ProductionLocations ?? []).Where(static value => !string.IsNullOrWhiteSpace(value)).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            changes.AddedCompanies, changes.RemovedCompanies, changes.AddedCountries, changes.RemovedCountries);
    }

    private void RecordChange(BaseItem item, Guid libraryId, IEnumerable<string> addedCompanies, IEnumerable<string> removedCompanies, IEnumerable<string> addedCountries, IEnumerable<string> removedCountries)
    {
        _changes.AddOrUpdate(item.Id,
            _ => new ProductionChange(libraryId, Names(addedCompanies), Names(removedCompanies), Names(addedCountries), Names(removedCountries)),
            (_, current) => new ProductionChange(libraryId, Combine(current.AddedCompanies, addedCompanies), Combine(current.RemovedCompanies, removedCompanies), Combine(current.AddedCountries, addedCountries), Combine(current.RemovedCountries, removedCountries)));
    }

    private IEnumerable<BaseItem> GetLibraryItems(Guid libraryId) => _libraryManager.GetItemList(new InternalItemsQuery
    {
        ParentId = libraryId,
        Recursive = true,
        IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series]
    });

    private static string? GetProviderId(BaseItem item, string name) => item.ProviderIds.TryGetValue(name, out var value) ? value : null;
    private static bool Contains(IEnumerable<string> values, string name) => values.Any(value => string.Equals(value?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    private static string[] Names(IEnumerable<string> values) => values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    private static string[] Combine(IEnumerable<string> existing, IEnumerable<string> additional) => Names(existing.Concat(additional));

    private sealed record ProductionChange(Guid LibraryId, string[] AddedCompanies, string[] RemovedCompanies, string[] AddedCountries, string[] RemovedCountries)
    {
        public static ProductionChange Empty { get; } = new(Guid.Empty, [], [], [], []);
    }
}
